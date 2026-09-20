namespace Skill.Suite.Domain.Sessions;

/// <summary>
/// One docker service a session runs, as the administrator configured it.
/// </summary>
/// <param name="Domain">
/// Hostname the service is reached on through the reverse proxy, such as <c>shop.skills.local</c>, or null
/// for a service competitors reach on a published host port alone.
/// </param>
/// <param name="RoutedPort">
/// The port the application inside the container listens on, which is what the proxy forwards to. Required
/// when <paramref name="Domain"/> is set and unset otherwise.
/// </param>
/// <remarks>
/// Setting <paramref name="Domain"/> is what turns a service into a routed one: the container joins the
/// proxy's network and is labelled so the proxy sends each competitor to their OWN container by the source
/// address of their workstation. One name for everybody, twenty containers behind it — which is the only way
/// a per-competitor service can be handed out as a URL rather than as twenty different port numbers.
/// <para>
/// Routing needs no port mapping. The proxy reaches the container over the shared docker network and talks
/// to <paramref name="RoutedPort"/> directly, so nothing has to be published on the host at all — which is
/// the better shape for a routed service, since a published port is a second address that reaches the same
/// container without going through the proxy. <see cref="PortMappings"/> stays optional and independent: an
/// administrator who also wants a host port still gets one, routed or not.
/// </para>
/// </remarks>
public sealed record SessionDockerImage(
    string Image,
    Dictionary<string, string> Env,
    Dictionary<string, string> Labels,
    List<VolumeMount> Volumes,
    List<PortMapping> PortMappings,
    string? Domain,
    int? RoutedPort);
