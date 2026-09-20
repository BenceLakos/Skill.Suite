namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// What <see cref="IContainerServiceManager.StopAsync"/> found when it went to stop the service.
/// </summary>
/// <remarks>
/// All three are successes — the service is not running afterwards either way — but they say different
/// things to whoever stopped the session: <see cref="NotRunning"/> means it had already died on its own, and
/// <see cref="Absent"/> that the session never had it up, or something removed it outside this application.
/// </remarks>
public enum ContainerServiceStop
{
    Stopped = 0,
    NotRunning = 1,
    Absent = 2,
}
