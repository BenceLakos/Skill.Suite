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
/// Starts the marking copy of one competitor's docker services on a closed session.
/// </summary>
/// <remarks>
/// Deliberately changes nothing in the database. Marking is a view of what the competition left behind — the
/// competitor's database, their repository, their enrolment — so the session's status, the enrolment rows
/// and the ordinal the host ports come from are all read and none of them written. That is also what makes
/// this safe to run twice.
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

        var enrolment = session.Competitors.FirstOrDefault(c => c.CompetitorId == request.CompetitorId);
        if (enrolment is null)
            return SessionErrors.CompetitorNotEnrolled;

        var competitor = await db.Competitors
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CompetitorId, cancellationToken);

        if (competitor is null)
            return SessionErrors.CompetitorNotEnrolled;

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
            [
                new SessionServicePlanCompetitor(
                    competitor.Username,
                    competitor.FullName,
                    competitor.IpAddress,
                    competitor.CountryCode,
                    vault.Unprotect(competitor.EncryptedPassword),
                    enrolment.Ordinal,
                    database.Value?.Logins.Contains(competitor.Username) ?? false),
            ],
            session.DatabaseName,
            ServiceSqlServerName.For(msSqlOptions.Value.Server),
            database.Value?.Admin,
            webhookOptions.Value.GitInternalBaseUrl,
            webhookOptions.Value.ServiceNetwork,
            request.MarkingIpAddress));

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
            "Session {SessionId}: marking {Username} from {MarkingIp} — {Running} containers up, " +
            "{Failed} failures, database services skipped: {Skipped}",
            session.Id, competitor.Username, request.MarkingIpAddress,
            run.Outcome.Succeeded, run.Outcome.Failures.Count, plan.SkippedNoDatabaseLogin.Count > 0);

        return new StartMarkingResult(
            competitor.Username,
            request.MarkingIpAddress,
            run.Outcome.Succeeded,
            [.. run.Running.Select(Endpoint)],
            run.Outcome.Failures,
            plan.SkippedNoDatabaseLogin.Count > 0);
    }

    /// <summary>
    /// The SQL Server administrator every database-scoped placeholder resolves to, or null when no service
    /// asks for one.
    /// </summary>
    /// <remarks>
    /// Read up front and refused up front, the same way starting a session plans its database stage: a
    /// missing credential or an unreadable login list makes every database-backed container impossible, and
    /// discovering that after half of them are up turns one clear refusal into several identical failures.
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

    /// <summary>
    /// One running container as an address, with no request host to read the proxy's published port from.
    /// </summary>
    /// <remarks>
    /// The admin page that shows this is reached through the same proxy, so its own address carries the
    /// port — but the command does not receive it, and inventing a setting for it would be a second place
    /// for the deployment's port to be stated wrongly. A bare <c>http://host</c> is right whenever the proxy
    /// is published on 80, which is the default.
    /// </remarks>
    private static MarkingEndpoint Endpoint(PlannedSessionService service) =>
        new(service.ServiceNumber,
            service.ConfiguredImage,
            service.ContainerName,
            ServiceUrl.For(service.Host, requestHost: null),
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
