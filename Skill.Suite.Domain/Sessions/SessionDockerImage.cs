namespace Skill.Suite.Domain.Sessions;

/// <summary>
/// One docker service a session runs, as the administrator configured it.
/// </summary>
/// <param name="Domain">
/// Hostname the service is reached on through the reverse proxy, such as <c>shop.skills.local</c>, or null
/// for a service competitors reach on a published host port alone.
/// </param>
/// <remarks>
/// Setting <paramref name="Domain"/> is what turns a service into a routed one: the container joins the
/// proxy's network and is labelled so the proxy sends each competitor to their OWN container by the source
/// address of their workstation. One name for everybody, twenty containers behind it — which is the only way
/// a per-competitor service can be handed out as a URL rather than as twenty different port numbers.
/// </remarks>
public sealed record SessionDockerImage(
    string Image,
    Dictionary<string, string> Env,
    Dictionary<string, string> Labels,
    List<VolumeMount> Volumes,
    List<PortMapping> PortMappings,
    string? Domain);
