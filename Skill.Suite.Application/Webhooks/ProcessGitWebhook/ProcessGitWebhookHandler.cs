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

        if (string.IsNullOrWhiteSpace(session.JudgementImage))
            return TestRunErrors.SessionMissingJudgementImage;

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

        var supersededPrevious = await SupersedePreviousRunsAsync(
            session.Id, enrolment.CompetitorId, cancellationToken);

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

        db.TestRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(new TestRunWorkItem(run.Id), cancellationToken);

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
            logger.LogWarning(
                "Session {SessionId} has no webhook secret, so the push to {Owner} was accepted unsigned. " +
                "Start the session through the UI to install a signed hook.",
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
    /// Scoped to the session as well as the competitor. Filtering on the competitor alone also cancelled
    /// their runs in every other session, so a push in one session killed a submission being judged in
    /// another.
    /// </remarks>
    private async Task<bool> SupersedePreviousRunsAsync(
        Guid sessionId, Guid competitorId, CancellationToken cancellationToken)
    {
        var affected = await db.TestRuns
            .Where(r => r.SessionId == sessionId &&
                        r.CompetitorId == competitorId &&
                        (r.Status == TestRunStatus.Pending ||
                         r.Status == TestRunStatus.Cloning ||
                         r.Status == TestRunStatus.Running))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, TestRunStatus.Cancelled)
                .SetProperty(r => r.FailureReason, TestRunReasons.Superseded)
                .SetProperty(r => r.FinishedAt, (DateTime?)DateTime.UtcNow),
                cancellationToken);

        registry.CancelForCompetitor(competitorId);
        return affected > 0;
    }
}
