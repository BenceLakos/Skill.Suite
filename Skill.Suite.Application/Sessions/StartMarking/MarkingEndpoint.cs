namespace Skill.Suite.Application.Sessions.StartMarking;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// Where one marking container is, as the expert about to connect to it needs it written down.
/// </summary>
/// <remarks>
/// Returned rather than left to be worked out from the container names, because the addresses are the whole
/// point: a routed service is reached at its URL and an unrouted one at a host port that differs per
/// competitor, and neither is guessable from the session's configuration alone.
/// </remarks>
/// <param name="Url">
/// The address to open for a routed service, or null for one reached on a published port. It only answers
/// from the machine whose address this marking run was started with.
/// </param>
public sealed record MarkingEndpoint(
    string ServiceNumber,
    string Image,
    string ContainerName,
    string? Url,
    IReadOnlyList<PortMapping> Ports);
