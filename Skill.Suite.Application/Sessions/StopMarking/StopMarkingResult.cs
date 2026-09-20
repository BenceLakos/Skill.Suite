namespace Skill.Suite.Application.Sessions.StopMarking;

/// <summary>
/// How many marking containers were removed, and the daemon's complaint if it would not.
/// </summary>
/// <remarks>
/// The error is carried rather than returned as a failure, the same way closing a session carries its
/// teardown error: a container left behind costs the host some memory and a port, which the admin can clear
/// up once they are told about it, and turning that into a failed command would only hide the count of what
/// did get removed.
/// </remarks>
public sealed record StopMarkingResult(int ServicesRemoved, string? ServiceRemovalError);
