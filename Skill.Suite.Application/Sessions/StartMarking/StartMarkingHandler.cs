namespace Skill.Suite.Application.Sessions.StartMarking;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Application.Credentials;
using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Starts the marking copy of a closed session's docker services.
/// </summary>
/// <remarks>
/// Deliberately changes nothing in the database. Marking is a view of what the competition left behind — the
/// competitors' databases, their repositories, their enrolments — so the session's status, the enrolment
/// rows and the ordinals the host ports come from are all read and none of them written. That is also what
/// makes this safe to run twice.
/// </remarks>
public sealed class StartMarkingHandler(
    IAppDbContext db,
    IMsSqlAdminClient msSql,
    IContainerServiceManager containerServices,
    IPasswordVault vault,
    IOptions<WebhookOptions> webhookOptions,
    IOptions<MsSqlOptions> msSqlOptions,
    ILogger<StartMarkingHandler> logger)
    : IRequestHandler<StartMarkingCommand, Result<StartMarkingResult>>
{
    public async ValueTask<Result<StartMarkingResult>> Handle(
        StartMarkingCommand request, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .AsNoTracking()
            .Include(s => s.Competitors)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (session is null)
            return SessionErrors.NotFound(request.Id);

        if (session.Status != SessionStatus.Closed)
            return SessionErrors.NotClosedForMarking;

        if (session.DockerImages.Count == 0)
            return SessionErrors.NoServicesToMark;

        var needsDatabase = session.DockerImages.Any(image => ServiceTemplate.Scan(image).NeedsDatabase);

        var database = await ResolveDatabaseAsync(session, needsDatabase, cancellationToken);
        if (database.IsFailure)
            return Result.Failure<StartMarkingResult>(database.Error);

        var pullCredential = await ResolvePullCredentialAsync(session, cancellationToken);
        if (pullCredential.IsFailure)
            return Result.Failure<StartMarkingResult>(pullCredential.Error);

        var plan = SessionServicePlanner.Plan(new SessionServicePlanRequest(
            session,
            SessionRunMode.Marking,
            SessionProvisioningStage.MarkingServices,
            await PlanCompetitorsAsync(session, database.Value, cancellationToken),
            session.DatabaseName,
            ServiceSqlServerName.For(msSqlOptions.Value.Server),
            database.Value?.Admin,
            webhookOptions.Value.GitInternalBaseUrl));

        var run = await SessionServiceRunner.RunAsync(
            containerServices,
            logger,
            plan,
            SessionProvisioningStage.MarkingServices,
            session.Id,
            pullCredential.Value,
            progress => Report(request, progress),
            cancellationToken);

        logger.LogInformation(
            "Session {SessionId} marking started: {Running} containers up, {Failed} failures, " +
            "{Skipped} competitors skipped for having no SQL login",
            session.Id, run.Outcome.Succeeded, run.Outcome.Failures.Count, plan.SkippedNoDatabaseLogin.Count);

        return new StartMarkingResult(
            run.Outcome.Succeeded,
            [.. run.Running.Select(Endpoint)],
            run.Outcome.Failures,
            plan.SkippedNoDatabaseLogin);
    }

    /// <summary>
    /// The SQL Server administrator every database-scoped placeholder resolves to, or null when no service
    /// asks for one.
    /// </summary>
    /// <remarks>
    /// Read up front and refused up front, the same way starting a session plans its database stage: a
    /// missing credential or an unreadable login list makes every database-backed container impossible, and
    /// discovering that after half of them are up turns one clear refusal into N identical failures.
    /// </remarks>
    private async Task<Result<MarkingDatabaseAccess?>> ResolveDatabaseAsync(
        Session session, bool needsDatabase, CancellationToken cancellationToken)
    {
        if (!needsDatabase)
            return Result.Success<MarkingDatabaseAccess?>(null);

        if (string.IsNullOrWhiteSpace(session.DatabaseName))
            return Result.Failure<MarkingDatabaseAccess?>(SessionErrors.MarkingNeedsDatabaseName);

        var admin = await db.FindByKindAsync(vault, CredentialKind.MsSql, cancellationToken);
        if (admin is null)
            return Result.Failure<MarkingDatabaseAccess?>(SessionErrors.MissingDatabaseCredential);

        try
        {
            var inventory = await msSql.GetInventoryAsync(admin, cancellationToken);

            return Result.Success<MarkingDatabaseAccess?>(new MarkingDatabaseAccess(
                admin, inventory.Logins.ToHashSet(StringComparer.OrdinalIgnoreCase)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Could not read the SQL Server logins, so marking was not started for session {SessionId}",
                session.Id);

            return Result.Failure<MarkingDatabaseAccess?>(SessionErrors.DatabaseAccessUnknown);
        }
    }

    /// <summary>
    /// Every competitor enrolled in the session, with the ordinal their marking ports are derived from.
    /// </summary>
    /// <remarks>
    /// Every enrolment, not only the provisioned ones. The repository and the database are separate pieces
    /// of work: a competitor whose repository failed may still have been given a database and worked in it
    /// through a service container, and refusing to mark them because a git call failed weeks ago would lose
    /// exactly the work marking exists to look at.
    /// </remarks>
    private async Task<List<SessionServicePlanCompetitor>> PlanCompetitorsAsync(
        Session session, MarkingDatabaseAccess? database, CancellationToken cancellationToken)
    {
        var ordinals = session.Competitors.ToDictionary(c => c.CompetitorId, c => c.Ordinal);
        var competitorIds = ordinals.Keys.ToList();

        var competitors = await db.Competitors
            .AsNoTracking()
            .Where(c => competitorIds.Contains(c.Id))
            .OrderBy(c => c.Username)
            .ToListAsync(cancellationToken);

        return
        [
            .. competitors.Select(competitor => new SessionServicePlanCompetitor(
                competitor.Username,
                competitor.FullName,
                competitor.IpAddress,
                competitor.CountryCode,
                vault.Unprotect(competitor.EncryptedPassword),
                ordinals[competitor.Id],
                database?.Logins.Contains(competitor.Username) ?? false)),
        ];
    }

    /// <summary>
    /// The credential the marking images are pulled with, resolved the way starting the session resolves it.
    /// </summary>
    private async Task<Result<BasicCredential?>> ResolvePullCredentialAsync(
        Session session, CancellationToken cancellationToken)
    {
        if (session.JudgementImagePullCredentialId is null)
            return Result.Success<BasicCredential?>(null);

        var credential = await db.FindByIdAsync(
            vault, session.JudgementImagePullCredentialId.Value, cancellationToken);

        return credential is null
            ? Result.Failure<BasicCredential?>(SessionErrors.MissingImagePullCredential)
            : Result.Success<BasicCredential?>(credential);
    }

    private static MarkingEndpoint Endpoint(PlannedSessionService service) =>
        new(service.CompetitorUsername,
            service.ServiceNumber,
            service.ConfiguredImage,
            service.ContainerName,
            service.PortMappings);

    /// <summary>
    /// Hands a report to the caller's sink, if it supplied one.
    /// </summary>
    /// <remarks>
    /// Every sink fault is logged and swallowed, for the reason the start handler's own <c>Report</c>
    /// explains: the sink belongs to a Blazor circuit that may already have gone away, and losing a progress
    /// update is never worth abandoning a half-started marking run for.
    /// </remarks>
    private void Report(StartMarkingCommand request, SessionProvisioningProgress progress)
    {
        if (request.Progress is null)
            return;

        try
        {
            request.Progress.Report(progress);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A start-marking progress report could not be delivered");
        }
    }
}
