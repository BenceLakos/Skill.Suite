namespace Skill.Suite.Application.Sessions.MySession;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions.Services;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;
using Skill.Suite.Domain.Sessions;

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

        // Read off the session directly rather than planned. A domain is stored literally — ServiceTemplate
        // renders environment values, label values and volume host paths, never the domain — so the name a
        // competitor is given is the one the administrator typed, and planning every container to find that
        // out would buy nothing now that the name is all this page shows.
        var services = session.DockerImages
            .Where(image => IsReachableBy(image, sessionDatabase is not null))
            .Select(image => new MySessionServiceDto(image.Domain!, ServiceUrl.For(image.Domain)!))
            .ToList();

        return new MySessionOverviewDto(
            credentials,
            new MySessionDetailsDto(
                session.Name,
                session.Slug,
                session.StartsAt,
                session.EndsAt,
                session.Status,
                // Rewritten for the competitor's machine: the stored value is the name the git server
                // calls itself over the docker network, which their laptop cannot resolve.
                CompetitorGitUrl.For(enrolment.RepositoryUrl, host),
                enrolment.ProvisionStatus,
                database,
                services),
            NotEnrolled: false);
    }

    /// <summary>
    /// Whether this competitor actually has a container answering on the service's domain.
    /// </summary>
    /// <remarks>
    /// The same two conditions the planner routes on — a domain, and the port behind it that the proxy
    /// forwards to — plus the one subtraction it makes: a service whose settings mention a database
    /// placeholder is never started for a competitor with no session database, and printing its address
    /// would send them to a name that answers for nobody.
    /// <para>
    /// "Has a session database" stands in for "holds a SQL login", which is as close as this page can get
    /// without an outbound connection on its render path — and the database card above already says which
    /// one it is.
    /// </para>
    /// </remarks>
    private static bool IsReachableBy(SessionDockerImage image, bool hasSessionDatabase) =>
        !string.IsNullOrWhiteSpace(image.Domain)
        && image.RoutedPort is not null
        && (hasSessionDatabase || !ServiceTemplate.Scan(image).NeedsDatabase);

    /// <summary>Nothing is running, or the session that is has since disappeared.</summary>
    private static MySessionOverviewDto NoSession(MySessionCredentialsDto credentials) =>
        new(credentials, null, NotEnrolled: false);

    private static string? NormalizeHost(string? host) =>
        string.IsNullOrWhiteSpace(host) ? null : host.Trim();
}
