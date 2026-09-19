namespace Skill.Suite.Infra.Containers;

/// <summary>
/// A docker operation on a session's service container failed. The message carries the daemon's own stderr,
/// because that is the only thing that says why — a port already taken, a missing image, a bad mount path —
/// and it is what an admin has to act on.
/// </summary>
public sealed class ContainerServiceException(string message) : Exception(message);
