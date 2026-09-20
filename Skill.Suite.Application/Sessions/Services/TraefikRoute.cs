namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// One container's route through the reverse proxy, as the labels need it described.
/// </summary>
/// <remarks>
/// Kept apart from the label builder so the decisions — which hostname, whose address, which port — are made
/// by the planner, which knows the session, while the builder only knows Traefik's label vocabulary.
/// </remarks>
/// <param name="Host">The hostname the request's <c>Host</c> header has to carry.</param>
/// <param name="ClientIp">
/// The only source address allowed through to this container, or null for a route open to everybody.
/// <para>
/// This is what separates twenty containers sharing one hostname: during the competition it is the
/// competitor's own workstation, and while marking it is the machine the expert is marking from.
/// </para>
/// </param>
/// <param name="ContainerPort">The port inside the container the proxy forwards to.</param>
/// <param name="Network">The docker network the proxy reaches the container on.</param>
internal sealed record TraefikRoute(
    string ContainerName,
    string Host,
    string? ClientIp,
    int ContainerPort,
    string Network);
