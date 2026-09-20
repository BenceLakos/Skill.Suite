namespace Skill.Suite.Application.Sessions.MySession;

using Skill.Suite.Domain.Sessions;

/// <summary>
/// One service container the session runs, as a competitor needs to address it.
/// </summary>
/// <remarks>
/// There may be one of these per session or one per competitor, and <see cref="PerCompetitor"/> says which.
/// A service whose configuration names no competitor has fixed host ports, so a second copy would collide on
/// them and everybody in the session connects to the same instance; a service configured with a competitor
/// placeholder gets a container, a name and host ports of its own per competitor, and what is described here
/// is then THIS competitor's.
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
public sealed record MySessionServiceDto(
    string Number,
    string Image,
    string ContainerName,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<PortMapping> Ports,
    bool PerCompetitor);
