namespace Skill.Suite.Application.Abstractions;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// One long-running service container of a session, as the docker daemon needs it described.
/// </summary>
/// <param name="ContainerName">
/// Identifies the service across restarts of this application: it is the only handle
/// <see cref="IContainerServiceManager.EnsureRunningAsync"/> has for deciding whether the service is already
/// up, so it must be derived from the session and the image rather than generated per call.
/// </param>
/// <param name="Labels">
/// Stamped onto the container so the session's services can be found again by label at close, including the
/// ones started by an earlier run of this application.
/// </param>
/// <param name="RegistryAuth">Credentials for the pull, or null for an image the host can already reach.</param>
public sealed record ContainerServiceRequest(
    string Image,
    string ContainerName,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<VolumeMount> Volumes,
    IReadOnlyList<PortMapping> PortMappings,
    RegistryAuth? RegistryAuth);
