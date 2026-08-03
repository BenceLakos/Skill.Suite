using System.Security.Cryptography;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.StartSession;

public sealed class StartSessionHandler(
    IAppDbContext db,
    IGitHostClient gitHost,
    IGitClient git,
    IPasswordVault vault,
    IOptions<WebhookOptions> webhookOptions,
    ILogger<StartSessionHandler> logger)
    : IRequestHandler<StartSessionCommand, Result<StartSessionResult>>
{
    /// <summary>The branch competitors work on, and the only one the webhook accepts.</summary>
    private const string DefaultBranch = "main";

    public async ValueTask<Result<StartSessionResult>> Handle(
        StartSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .Include(s => s.Competitors)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (session is null)
            return SessionErrors.NotFound(request.Id);

        var preflight = await PreflightAsync(session, cancellationToken);
        if (preflight.IsFailure)
            return Result.Failure<StartSessionResult>(preflight.Error);

        var admin = await ResolveAdminCredentialAsync(session.GitCredentialId, cancellationToken);
        if (admin is null)
            return SessionErrors.MissingGitCredential;

        var competitors = await db.Competitors
            .AsNoTracking()
            .OrderBy(c => c.Username)
            .ToListAsync(cancellationToken);

        if (competitors.Count == 0)
            return SessionErrors.NoCompetitors;

        // Held in plaintext only for the duration of this call: the git host needs the literal key to sign
        // deliveries with, while what is persisted is protected the same way a credential secret is.
        //
        // REUSED when the session already has one, rather than rotated. Rotating made the documented repair
        // action a mark-loss event: re-pressing Start on a live session replaced the secret immediately but
        // reinstalled the hook carrying it only at the very end, so for the whole provisioning window every
        // other competitor's pushes were rejected as unsigned. Reusing keeps the installed hooks valid
        // throughout, and Start stays idempotent.
        var secret = session.WebhookSecret is { Length: > 0 } existing
            ? vault.Unprotect(existing)
            : Convert.ToHexString(RandomNumberGenerator.GetBytes(WebhookSecretBytes));

        // Flip the status first so a provisioning failure leaves an Active session with some repositories
        // rather than a Draft session with repositories nobody can push to. Re-running Start then retries.
        var started = session.Start(vault.Protect(secret));
        if (started.IsFailure)
            return Result.Failure<StartSessionResult>(started.Error);

        await db.SaveChangesAsync(cancellationToken);

        var organization = session.GitOrganization;
        var template = new RepositoryReference(organization, Session.TemplateRepositoryName);

        await gitHost.EnsureOrganizationAsync(
            new EnsureOrganizationRequest(organization, session.Name, admin), cancellationToken);

        await gitHost.EnsureTemplateRepositoryAsync(
            new EnsureRepositoryRequest(template, $"Starter package for {session.Name}", admin),
            cancellationToken);

        await SeedTemplateAsync(session, template, admin, cancellationToken);

        var failures = await ProvisionCompetitorsAsync(
            session, template, competitors, admin, cancellationToken);

        // The webhook goes on LAST, deliberately. An organisation hook fires for the pushes provisioning
        // itself performs, so installing it first produces a judgement run per competitor at start - each
        // attributed to nothing, since the template repository is not a competitor.
        await gitHost.EnsureOrganizationWebhookAsync(
            new EnsureWebhookRequest(
                organization,
                webhookOptions.Value.PublicWebhookUrl,
                secret,
                DefaultBranch,
                admin),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var provisioned = session.Competitors.Count(c => c.ProvisionStatus == SessionProvisionStatus.Provisioned);
        logger.LogInformation(
            "Session {SessionId} started: {Provisioned} repositories provisioned in {Org}, {Failed} failed",
            session.Id, provisioned, organization, failures.Count);

        return new StartSessionResult(organization, provisioned, failures);
    }

    /// <summary>
    /// Everything that must hold before any repository is created, checked while it is still cheap to say no.
    /// </summary>
    private async Task<Result> PreflightAsync(Session session, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.TemplateFolder))
            return Result.Failure(SessionErrors.MissingTemplateFolder);

        // The path is read by this process, so it has to exist *inside the container*. A path that is only
        // valid on the host produces repositories with no starter package and no obvious cause.
        if (!Directory.Exists(session.TemplateFolder) ||
            !Directory.EnumerateFileSystemEntries(session.TemplateFolder).Any())
        {
            return Result.Failure(SessionErrors.TemplateFolderNotFound);
        }

        if (session.GitCredentialId is null)
            return Result.Failure(SessionErrors.MissingGitCredential);

        if (string.IsNullOrWhiteSpace(webhookOptions.Value.GitInternalBaseUrl))
            return Result.Failure(SessionErrors.GitHostNotConfigured);

        // A push is matched to a session through the organisation that owns the repository, but the run that
        // gets created still needs exactly one active session to charge the judgement to.
        var otherActive = await db.Sessions
            .AnyAsync(s => s.Id != session.Id && s.Status == SessionStatus.Active, cancellationToken);

        return otherActive ? Result.Failure(SessionErrors.AnotherSessionActive) : Result.Success();
    }

    /// <summary>
    /// Pushes the starter package into the template repository, unless it already has content.
    /// </summary>
    private async Task SeedTemplateAsync(
        Session session,
        RepositoryReference template,
        BasicCredential admin,
        CancellationToken cancellationToken)
    {
        var alreadySeeded = await gitHost.WaitForRepositoryContentAsync(
            template, admin, TimeSpan.Zero, cancellationToken);

        if (alreadySeeded)
        {
            logger.LogInformation("Template {Owner}/{Name} already has content; not re-seeding",
                template.Owner, template.Name);
            return;
        }

        // Non-null by preflight, which refuses to start a session when the git host is not configured.
        var baseUrl = webhookOptions.Value.GitInternalBaseUrl!.TrimEnd('/');
        var url = $"{baseUrl}/{template.Owner}/{template.Name}.git";

        await git.PushDirectoryAsync(
            new GitPushDirectoryRequest(
                url,
                session.TemplateFolder!,
                DefaultBranch,
                $"Starter package for {session.Name}",
                admin),
            cancellationToken);

        // Not optional, and not merely defensive. Copying a template the server does not yet see as having
        // content produces empty competitor repositories and reports success for every one of them.
        var visible = await gitHost.WaitForRepositoryContentAsync(
            template, admin, TemplateVisibilityTimeout, cancellationToken);

        if (!visible)
        {
            throw new InvalidOperationException(
                $"The starter package was pushed to {template.Owner}/{template.Name} but the git host still " +
                "reports it as empty. Copying competitor repositories from it now would silently produce " +
                "empty repositories, so provisioning stopped instead.");
        }
    }

    private async Task<List<StartSessionFailure>> ProvisionCompetitorsAsync(
        Session session,
        RepositoryReference template,
        List<Competitor> competitors,
        BasicCredential admin,
        CancellationToken cancellationToken)
    {
        var failures = new List<StartSessionFailure>();

        foreach (var competitor in competitors)
        {
            // The repository name IS the username: it is what the competitor sees, and the enrolment row
            // makes attribution exact regardless, so there is nothing to gain from decorating it.
            var isNewEnrolment = session.Competitors.All(c => c.CompetitorId != competitor.Id);
            var enrolment = session.EnrolCompetitor(competitor.Id, competitor.Username);

            // db.Add, not just adding to the navigation collection. SessionCompetitor.Create assigns its own
            // Guid key, and EF reads a non-default key as "this row already exists" - so the insert is
            // skipped silently. Everything else still succeeds: repositories are created, the secret is
            // stored, SaveChanges reports no error, and every later push is rejected as CompetitorNotFound
            // because the row the webhook resolves through was never written.
            if (isNewEnrolment)
                db.Add(enrolment);

            var target = new RepositoryReference(session.GitOrganization, competitor.Username);

            try
            {
                var cloneUrl = await gitHost.GenerateRepositoryFromTemplateAsync(
                    new GenerateRepositoryRequest(template, target, DefaultBranch, admin),
                    cancellationToken);

                enrolment.MarkProvisioned(cloneUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One competitor's repository failing must not deny the rest theirs.
                logger.LogError(ex, "Could not provision a repository for {Username}", competitor.Username);
                enrolment.MarkFailed(ex.Message);
                failures.Add(new StartSessionFailure(competitor.Username, ex.Message));
            }

            // Saved per competitor, not once at the end. The enrolment row is what the webhook resolves a push
            // through, so a throw partway used to leave an Active session with repositories on the git host and
            // zero rows in the database — every push then rejected as CompetitorNotFound, with the session
            // looking perfectly live in the UI. Committing each row as it is decided means an interrupted Start
            // leaves exactly the competitors it got to, which is also what makes re-running it a real retry.
            await db.SaveChangesAsync(cancellationToken);
        }

        return failures;
    }

    private async Task<BasicCredential?> ResolveAdminCredentialAsync(
        Guid? credentialId, CancellationToken cancellationToken)
    {
        if (credentialId is null)
            return null;

        var credential = await db.Credentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == credentialId.Value, cancellationToken);

        return credential is null ? null : BasicCredential.Parse(vault.Unprotect(credential.EncryptedSecret));
    }

    /// <summary>256-bit HMAC key, matching what the signature filter verifies with.</summary>
    private const int WebhookSecretBytes = 32;

    private static readonly TimeSpan TemplateVisibilityTimeout = TimeSpan.FromSeconds(30);
}
