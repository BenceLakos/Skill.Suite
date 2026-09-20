namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// Every container a session's images resolve to, plus what could not be planned and who was left out.
/// </summary>
/// <remarks>
/// Failures come out of planning rather than out of running because some of them are arithmetic: a host port
/// that leaves the port range for the twentieth competitor is known before the daemon is spoken to, and is
/// recorded against that competitor so the other nineteen still get their container.
/// </remarks>
/// <param name="SkippedNoDatabaseLogin">
/// Competitors a database-scoped service was NOT started for, because the SQL Server holds no login for them
/// and so no session database of theirs exists. Not a failure: nothing was attempted, and pointing a service
/// at a database that was never created would fail later and say less.
/// </param>
internal sealed record SessionServicePlan(
    IReadOnlyList<PlannedSessionService> Services,
    IReadOnlyList<SessionProvisioningFailure> Failures,
    IReadOnlyList<string> SkippedNoDatabaseLogin);
