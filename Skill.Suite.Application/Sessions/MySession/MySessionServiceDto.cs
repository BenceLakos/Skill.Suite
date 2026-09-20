namespace Skill.Suite.Application.Sessions.MySession;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// One service container the session runs, as a competitor needs to address it.
/// </summary>
/// <remarks>
/// There is one of these per session, not one per competitor: the ports it publishes are fixed host ports, so
/// a second copy would collide on them. Everyone in the session connects to the same instance, which the page
/// says out loud.
/// <para>
/// Volumes and labels are deliberately absent. They are host paths and bookkeeping for the docker daemon —
/// nothing a competitor can act on, and the host paths describe the competition machine's filesystem.
/// Environment variables ARE included: they are what the session author puts there for competitors to connect
/// with, which makes that surface the author's to decide.
/// </para>
/// </remarks>
/// <param name="Number">The service's position as the admin sees it listed, and as its docker label carries it.</param>
public sealed record MySessionServiceDto(
    string Number,
    string Image,
    string ContainerName,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<PortMapping> Ports);
