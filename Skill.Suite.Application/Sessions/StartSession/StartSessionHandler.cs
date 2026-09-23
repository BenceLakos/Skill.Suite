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
using Skill.Suite.Application.Sessions.Services;
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
    IStarterPackageStore starterPackages,
    IContainerServiceManager containerServices,
    IPasswordVault vault,
    IOptions<WebhookOptions> webhookOptions,
    IOptions<MsSqlOptions> msSqlOptions,
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

        var databases = await ProvisionCompetitorDatabasesAsync(request, databasePlan.Value, cancellationToken);
        failures.AddRange(databases.Failures);

        var services = await StartServicesAsync(
            request, session, provisionable, databasePlan.Value, pullCredential.Value, cancellationToken);

        failures.AddRange(services.Failures);

        // The webhook goes on LAST, deliberately. An organisation hook fires for the pushes provisioning
        // itself performs, so installing it first produces a judgement run per competitor at start - each
        // attributed to nothing, since the template repository is not a competitor.
        //
        // And only for a session that is judged at all - see SessionWebhookReconciler, which also removes
        // the hook a session that has since had its judgement image cleared was left holding.
        //
        // The secret above is generated and persisted either way, deliberately. It costs 32 bytes, it keeps
        // Start idempotent in both directions - adding an image later and starting again installs the hook
        // with the key this session has always had, rather than one the domain would have to be asked for
        // separately - and Session.Start stays a transition with a single shape.
        await SessionWebhookReconciler.ReconcileAsync(
            gitHost,
            logger,
            session,
            webhookOptions.Value.PublicWebhookUrl,
            DefaultBranch,
            secret,
            admin,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var provisioned = session.Competitors.Count(c => c.ProvisionStatus == SessionProvisionStatus.Provisioned);
        var skippedNoDatabaseLogin = databasePlan.Value?.SkippedNoDatabaseLogin ?? [];

        logger.LogInformation(
            "Session {SessionId} started: {Provisioned} repositories provisioned in {Org}, " +
            "{Granted} competitor databases under the base name {Database}, " +
            "{Services} service containers running from {ConfiguredServices} configured services, " +
            "{Failed} failures, {SkippedGit} skipped for having no git host account, " +
            "{SkippedDatabase} skipped for having no SQL login",
            session.Id, provisioned, organization,
            databases.Succeeded, databasePlan.Value?.BaseName ?? NoDatabaseConfigured,
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
    /// What the database stage will do, or null when the session configures no database at all.
    /// </summary>
    /// <remarks>
    /// Partitions the competitors who are getting a repository, not every competitor in the database: a
    /// competitor provisioning is skipping is not in this session at all, so reporting them as missing a SQL
    /// login would be noise about somebody who was never going to connect.
    /// <para>
    /// Nobody holding a login is not an error, unlike the git side. There is simply nothing to create: every
    /// database this stage makes belongs to one competitor, so a session where none of them has a login ends
    /// with no databases and a skipped list that says exactly why.
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

        // Read again here rather than carried over from preflight, which refuses rather than produces: the
        // file is a few kilobytes on a mounted volume, and the copy that runs is the one read last.
        var seed = await ReadSeedScriptAsync(session, cancellationToken);
        if (seed.IsFailure)
            return Result.Failure<DatabaseAccessPlan?>(seed.Error);

        var partition = AccountAccessPartitioner.Partition(provisionable, inventory.Logins);

        return Result.Success<DatabaseAccessPlan?>(new DatabaseAccessPlan(
            session.DatabaseName,
            session.DatabaseReadAccess,
            session.DatabaseWriteAccess,
            admin,
            seed.Value,
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

        // Read while nothing has been created yet, and discarded: a seed script that is not on the volume any
        // more is a mistake to report now, not after twenty repositories exist and the database is waiting
        // for a script that cannot be loaded.
        var seed = await ReadSeedScriptAsync(session, cancellationToken);
        if (seed.IsFailure)
            return Result.Failure(seed.Error);

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
    /// Loads the session's database seed script off the starter packages volume, if it has one.
    /// </summary>
    /// <remarks>
    /// A missing file is a refusal rather than a warning. The script is how the competitors' database comes
    /// to have the tables and rows the test project is written against, so starting without it hands everyone
    /// an empty database and a task nobody can complete — which only shows up once the competition has begun.
    /// </remarks>
    private async Task<Result<SeedScript?>> ReadSeedScriptAsync(
        Session session, CancellationToken cancellationToken)
    {
        var path = session.DatabaseSeedScript;

        if (string.IsNullOrWhiteSpace(path))
            return Result.Success<SeedScript?>(null);

        // Only reachable for a session saved before the field existed or edited around the validators: there
        // is no database to run the script against, and running it against another session's would be worse
        // than refusing.
        if (string.IsNullOrWhiteSpace(session.DatabaseName))
            return Result.Failure<SeedScript?>(SessionErrors.SeedScriptWithoutDatabase);

        var script = await starterPackages.ReadTextAsync(path, cancellationToken);

        return script.IsFailure
            ? Result.Failure<SeedScript?>(SessionErrors.SeedScriptUnreadable(path, script.Error.Message))
            : Result.Success<SeedScript?>(new SeedScript(path, script.Value));
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
    /// Gives every competitor who holds a SQL login a database of their own, seeded and granted.
    /// </summary>
    /// <remarks>
    /// One database per competitor, named <c>{base}-{username}</c>, and not one shared between them. A
    /// competitor's database is theirs alone: only their login is granted anything on it, so a task that
    /// drops a table, fills it with test rows or leaves a transaction open costs that competitor and nobody
    /// else. The session's "database name" is therefore a base name — it names no database on the server.
    /// <para>
    /// Every competitor is an independent unit of work, all the way down. A name that cannot be created, a
    /// seed script that fails and a grant that is refused are all recorded against that one competitor and
    /// the loop carries on, because the alternative — one competitor's broken database denying the other
    /// nineteen theirs — is the failure this whole stage exists to avoid.
    /// </para>
    /// </remarks>
    private async Task<SessionProvisioningStageOutcome> ProvisionCompetitorDatabasesAsync(
        StartSessionCommand request, DatabaseAccessPlan? plan, CancellationToken cancellationToken)
    {
        if (plan is null)
            return EmptyStage;

        var failures = new List<SessionProvisioningFailure>();
        var total = plan.WithLogin.Count;
        var completed = 0;
        var granted = 0;

        Report(request, new SessionProvisioningProgress(SessionProvisioningStage.Databases, completed, total));

        foreach (var competitor in plan.WithLogin)
        {
            var outcome = await ProvisionDatabaseForAsync(plan, competitor.Username, cancellationToken);

            if (outcome.Granted)
                granted++;

            failures.AddRange(outcome.Failures);

            // Counted whether the competitor got their database or not: the bar tracks competitors dealt
            // with, so a failure reported afterwards must not leave it stalled short of the end.
            completed++;
            Report(request, new SessionProvisioningProgress(SessionProvisioningStage.Databases, completed, total));
        }

        return new SessionProvisioningStageOutcome(granted, failures);
    }

    /// <summary>
    /// Creates one competitor's database, seeds it if this run is what created it, and grants them access.
    /// </summary>
    /// <remarks>
    /// In that order, and the order matters. Seeding before the grant means the competitor never sees a
    /// half-built schema; granting after a failed seed means they can still connect to whatever the script
    /// did manage, which is something they and an expert can work with, unlike a database they are locked
    /// out of.
    /// </remarks>
    private async Task<CompetitorDatabaseOutcome> ProvisionDatabaseForAsync(
        DatabaseAccessPlan plan, string username, CancellationToken cancellationToken)
    {
        var failures = new List<SessionProvisioningFailure>();
        var database = SessionDatabaseNaming.For(plan.BaseName, username);

        // Checked before the server is asked, because the server's answer to an over-long identifier says
        // nothing about which of the two halves the administrator has to shorten.
        if (!SessionDatabaseNaming.FitsAnIdentifier(plan.BaseName, username))
        {
            logger.LogError(
                "The database name {Database} for {Username} is longer than SQL Server allows, so no database "
                + "was created for them", database, username);

            failures.Add(new SessionProvisioningFailure(
                SessionProvisioningStage.Databases,
                username,
                $"'{database}' is longer than the {SessionDatabaseNaming.MaxIdentifierLength} characters SQL "
                + "Server allows for a database name. Shorten the session's database name."));

            return new CompetitorDatabaseOutcome(false, failures);
        }

        AccountProvisioning provisioning;

        try
        {
            provisioning = await msSql.EnsureDatabaseAsync(database, plan.Admin, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Could not create the database {Database} for {Username}", database, username);

            failures.Add(new SessionProvisioningFailure(
                SessionProvisioningStage.Databases, username, ExternalMessage.Trim(ex.Message)));

            return new CompetitorDatabaseOutcome(false, failures);
        }

        var seedFailure = await SeedDatabaseAsync(plan, username, database, provisioning, cancellationToken);
        if (seedFailure is not null)
            failures.Add(seedFailure);

        try
        {
            await msSql.GrantDatabaseAccessAsync(
                new MsSqlDatabaseAccessRequest(database, username, plan.Read, plan.Write, plan.Admin),
                cancellationToken);

            return new CompetitorDatabaseOutcome(true, failures);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One competitor's grant failing must not deny the rest theirs.
            logger.LogError(ex,
                "Could not grant {Username} access to their database {Database}", username, database);

            failures.Add(new SessionProvisioningFailure(
                SessionProvisioningStage.Databases, username, ExternalMessage.Trim(ex.Message)));

            return new CompetitorDatabaseOutcome(false, failures);
        }
    }

    /// <summary>
    /// Runs the session's seed script against one competitor's database, if this run is what created it.
    /// </summary>
    /// <remarks>
    /// Re-running Start is the documented repair for a half-finished provision, and the seed script is the one
    /// step that cannot be repeated safely: by the second run the competitor may already be working in their
    /// database, and replaying an author's script over their work either fails on the objects it created the
    /// first time or deletes what they have done since. "The database already existed" is therefore read as
    /// "they may already be in it", and the script is skipped — loudly, because an administrator who edited
    /// the script and pressed Start again has every reason to expect it to have run.
    /// <para>
    /// The decision is per competitor, which is what makes a re-run useful: a competitor whose database
    /// failed to be created the first time gets a freshly created and freshly seeded one now, while
    /// everybody already working keeps theirs untouched.
    /// </para>
    /// </remarks>
    private async Task<SessionProvisioningFailure?> SeedDatabaseAsync(
        DatabaseAccessPlan plan,
        string username,
        string database,
        AccountProvisioning provisioning,
        CancellationToken cancellationToken)
    {
        if (plan.Seed is null)
            return null;

        if (provisioning != AccountProvisioning.Created)
        {
            logger.LogInformation(
                "The database {Database} already existed, so the seed script {Script} was NOT run for "
                + "{Username}. Drop that database and start the session again to seed it from scratch.",
                database, plan.Seed.Path, username);

            return null;
        }

        try
        {
            await msSql.ExecuteScriptAsync(database, plan.Seed.Sql, plan.Admin, cancellationToken);

            logger.LogInformation(
                "Seeded the new database {Database} for {Username} from {Script}",
                database, username, plan.Seed.Path);

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Recorded against the competitor rather than the script: one competitor's database is what is
            // broken, the other nineteen may well have been seeded from the same file without complaint.
            logger.LogError(ex,
                "Could not run the seed script {Script} against {Database} for {Username}",
                plan.Seed.Path, database, username);

            return new SessionProvisioningFailure(
                SessionProvisioningStage.Databases,
                username,
                $"{plan.Seed.Path}: {ExternalMessage.Trim(ex.Message)}");
        }
    }

    /// <summary>
    /// Brings up the session's long-running service containers: one per configured image, or one per
    /// competitor for an image whose configuration names them.
    /// </summary>
    /// <remarks>
    /// Labelled with the session slug as they are started, because that label is the only thing closing the
    /// session has to find them by — including a service whose image was removed from the session after it
    /// was started. A label the image itself carries is kept, but the session's own win a collision: they
    /// are what removal depends on.
    /// <para>
    /// How many containers each image becomes is <see cref="SessionServicePlanner"/>'s decision and nothing
    /// is decided here, so stopping and marking cannot disagree with starting about the shape of a service.
    /// The progress bar therefore counts containers rather than images: with twenty competitors, an image
    /// that names one of them is twenty units of work, and counting it as one leaves the bar stuck at a
    /// third while the daemon is busy for a minute.
    /// </para>
    /// </remarks>
    private async Task<SessionProvisioningStageOutcome> StartServicesAsync(
        StartSessionCommand request,
        Session session,
        IReadOnlyList<Competitor> provisionable,
        DatabaseAccessPlan? databasePlan,
        BasicCredential? pullCredential,
        CancellationToken cancellationToken)
    {
        if (session.DockerImages.Count == 0)
            return EmptyStage;

        var plan = SessionServicePlanner.Plan(new SessionServicePlanRequest(
            session,
            SessionRunMode.Competition,
            SessionProvisioningStage.DockerServices,
            PlanCompetitors(session, provisionable, databasePlan),
            databasePlan?.BaseName,
            ServiceSqlServerName.For(msSqlOptions.Value.Server),
            databasePlan?.Admin,
            webhookOptions.Value.GitInternalBaseUrl,
            webhookOptions.Value.ServiceNetwork,
            // Competition containers are separated by the competitor's own workstation address; no marker is
            // involved, so there is no address to be told.
            MarkingIpAddress: null));

        // Already reported by the database stage under the same names, so this is logged rather than
        // returned: the same competitors, said twice, reads as two different problems.
        if (plan.SkippedNoDatabaseLogin.Count > 0)
        {
            logger.LogInformation(
                "Session {SessionId}: {Count} competitors get no database-backed service container because "
                + "the SQL Server holds no login for them: {Usernames}",
                session.Id, plan.SkippedNoDatabaseLogin.Count, string.Join(", ", plan.SkippedNoDatabaseLogin));
        }

        var run = await SessionServiceRunner.RunAsync(
            containerServices,
            logger,
            plan,
            SessionProvisioningStage.DockerServices,
            session.Id,
            pullCredential,
            progress => Report(request, progress),
            cancellationToken);

        return run.Outcome;
    }

    /// <summary>
    /// The competitors a service may be started for, with the two values the planner cannot look up itself.
    /// </summary>
    /// <remarks>
    /// Only competitors who have an enrolment row, which by this point is every provisionable one: the
    /// repository stage enrols them and saves each row as it goes, so the ordinal a container's host ports
    /// are derived from exists before this stage runs. A competitor the repository stage never reached has
    /// no ordinal, and a container whose ports were guessed would collide with somebody's.
    /// </remarks>
    private List<SessionServicePlanCompetitor> PlanCompetitors(
        Session session, IReadOnlyList<Competitor> provisionable, DatabaseAccessPlan? databasePlan)
    {
        var ordinals = session.Competitors.ToDictionary(c => c.CompetitorId, c => c.Ordinal);

        var withLogin = databasePlan is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : databasePlan.WithLogin.Select(c => c.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. provisionable
                .Where(competitor => ordinals.ContainsKey(competitor.Id))
                .Select(competitor => new SessionServicePlanCompetitor(
                    competitor.Username,
                    competitor.FullName,
                    competitor.IpAddress,
                    competitor.MobileIpAddress,
                    competitor.CountryCode,
                    vault.Unprotect(competitor.EncryptedPassword),
                    ordinals[competitor.Id],
                    withLogin.Contains(competitor.Username))),
        ];
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
