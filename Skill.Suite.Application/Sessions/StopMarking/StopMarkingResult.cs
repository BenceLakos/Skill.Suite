namespace Skill.Suite.Application.Sessions.StopMarking;

/// <summary>
/// How many marking containers were removed, and the daemon's complaint if it would not.
/// </summary>
/// <remarks>
/// The error is carried rather than returned as a failure, the same way closing a session carries its
/// teardown error: a container left behind costs the host some memory, a port and a proxy route, which the
/// admin can clear up once they are told about it, and turning that into a failed command would only hide
/// the count of what did get removed.
/// </remarks>
/// <param name="CompetitorUsername">Whose marking was stopped, or null when the whole session's was.</param>
public sealed record StopMarkingResult(
    string? CompetitorUsername, int ServicesRemoved, string? ServiceRemovalError);
