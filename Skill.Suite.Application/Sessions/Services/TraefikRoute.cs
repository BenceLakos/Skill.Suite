namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// One container's route through the reverse proxy, as the labels need it described.
/// </summary>
/// <remarks>
/// Kept apart from the label builder so the decisions — which hostname, whose address, which port — are made
/// by the planner, which knows the session, while the builder only knows Traefik's label vocabulary.
/// </remarks>
/// <param name="Host">The hostname the request's <c>Host</c> header has to carry.</param>
/// <param name="ClientIps">
/// The source addresses allowed through to this container. Empty means a route open to everybody.
/// <para>
/// This is what separates twenty containers sharing one hostname: during the competition these are the
/// competitor's own machines, and while marking they are the expert's machine and — so the competitor's
/// phone can still be pointed at the container being marked — that competitor's mobile device.
/// </para>
/// <para>
/// A list rather than one address because Traefik v3's <c>ClientIP</c> matcher takes exactly one value; two
/// devices are two matchers joined by <c>||</c>.
/// </para>
/// </param>
/// <param name="ContainerPort">The port inside the container the proxy forwards to.</param>
/// <param name="Network">The docker network the proxy reaches the container on.</param>
internal sealed record TraefikRoute(
    string ContainerName,
    string Host,
    IReadOnlyList<string> ClientIps,
    int ContainerPort,
    string Network);
