using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.Webhooks.ProcessGitWebhook;

public sealed class ProcessGitWebhookHandler(
    IAppDbContext db,
    IBackgroundTaskQueue queue,
    IActiveTestRunRegistry registry,
    IPasswordVault vault,
    IOptions<WebhookOptions> options,
    ILogger<ProcessGitWebhookHandler> logger)
    : IRequestHandler<ProcessGitWebhookCommand, Result<TestRunAcceptedDto>>
{
    public async ValueTask<Result<TestRunAcceptedDto>> Handle(ProcessGitWebhookCommand request, CancellationToken cancellationToken)
    {
        // The session is identified by the organisation that owns the repository, which provisioning named
        // after the session slug. This replaced "the active session with the earliest start date": that rule
        // ignored the repository entirely, so with two active sessions a push was judged by whichever
        // happened to sort first.
        if (string.IsNullOrWhiteSpace(request.Owner))
            return TestRunErrors.UnknownOrganization(null);

        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Slug == request.Owner && s.Status == SessionStatus.Active,
                cancellationToken);

        if (session is null)
            return TestRunErrors.UnknownOrganization(request.Owner);

        // Not a fault, and never an exception: a session with no judgement image is provisioned and run
        // exactly like any other, it is simply marked by hand. Start leaves such an organisation without a
        // hook, so the only deliveries that reach here come from a hook the host still holds — one installed
        // while the session did name an image, or added by hand. Answered with a refusal rather than turned
        // into a run there would be nothing to execute, and logged so the delivery is explicable.
        if (!session.RequiresJudgement)
        {
            logger.LogInformation(
                "Push to {Owner}/{Repo} is not judged: session {SessionId} declares no judgement image",
                request.Owner, request.RepositorySlug, session.Id);

            return TestRunErrors.SessionMissingJudgementImage;
        }

        var signature = VerifySignature(session, request);
        if (signature.IsFailure)
            return Result.Failure<TestRunAcceptedDto>(signature.Error);

        // The starter package lives in the organisation too, and provisioning pushes to it. Judging that
        // push would create a run belonging to no competitor.
        if (string.Equals(request.RepositorySlug, Session.TemplateRepositoryName, StringComparison.OrdinalIgnoreCase))
            return TestRunErrors.TemplateRepositoryPush;

        // Exact lookup against the enrolment row provisioning wrote. The previous resolver fell back to
        // substring matching on usernames, which credits a push to `alice` to a competitor named `ali`.
        var enrolment = await db.SessionCompetitors
            .FirstOrDefaultAsync(x => x.SessionId == session.Id && x.RepositoryName == request.RepositorySlug,
                cancellationToken);

        if (enrolment is null)
        {
            logger.LogWarning(
                "Push to {Owner}/{Repo} has no enrolment row in session {SessionId}; rejecting",
                request.Owner, request.RepositorySlug, session.Id);
            return TestRunErrors.CompetitorNotFound;
        }

        var competitorUsername = await db.Competitors
            .Where(c => c.Id == enrolment.CompetitorId)
            .Select(c => c.Username)
            .FirstOrDefaultAsync(cancellationToken);

        var folderName = SubmissionFolderNameGenerator.Generate(
            options.Value.FolderTemplate, competitorUsername, request.CommitSha);

        var run = TestRun.Create(
            session.Id,
            enrolment.CompetitorId,
            request.RepositoryUrl,
            request.RepositoryName,
            request.Branch,
            request.CommitSha,
            folderName,
            session.JudgementImage);

        // Everything from here runs with CancellationToken.None, and the accepted submission is persisted
        // BEFORE the older run is superseded. Both halves of that matter, and both were wrong:
        //
        //   * The request token is Gitea's. Its delivery times out after a few seconds, and a superseded run's
        //     `docker stop` used to burn up to ten of them on this thread — so the token was often already
        //     cancelled by the time the new run was saved. The old run ended Cancelled, the new one was never
        //     written, and the competitor's latest work had no result at all. Gitea does not retry.
        //   * Superseding first left a window with nothing recorded. Recording first means the worst case is a
        //     duplicate run, which supersede then resolves, rather than a lost one.
        db.TestRuns.Add(run);
        await db.SaveChangesAsync(CancellationToken.None);

        var supersededPrevious = await SupersedePreviousRunsAsync(
            session.Id, enrolment.CompetitorId, run.Id, CancellationToken.None);

        await queue.EnqueueAsync(new TestRunWorkItem(run.Id), CancellationToken.None);

        return new TestRunAcceptedDto(
            TestRunId: run.Id,
            SessionId: run.SessionId,
            CompetitorId: run.CompetitorId,
            RepositoryUrl: run.RepositoryUrl,
            Branch: run.Branch,
            CommitSha: run.CommitSha,
            FolderName: run.FolderName,
            Status: run.Status,
            CreatedAt: run.CreatedAt,
            SupersededPreviousRun: supersededPrevious);
    }

    /// <summary>
    /// Rejects a delivery whose HMAC does not match the secret this session's webhook was installed with.
    /// </summary>
    /// <remarks>
    /// A session with no secret is accepted unsigned, and says so in the log. That is not a loophole an
    /// attacker can open: only <c>StartSession</c> writes the secret, and it always writes one. It covers
    /// sessions created directly in the database — the scripted end-to-end harness does exactly that — which
    /// were unauthenticated before this check existed and are no worse now.
    /// </remarks>
    private Result VerifySignature(Session session, ProcessGitWebhookCommand request)
    {
        if (session.WebhookSecret is null || session.WebhookSecret.Length == 0)
        {
            // Fails closed. A session started through the UI always has a secret, so reaching here means the
            // session was inserted directly into the database — and accepting the push anyway turned the
            // endpoint into an anonymous judging trigger for anyone who could reach the port.
            if (!options.Value.AllowUnsignedPushes)
            {
                logger.LogWarning(
                    "Rejected a push to {Owner}: session {SessionId} has no webhook secret. Start the session " +
                    "through the UI to install a signed hook, or set Webhook:AllowUnsignedPushes for a local " +
                    "harness only.",
                    request.Owner, session.Id);

                return Result.Failure(TestRunErrors.InvalidSignature);
            }

            logger.LogWarning(
                "Session {SessionId} has no webhook secret and Webhook:AllowUnsignedPushes is on, so the push " +
                "to {Owner} was accepted unsigned. This must never be enabled in production.",
                session.Id, request.Owner);

            return Result.Success();
        }

        string secret;
        try
        {
            secret = vault.Unprotect(session.WebhookSecret);
        }
        catch (Exception ex)
        {
            // The data-protection key ring was rotated or lost, so the stored secret can no longer be read.
            // Rejecting is right, but it must not surface as a 500: an unreadable secret is an operational
            // problem with one fix (start the session again to reinstall the hook), and a 500 per push says
            // nothing about that.
            logger.LogError(ex,
                "Could not decrypt the webhook secret for session {SessionId}. Start the session again to " +
                "regenerate it and reinstall the hook.", session.Id);

            return Result.Failure(TestRunErrors.InvalidSignature);
        }

        if (WebhookSignature.IsValid(request.RawBody, request.Signature, secret))
            return Result.Success();

        // Deliberately terse, and identical whether the signature was absent, malformed or simply wrong:
        // the response is the one thing an attacker can observe.
        logger.LogWarning("Rejected an unsigned or incorrectly signed push to {Owner}/{Repo}",
            request.Owner, request.RepositorySlug);

        return Result.Failure(TestRunErrors.InvalidSignature);
    }

    /// <summary>
    /// Cancels this competitor's in-flight runs in this session, in the database and in the worker.
    /// </summary>
    /// <remarks>
    /// Scoped to the session as well as the competitor, in BOTH halves. Filtering on the competitor alone also
    /// cancelled their runs in every other session, so a push in one session killed a submission being judged in
    /// another. Fixing only the database half left the same bug in the worker: the registry keys on the
    /// competitor, so <c>CancelForCompetitor</c> would still <c>docker stop</c> a container belonging to a run in
    /// a session this push has nothing to do with — and the executing handler, seeing its container stopped,
    /// would record that run as "superseded by a newer submission". The ids are therefore read first and
    /// cancelled individually, which makes the in-memory half scoped by construction rather than by convention.
    /// </remarks>
    private async Task<bool> SupersedePreviousRunsAsync(
        Guid sessionId, Guid competitorId, Guid keepRunId, CancellationToken cancellationToken)
    {
        var superseded = await db.TestRuns
            .Where(r => r.SessionId == sessionId &&
                        r.CompetitorId == competitorId &&
                        // The run this push just created is persisted first, so it has to be excluded or the
                        // very submission being accepted would be cancelled as its own predecessor.
                        r.Id != keepRunId &&
                        (r.Status == TestRunStatus.Pending ||
                         r.Status == TestRunStatus.Cloning ||
                         r.Status == TestRunStatus.Running))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (superseded.Count == 0)
            return false;

        var affected = await db.TestRuns
            .Where(r => superseded.Contains(r.Id) &&
                        (r.Status == TestRunStatus.Pending ||
                         r.Status == TestRunStatus.Cloning ||
                         r.Status == TestRunStatus.Running))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, TestRunStatus.Cancelled)
                .SetProperty(r => r.FailureReason, TestRunReasons.Superseded)
                .SetProperty(r => r.FinishedAt, (DateTime?)DateTime.UtcNow),
                cancellationToken);

        // Signalled per run, after the rows are terminal. CancelForRun resolves through the registry's competitor
        // index and no-ops on a run this process is not executing, so a stale id cannot stop the wrong container.
        foreach (var runId in superseded)
            registry.CancelForRun(runId);

        return affected > 0;
    }
}
