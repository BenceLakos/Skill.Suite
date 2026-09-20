namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// The address to type for a routed service, built from its domain.
/// </summary>
/// <remarks>
/// Plain <c>http</c>, because the stack has no TLS: a venue LAN has no certificate authority, and a
/// self-signed certificate would have to be trusted on every competitor machine.
/// <para>
/// The port is the one problem worth solving here. The proxy listens on 80 inside its container but the
/// compose stack publishes it as <c>TRAEFIK_HTTP_PORT</c>, which this application has no setting for and
/// must not grow one — so it is read off the host the caller reached this application on, which went through
/// that very proxy on that very port. Without a caller host there is nothing to read and the bare
/// <c>http://host</c> is returned, which is right whenever the proxy is on 80.
/// </para>
/// </remarks>
internal static class ServiceUrl
{
    private const string Scheme = "http://";

    /// <summary>The port an http URL does not need to state.</summary>
    private const string DefaultHttpPort = "80";

    private const char PortSeparator = ':';

    public static string? For(string? host, string? requestHost)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        var port = PortOf(requestHost);

        return port is null ? Scheme + host : Scheme + host + PortSeparator + port;
    }

    /// <summary>
    /// The explicit, non-default port of the host the caller reached this application on, if it has one.
    /// </summary>
    /// <remarks>
    /// An IPv6 literal's colons are inside brackets, so the separator is looked for after them.
    /// </remarks>
    private static string? PortOf(string? requestHost)
    {
        if (string.IsNullOrWhiteSpace(requestHost))
            return null;

        var trimmed = requestHost.Trim();

        var separator = trimmed.LastIndexOf(PortSeparator);
        if (separator < 0 || separator < trimmed.LastIndexOf(']'))
            return null;

        var port = trimmed[(separator + 1)..];

        return port.Length == 0 || port == DefaultHttpPort ? null : port;
    }
}
