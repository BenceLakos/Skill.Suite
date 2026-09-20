namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// One competitor, flattened to exactly what planning a service container needs of them.
/// </summary>
/// <remarks>
/// A record of its own rather than the <c>Competitor</c> entity, because two of the five values are not on
/// it: the password has to be unprotected by the caller, and the ordinal lives on the session enrolment. A
/// planner reaching for a vault or a <c>DbContext</c> to fetch them would stop being the pure, assertable
/// thing that decides how many containers a session starts.
/// </remarks>
/// <param name="Password">The competitor's own password, already unprotected.</param>
/// <param name="Ordinal">
/// Their stable position in the session, which is what shifts their copy of the service off everybody else's
/// host ports. Assigned at enrolment and never reused, so restarting a session — or enrolling somebody a day
/// later — leaves the ports every other competitor has already written down alone.
/// </param>
/// <param name="HasDatabaseLogin">
/// Whether the SQL Server holds a login for them, which is what decides whether a session database was ever
/// created for them, and therefore whether a database-scoped service has anything to be pointed at.
/// </param>
internal sealed record SessionServicePlanCompetitor(
    string Username,
    string FullName,
    string IpAddress,
    /// <summary>Their second device, or null when they have none.</summary>
    string? MobileIpAddress,
    string CountryCode,
    string Password,
    int Ordinal,
    bool HasDatabaseLogin);
