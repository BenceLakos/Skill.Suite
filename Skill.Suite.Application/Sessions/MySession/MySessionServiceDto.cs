namespace Skill.Suite.Application.Sessions.MySession;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// One service container the session runs for THIS competitor.
/// </summary>
/// <remarks>
/// Always theirs alone: every service of a session is started once per competitor, the same way every
/// competitor gets their own session database. The container name, the host ports and the settings described
/// here belong to the competitor reading the page and to nobody else.
/// <para>
/// Volumes and labels are deliberately absent. They are host paths and bookkeeping for the docker daemon —
/// nothing a competitor can act on, and the host paths describe the competition machine's filesystem.
/// Environment variables ARE included: they are what the session author puts there for competitors to connect
/// with, which makes that surface the author's to decide.
/// </para>
/// <para>
/// The environment is shown resolved, exactly as the container received it, and nothing in it is masked.
/// Every secret a placeholder can put there is this competitor's own — their password, their SQL login, their
/// connection string — and the same page already prints all three verbatim in the credentials and database
/// cards. Masking here would hide nothing they do not already have while making the page disagree with what
/// is actually inside their container, which is the one thing it exists to tell them. The administrator's
/// credentials never reach this page: they appear only in marking containers, and marking runs on a closed
/// session, which this page does not describe.
/// </para>
/// <para>
/// A database server in the environment is shown as the CONTAINER addresses it — the docker host — not as
/// the competitor's own laptop does. It is the value the service is actually running with, and a doctored
/// one would make this page disagree with what is inside the container. The address the competitor connects
/// to SQL Server on themselves is on the database card, where it belongs.
/// </para>
/// </remarks>
/// <param name="Number">The service's position as the admin sees it listed, and as its docker label carries it.</param>
/// <remarks>
/// The docker image reference is deliberately absent. It named nothing a competitor can act on — they cannot
/// pull it, restart it or choose another — and it leaked the session author's registry layout onto a page
/// whose whole job is the address and the settings to connect with.
/// </remarks>
/// <param name="Ports">
/// The published host ports, which are what a competitor connects to when <paramref name="Url"/> is null.
/// A routed service shows its URL INSTEAD of these, even when it also publishes ports: a host port beside a
/// URL is a second address for the same container that does not go through the proxy, and a competitor who
/// used it would be working around the very thing that keeps each of them on their own instance.
/// </param>
/// <param name="Url">
/// The address to open when the service is routed by hostname, or null when it is reached on a published
/// port alone. It answers only from this competitor's own workstation — the proxy sends every other machine
/// to its own competitor's container.
/// </param>
public sealed record MySessionServiceDto(
    string Number,
    string ContainerName,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<PortMapping> Ports,
    string? Url);
