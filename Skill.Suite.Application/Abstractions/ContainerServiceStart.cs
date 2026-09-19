namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// What <see cref="IContainerServiceManager.EnsureRunningAsync"/> had to do to satisfy the request.
/// </summary>
/// <remarks>
/// Reported rather than swallowed because the three outcomes mean different things to whoever is watching a
/// session start: <see cref="AlreadyRunning"/> is a no-op, while <see cref="Recreated"/> says a service that
/// was supposed to stay up had died and lost whatever state it held outside its volumes.
/// </remarks>
public enum ContainerServiceStart
{
    Started = 0,
    AlreadyRunning = 1,
    Recreated = 2,
}
