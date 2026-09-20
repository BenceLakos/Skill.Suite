namespace Skill.Suite.Application.Sessions.StartMarking;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// Where one marking container is, as the expert about to connect to it needs it written down.
/// </summary>
/// <remarks>
/// Returned rather than left to be worked out from the container names, because the ports are the whole
/// point: a per-competitor service publishes a different host port for every competitor, and an expert with
/// twenty competitors and three services has sixty numbers to get right. The session page prints this list.
/// </remarks>
/// <param name="CompetitorUsername">Whose container this is, or null for a shared service.</param>
public sealed record MarkingEndpoint(
    string? CompetitorUsername,
    string ServiceNumber,
    string Image,
    string ContainerName,
    IReadOnlyList<PortMapping> Ports);
