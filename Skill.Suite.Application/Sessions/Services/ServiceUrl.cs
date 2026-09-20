namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// The address to type for a routed service: its hostname, over plain http.
/// </summary>
/// <remarks>
/// Plain <c>http</c> and no port, deliberately. The stack has no TLS — a venue LAN has no certificate
/// authority, and a self-signed certificate would have to be trusted on every competitor machine — and the
/// proxy answers on the default port, so there is nothing to state.
/// <para>
/// An earlier version appended the port the caller reached this application on, reasoning that the browser
/// had come through the same proxy. That is a guess, not a derivation: it is wrong for anyone reaching the
/// Suite by another route, and a port in an address a competitor pastes is worse than absent. If the proxy
/// ever moves off 80 that is fixed at the proxy, not by this.
/// </para>
/// </remarks>
internal static class ServiceUrl
{
    private const string Scheme = "http://";

    public static string? For(string? host) =>
        string.IsNullOrWhiteSpace(host) ? null : Scheme + host;
}
