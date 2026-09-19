namespace Skill.Suite.Application.Sessions.CloseSession;

/// <summary>
/// The session is closed; this is what happened to the containers it had running.
/// </summary>
/// <remarks>
/// A docker daemon that cannot be reached must not keep a session open. Closing is a database transition the
/// competition depends on — it is what stops accepting pushes — while a container left behind costs the host
/// some memory and a port, which an admin can clear up by hand once they are told about it. So the removal
/// error is carried here rather than returned as a failure.
/// </remarks>
public sealed record CloseSessionResult(int ServicesRemoved, string? ServiceRemovalError);
