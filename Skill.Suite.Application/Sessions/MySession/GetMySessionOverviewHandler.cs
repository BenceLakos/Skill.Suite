namespace Skill.Suite.Application.Sessions.MySession;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
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
    ICurrentUserService currentUser)
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
        // theirs here to describe. Everything below — the session's name, its shared database, the services'
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

        var database = new MySessionDatabaseDto(
            server,
            competitor.Username,
            credentials.Password,
            competitor.Username,
            MsSqlConnectionString.For(
                server, competitor.Username, competitor.Username, credentials.Password),
            session.DatabaseName,
            session.DatabaseReadAccess,
            session.DatabaseWriteAccess,
            hasSessionDatabaseAccess
                ? MsSqlConnectionString.For(
                    server, session.DatabaseName, competitor.Username, credentials.Password)
                : null);

        // Numbered through the same helper the container name and the docker label use, so the service a
        // competitor reports a problem with is the one an admin finds in `docker ps`.
        var services = session.DockerImages
            .Select((image, index) => new MySessionServiceDto(
                SessionServiceNaming.ServiceNumber(index),
                image.Image,
                SessionServiceNaming.ContainerName(session.Slug, index),
                image.Env,
                image.PortMappings))
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
