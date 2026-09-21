namespace Skill.Suite.Application.Sessions.MySession;

/// <summary>
/// One service of the session, as the competitor reaches it: a hostname, and nothing else.
/// </summary>
/// <remarks>
/// Only routed services appear here. A service with no domain has no address a competitor can be given, so it
/// is not something this page can tell them anything useful about at all.
/// <para>
/// Deliberately nothing else. The service number, the container name, the published host ports and the
/// resolved environment were all on this page and all came off it: none of them is something a competitor can
/// act on, a host port beside a routed name is a second address for the same container that skips the proxy,
/// and the rest described how the session was assembled rather than how to use it.
/// </para>
/// </remarks>
/// <param name="Domain">The hostname the service answers on, exactly as the session stores it.</param>
/// <param name="Url">
/// <paramref name="Domain"/> as an address to open. It answers only from this competitor's own machines — the
/// proxy sends every other workstation to its own competitor's container.
/// </param>
public sealed record MySessionServiceDto(string Domain, string Url);
