namespace Skill.Suite.Application.Sessions.StartSession;

using System.Security.Cryptography;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Application.Credentials;
using Skill.Suite.Application.DockerImages;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;
using Skill.Suite.Domain.Credentials;
using Skill.Suite.Domain.Sessions;

public sealed class StartSessionHandler(
    IAppDbContext db,
    IGitHostClient gitHost,
    IGitClient git,
    IMsSqlAdminClient msSql,
    IContainerServiceManager containerServices,
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

        var access = await PartitionByGitAccessAsync(session.Id, competitors, admin, cancellationToken);
        if (access.IsFailure)
            return Result.Failure<StartSessionResult>(access.Error);

        var (provisionable, skipped) = access.Value;

        if (provisionable.Count == 0)
            return SessionErrors.NoCompetitorsWithGitAccess;

        if (skipped.Count > 0)
        {
            logger.LogInformation(
                "Session {SessionId}: {Skipped} of {Total} competitors have no git host account and are " +
                "not being provisioned: {Usernames}",
                session.Id, skipped.Count, competitors.Count, string.Join(", ", skipped));
        }

        // Both remaining stages are planned here, before anything is created, for the same reason the git
        // access check is: a missing credential or an unreadable server makes the whole stage impossible, and
        // finding that out after twenty repositories exist turns one clear refusal into twenty identical
        // per-item failures.
        var databasePlan = await PlanDatabaseAccessAsync(session, provisionable, cancellationToken);
        if (databasePlan.IsFailure)
            return Result.Failure<StartSessionResult>(databasePlan.Error);

        var pullCredential = await ResolvePullCredentialAsync(session, cancellationToken);
        if (pullCredential.IsFailure)
            return Result.Failure<StartSessionResult>(pullCredential.Error);

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

        Report(request, SessionProvisioningProgress.Indeterminate(SessionProvisioningStage.Preparing));

        await gitHost.EnsureOrganizationAsync(
            new EnsureOrganizationRequest(organization, session.Name, admin), cancellationToken);

        await gitHost.EnsureTemplateRepositoryAsync(
            new EnsureRepositoryRequest(template, $"Starter package for {session.Name}", admin),
            cancellationToken);

        await SeedTemplateAsync(session, template, admin, cancellationToken);

        var failures = await ProvisionCompetitorsAsync(
            request, session, template, provisionable, admin, cancellationToken);

        var databases = await GrantDatabaseAccessAsync(request, databasePlan.Value, cancellationToken);
        failures.AddRange(databases.Failures);

        var services = await StartServicesAsync(request, session, pullCredential.Value, cancellationToken);
        failures.AddRange(services.Failures);

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
        var skippedNoDatabaseLogin = databasePlan.Value?.SkippedNoDatabaseLogin ?? [];

        logger.LogInformation(
            "Session {SessionId} started: {Provisioned} repositories provisioned in {Org}, " +
            "{Granted} database grants on {Database}, {Services} of {ConfiguredServices} services running, " +
            "{Failed} failures, {SkippedGit} skipped for having no git host account, " +
            "{SkippedDatabase} skipped for having no SQL login",
            session.Id, provisioned, organization,
            databases.Succeeded, databasePlan.Value?.Database ?? NoDatabaseConfigured,
            services.Succeeded, session.DockerImages.Count,
            failures.Count, skipped.Count, skippedNoDatabaseLogin.Count);

        return new StartSessionResult(
            organization,
            provisioned,
            databases.Succeeded,
            services.Succeeded,
            failures,
            skipped,
            skippedNoDatabaseLogin);
    }

    /// <summary>
    /// Which competitors actually hold an account on the git host, asked once for the whole list.
    /// </summary>
    /// <remarks>
    /// Fails closed, and does so before the session is flipped to Active. The only alternative to a readable
    /// user list is provisioning everybody, which is worse than refusing to start: a repository handed to a
    /// competitor who cannot sign in reads as provisioned in every view the admin has, and is only
    /// discovered when the competition has already begun.
    /// <para>
    /// A competitor enrolled by an earlier run whose git account has since been deleted lands in the skipped
    /// list as well, and their enrolment row is left untouched. Re-enrolling would reset a Provisioned row
    /// to Pending and lose the clone URL, while deleting it would throw away the attribution their earlier
    /// pushes were marked under — neither is worth doing for an account that may simply be waiting to be
    /// recreated. Reporting it is enough for the admin to act on.
    /// </para>
    /// </remarks>
    private async Task<Result<AccountAccessPartition>> PartitionByGitAccessAsync(
        Guid sessionId,
        IReadOnlyList<Competitor> competitors,
        BasicCredential admin,
        CancellationToken cancellationToken)
    {
        try
        {
            var hostUsernames = await gitHost.ListUsernamesAsync(admin, cancellationToken);
            return AccountAccessPartitioner.Partition(competitors, hostUsernames);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Could not list the git host users, so session {SessionId} was not started", sessionId);

            return SessionErrors.GitAccessUnknown;
        }
    }

    /// <summary>
    /// What the database stage will do, or null when the session has no shared database.
    /// </summary>
    /// <remarks>
    /// Partitions the competitors who are getting a repository, not every competitor in the database: a
    /// competitor provisioning is skipping is not in this session at all, so reporting them as missing a SQL
    /// login would be noise about somebody who was never going to connect.
    /// <para>
    /// Nobody holding a login is not an error, unlike the git side. The database itself is still worth
    /// creating — the session's services may be the only thing that connects to it — and the skipped list
    /// says plainly that no competitor was granted anything.
    /// </para>
    /// </remarks>
    private async Task<Result<DatabaseAccessPlan?>> PlanDatabaseAccessAsync(
        Session session,
        IReadOnlyList<Competitor> provisionable,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.DatabaseName))
            return Result.Success<DatabaseAccessPlan?>(null);

        var admin = await db.FindByKindAsync(vault, CredentialKind.MsSql, cancellationToken);
        if (admin is null)
            return Result.Failure<DatabaseAccessPlan?>(SessionErrors.MissingDatabaseCredential);

        MsSqlAccountInventory inventory;
        try
        {
            inventory = await msSql.GetInventoryAsync(admin, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Could not read the SQL Server logins, so session {SessionId} was not started", session.Id);

            return Result.Failure<DatabaseAccessPlan?>(SessionErrors.DatabaseAccessUnknown);
        }

        var partition = AccountAccessPartitioner.Partition(provisionable, inventory.Logins);

        return Result.Success<DatabaseAccessPlan?>(new DatabaseAccessPlan(
            session.DatabaseName,
            session.DatabaseReadAccess,
            session.DatabaseWriteAccess,
            admin,
            partition.Provisionable,
            partition.Skipped));
    }

    /// <summary>
    /// The credential the session's service images are pulled with, or null when none is needed.
    /// </summary>
    /// <remarks>
    /// A session naming a credential row that has since been deleted refuses the Start rather than falling
    /// back to an anonymous pull: the fallback fails on every private image anyway, and it fails per service
    /// with a docker error about authentication rather than pointing at the credential the admin removed.
    /// </remarks>
    private async Task<Result<BasicCredential?>> ResolvePullCredentialAsync(
        Session session, CancellationToken cancellationToken)
    {
        if (session.DockerImages.Count == 0 || session.JudgementImagePullCredentialId is null)
            return Result.Success<BasicCredential?>(null);

        var credential = await db.FindByIdAsync(
            vault, session.JudgementImagePullCredentialId.Value, cancellationToken);

        return credential is null
            ? Result.Failure<BasicCredential?>(SessionErrors.MissingImagePullCredential)
            : Result.Success<BasicCredential?>(credential);
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

    /// <summary>
    /// Copies the template into one repository per competitor and grants each of them access to their own.
    /// </summary>
    /// <remarks>
    /// Both halves are needed for a competitor to be provisioned, and both are retried by re-running Start:
    /// the copy is skipped for a repository that already exists, and the grant is applied again over itself.
    /// </remarks>
    private async Task<List<SessionProvisioningFailure>> ProvisionCompetitorsAsync(
        StartSessionCommand request,
        Session session,
        RepositoryReference template,
        IReadOnlyList<Competitor> competitors,
        BasicCredential admin,
        CancellationToken cancellationToken)
    {
        var failures = new List<SessionProvisioningFailure>();

        var total = competitors.Count;
        var completed = 0;

        Report(request, new SessionProvisioningProgress(SessionProvisioningStage.Repositories, completed, total));

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

                // The repository is private and lives in a private organisation, so creating it grants the
                // competitor nothing: without this they cannot clone it, push to it, or even see it exists.
                // A grant that fails is therefore a failed provision, not a warning — the same as no
                // repository at all from where the competitor is sitting.
                await gitHost.EnsureCollaboratorAsync(target, competitor.Username, admin, cancellationToken);

                enrolment.MarkProvisioned(cloneUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One competitor's repository failing must not deny the rest theirs.
                logger.LogError(ex, "Could not provision a repository for {Username}", competitor.Username);
                enrolment.MarkFailed(ex.Message);
                failures.Add(new SessionProvisioningFailure(
                    SessionProvisioningStage.Repositories, competitor.Username, ExternalMessage.Trim(ex.Message)));
            }

            // Saved per competitor, not once at the end. The enrolment row is what the webhook resolves a push
            // through, so a throw partway used to leave an Active session with repositories on the git host and
            // zero rows in the database — every push then rejected as CompetitorNotFound, with the session
            // looking perfectly live in the UI. Committing each row as it is decided means an interrupted Start
            // leaves exactly the competitors it got to, which is also what makes re-running it a real retry.
            await db.SaveChangesAsync(cancellationToken);

            // Counted whether the repository was created or not: the bar tracks competitors dealt with, and
            // a failure the admin is told about afterwards must not leave it stalled short of the end.
            completed++;
            Report(request, new SessionProvisioningProgress(SessionProvisioningStage.Repositories, completed, total));
        }

        return failures;
    }

    /// <summary>
    /// Creates the session's shared database and gives each competitor's login the configured access to it.
    /// </summary>
    /// <remarks>
    /// The database failing is recorded once, against the database itself, and the grants are abandoned rather
    /// than attempted: every one of them would fail with the same cause, and N copies of one message buries
    /// the repository failures the admin also has to read. The bar is still driven to the end so it does not
    /// sit at zero while the next stage runs.
    /// </remarks>
    private async Task<SessionProvisioningStageOutcome> GrantDatabaseAccessAsync(
        StartSessionCommand request, DatabaseAccessPlan? plan, CancellationToken cancellationToken)
    {
        if (plan is null)
            return EmptyStage;

        var failures = new List<SessionProvisioningFailure>();
        var total = plan.WithLogin.Count;
        var completed = 0;

        Report(request, new SessionProvisioningProgress(SessionProvisioningStage.Databases, completed, total));

        try
        {
            await msSql.EnsureDatabaseAsync(plan.Database, plan.Admin, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not create the session database {Database}", plan.Database);
            failures.Add(new SessionProvisioningFailure(
                SessionProvisioningStage.Databases, plan.Database, ExternalMessage.Trim(ex.Message)));

            Report(request, new SessionProvisioningProgress(SessionProvisioningStage.Databases, total, total));
            return new SessionProvisioningStageOutcome(NothingSucceeded, failures);
        }

        var granted = 0;

        foreach (var competitor in plan.WithLogin)
        {
            try
            {
                await msSql.GrantDatabaseAccessAsync(
                    new MsSqlDatabaseAccessRequest(
                        plan.Database, competitor.Username, plan.Read, plan.Write, plan.Admin),
                    cancellationToken);

                granted++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One competitor's grant failing must not deny the rest theirs.
                logger.LogError(ex,
                    "Could not grant {Username} access to the session database {Database}",
                    competitor.Username, plan.Database);

                failures.Add(new SessionProvisioningFailure(
                    SessionProvisioningStage.Databases, competitor.Username, ExternalMessage.Trim(ex.Message)));
            }

            completed++;
            Report(request, new SessionProvisioningProgress(SessionProvisioningStage.Databases, completed, total));
        }

        return new SessionProvisioningStageOutcome(granted, failures);
    }

    /// <summary>
    /// Brings up the session's long-running service containers, one per configured image.
    /// </summary>
    /// <remarks>
    /// Labelled with the session slug as they are started, because that label is the only thing closing the
    /// session has to find them by — including a service whose image was removed from the session after it
    /// was started. A label the image itself carries is kept, but the session's own two win a collision: they
    /// are what removal depends on.
    /// </remarks>
    private async Task<SessionProvisioningStageOutcome> StartServicesAsync(
        StartSessionCommand request,
        Session session,
        BasicCredential? pullCredential,
        CancellationToken cancellationToken)
    {
        var images = session.DockerImages;
        if (images.Count == 0)
            return EmptyStage;

        var failures = new List<SessionProvisioningFailure>();
        var total = images.Count;
        var running = 0;

        Report(request, new SessionProvisioningProgress(SessionProvisioningStage.DockerServices, NothingSucceeded, total));

        for (var index = 0; index < total; index++)
        {
            var image = images[index];

            // Started on the host daemon over the mounted socket, so a registry host that only exists on the
            // docker network has to be restated. The session's own record of the image is left as it is.
            var daemonImage = DaemonImageReference.ForDaemon(
                image.Image, webhookOptions.Value.GitInternalBaseUrl);

            if (!string.Equals(daemonImage, image.Image, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Session {SessionId} pulls {DaemonImage}; {StoredImage} names a host only the docker "
                    + "network resolves.",
                    session.Id, daemonImage, image.Image);
            }

            var labels = new Dictionary<string, string>(image.Labels)
            {
                [SessionServiceLabels.SessionKey] = session.Slug,
                [SessionServiceLabels.ServiceKey] = SessionServiceNaming.ServiceNumber(index),
            };

            try
            {
                var outcome = await containerServices.EnsureRunningAsync(
                    new ContainerServiceRequest(
                        daemonImage,
                        SessionServiceNaming.ContainerName(session.Slug, index),
                        image.Env,
                        labels,
                        image.Volumes,
                        image.PortMappings,
                        RegistryAuthFactory.ForHostedImage(pullCredential, daemonImage)),
                    cancellationToken);

                logger.LogInformation(
                    "Session {SessionId} service {Image}: {Outcome}", session.Id, image.Image, outcome);

                running++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One service failing must not deny the session the rest of them.
                logger.LogError(ex,
                    "Could not start the service {Image} for session {SessionId}", image.Image, session.Id);

                failures.Add(new SessionProvisioningFailure(
                    SessionProvisioningStage.DockerServices, image.Image, ExternalMessage.Trim(ex.Message)));
            }

            Report(request, new SessionProvisioningProgress(SessionProvisioningStage.DockerServices, index + 1, total));
        }

        return new SessionProvisioningStageOutcome(running, failures);
    }

    /// <summary>
    /// Hands a report to the caller's sink, if it supplied one.
    /// </summary>
    /// <remarks>
    /// The sink belongs to whoever sent the command — on the sessions page it marshals onto the Blazor
    /// renderer — so it can fail for reasons that have nothing to do with the git host: a circuit that has
    /// gone away, a component disposed while provisioning was still running. Losing a progress update is
    /// never worth abandoning a half-provisioned session for, so every sink fault is logged and swallowed.
    /// Cancellation is not read from here either; the next awaited git or database call observes the token.
    /// </remarks>
    private void Report(StartSessionCommand request, SessionProvisioningProgress progress)
    {
        if (request.Progress is null)
            return;

        try
        {
            request.Progress.Report(progress);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A start-session progress report could not be delivered");
        }
    }

    private async Task<BasicCredential?> ResolveAdminCredentialAsync(
        Guid? credentialId, CancellationToken cancellationToken) =>
        credentialId is null ? null : await db.FindByIdAsync(vault, credentialId.Value, cancellationToken);

    /// <summary>256-bit HMAC key, matching what the signature filter verifies with.</summary>
    private const int WebhookSecretBytes = 32;

    /// <summary>Stands in for the database name in the summary log line when the session has none.</summary>
    private const string NoDatabaseConfigured = "(none)";

    private const int NothingSucceeded = 0;

    /// <summary>A stage the session is not configured for: nothing attempted, nothing to report.</summary>
    private static readonly SessionProvisioningStageOutcome EmptyStage = new(NothingSucceeded, []);

    private static readonly TimeSpan TemplateVisibilityTimeout = TimeSpan.FromSeconds(30);
}
