namespace Skill.Suite.Application.Sessions.MySession;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;

/// <summary>
/// Assembles the one page a competitor is given about their own session.
/// </summary>
/// <remarks>
/// The competitor is the signed-in user: account provisioning creates the Identity user with the competitor's
/// own id, which is what every other competitor-scoped query relies on as well.
/// <para>
/// Scoped to the active session, like the competitor's test-run queries are, so a closed session's services
/// and database are not still being offered to connect to the day after.
/// </para>
/// </remarks>
public sealed class GetMySessionOverviewHandler(
    IAppDbContext db,
    IPasswordVault vault,
    ICurrentUserService currentUser,
    IOptions<MsSqlOptions> msSqlOptions,
    IOptions<WebhookOptions> webhookOptions)
    : IRequestHandler<GetMySessionOverviewQuery, Result<MySessionOverviewDto>>
{
    public async ValueTask<Result<MySessionOverviewDto>> Handle(
        GetMySessionOverviewQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } competitorId)
            return Result.Failure<MySessionOverviewDto>(
                Error.Unauthorized("MySession.Unauthorized", "You must be signed in to view your session."));

        var competitor = await db.Competitors
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == competitorId, cancellationToken);

        if (competitor is null)
            return CompetitorErrors.NotFound(competitorId);

        var credentials = new MySessionCredentialsDto(
            competitor.Username, vault.Unprotect(competitor.EncryptedPassword));

        var activeSessionId = await ActiveSessionResolver.ResolveAsync(db, cancellationToken);
        if (activeSessionId is null)
            return NoSession(credentials);

        var session = await db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == activeSessionId.Value, cancellationToken);

        if (session is null)
            return NoSession(credentials);

        var enrolment = await db.SessionCompetitors
            .AsNoTracking()
            .FirstOrDefaultAsync(
                sc => sc.SessionId == session.Id && sc.CompetitorId == competitorId, cancellationToken);

        // The enrolment row is what makes someone part of a session, and without one there is nothing of
        // theirs here to describe. Everything below — the session's name, their session database, the services'
        // addresses and their environment — belongs to the competitors taking part, so it is withheld rather
        // than shown to a competitor the session was never started for.
        if (enrolment is null)
            return new MySessionOverviewDto(credentials, null, NotEnrolled: true);

        var host = NormalizeHost(request.RequestHost);
        var server = CompetitorSqlServerName.For(host);

        // A connection string for a database the login may neither read nor write is an invitation to spend
        // competition time on a connection that opens and then refuses every statement. The database name and
        // the access flags are still shown, so it is clear WHY there is nothing to paste.
        var hasSessionDatabaseAccess = session.DatabaseReadAccess || session.DatabaseWriteAccess;

        // Derived through the same helper that provisioning names the database with, so what the competitor
        // pastes is what was actually created for them — and nobody else's database is nameable from here.
        var sessionDatabase = string.IsNullOrWhiteSpace(session.DatabaseName)
            ? null
            : SessionDatabaseNaming.For(session.DatabaseName, competitor.Username);

        var database = new MySessionDatabaseDto(
            server,
            competitor.Username,
            credentials.Password,
            sessionDatabase,
            session.DatabaseReadAccess,
            session.DatabaseWriteAccess,
            hasSessionDatabaseAccess
                ? MsSqlConnectionString.For(
                    server, sessionDatabase, competitor.Username, credentials.Password)
                : null);

        // Planned through the very code that started the containers, with this competitor as the only
        // competitor in the session, so what the page shows is what their container actually got: the same
        // name, the same resolved environment and the same host ports. Describing the images directly would
        // describe nobody's container, since every service is one container per competitor.
        //
        // "Has a SQL login" is answered with "the session has a database", which is as close as this page can
        // honestly get without an outbound connection on its render path. A competitor who holds no login has
        // no session database either, and the database card above already says so.
        var plan = SessionServicePlanner.Plan(new SessionServicePlanRequest(
            session,
            SessionRunMode.Competition,
            SessionProvisioningStage.DockerServices,
            [
                new SessionServicePlanCompetitor(
                    competitor.Username,
                    competitor.FullName,
                    competitor.IpAddress,
                    competitor.CountryCode,
                    credentials.Password,
                    enrolment.Ordinal,
                    sessionDatabase is not null),
            ],
            session.DatabaseName,
            ServiceSqlServerName.For(msSqlOptions.Value.Server),
            DatabaseAdmin: null,
            // Null on purpose: this page prints the reference the session stores, which is what the
            // administrator typed, not the one the host daemon was handed to pull with.
            GitInternalBaseUrl: null,
            webhookOptions.Value.ServiceNetwork,
            // No marker is involved: this describes the competition containers, which are separated by the
            // competitor's own workstation address.
            MarkingIpAddress: null));

        var services = plan.Services
            .Select(service => new MySessionServiceDto(
                service.ServiceNumber,
                service.ConfiguredImage,
                service.ContainerName,
                service.Environment,
                service.PortMappings,
                // The proxy listens on 80 inside its container and the stack publishes it on a port this
                // application has no setting for — so it is read off the host this very page was reached on,
                // which came through that proxy on that port.
                ServiceUrl.For(service.Host, host)))
            .ToList();

        return new MySessionOverviewDto(
            credentials,
            new MySessionDetailsDto(
                session.Name,
                session.Slug,
                session.StartsAt,
                session.EndsAt,
                session.Status,
                enrolment.RepositoryUrl,
                enrolment.ProvisionStatus,
                host,
                database,
                services),
            NotEnrolled: false);
    }

    /// <summary>Nothing is running, or the session that is has since disappeared.</summary>
    private static MySessionOverviewDto NoSession(MySessionCredentialsDto credentials) =>
        new(credentials, null, NotEnrolled: false);

    private static string? NormalizeHost(string? host) =>
        string.IsNullOrWhiteSpace(host) ? null : host.Trim();
}
