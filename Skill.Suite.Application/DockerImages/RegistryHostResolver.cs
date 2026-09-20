namespace Skill.Suite.Application.DockerImages;

using System.Net;

/// <summary>
/// Works out which registry host an image reference offered in the UI should carry.
/// </summary>
/// <remarks>
/// Gitea serves its container registry on the same host and port as its API, so the registry host is the
/// configured internal base URL with the scheme and any path removed. Nothing is configured separately for it.
/// <para>
/// There are two views of that one registry, and they are not interchangeable. The process view is what this
/// application uses over the docker network — a compose service name such as <c>gitea:3000</c>. The daemon
/// view is what <c>docker login</c> and <c>docker pull</c> use, and those run on the <em>host</em> daemon whose
/// socket is mounted into this container: the host has no DNS for a compose service name, so a single-label
/// host is translated to <c>localhost</c> plus the same port, which is where compose publishes the registry.
/// That also sidesteps the daemon's https-by-default rule, because <c>localhost</c> and 127.0.0.0/8 are
/// insecure registries out of the box. <see cref="Resolve"/> returns the daemon view and is what the UI offers.
/// </para>
/// </remarks>
public static class RegistryHostResolver
{
    /// <summary>The registry as the host daemon reaches it when configuration does not say.</summary>
    public const string DefaultRegistryHost = "localhost:3000";

    /// <summary>The host the daemon is handed in place of a name only the docker network can resolve.</summary>
    private const string DaemonLoopbackHost = "localhost";

    /// <summary>
    /// The registry host to pull from, as the docker daemon sees it.
    /// </summary>
    /// <remarks>
    /// A single-label host (no dot, not an IP address, not <c>localhost</c>) can only be a docker-network
    /// service name, so it becomes <c>localhost</c> and the URL's explicit port. Without an explicit port the
    /// published port cannot be guessed, so bare <c>localhost</c> is returned — that assumes the port published
    /// on the host equals the internal one, which is how the compose stack maps it.
    /// <para>
    /// Anything else — an FQDN, an IP address, <c>localhost</c> itself — is already reachable from the host and
    /// is returned lower-cased. Such a host served over plain http still has to be listed in the daemon's
    /// <c>insecure-registries</c>, which is the one thing <c>localhost</c> never needs.
    /// </para>
    /// </remarks>
    public static string Resolve(string? gitInternalBaseUrl)
    {
        var authority = ResolveProcessHost(gitInternalBaseUrl);
        if (authority is null)
            return DefaultRegistryHost;

        // The port separator is the last colon that is not inside an IPv6 literal's brackets.
        var separator = authority.LastIndexOf(':');
        if (separator < authority.LastIndexOf(']'))
            separator = -1;

        var host = separator < 0 ? authority : authority[..separator];
        var port = separator < 0 ? string.Empty : authority[separator..];

        return IsDockerNetworkName(host) ? DaemonLoopbackHost + port : authority;
    }

    /// <summary>
    /// The registry host as this process reaches it over the docker network — the lower-cased authority of
    /// <paramref name="gitInternalBaseUrl"/>, or <see langword="null"/> when it names none.
    /// </summary>
    /// <remarks>
    /// Nothing pulls with this host; it is the one a stored image reference was built against before the daemon
    /// view existed, so <see cref="DaemonImageReference"/> needs it to recognise such a reference.
    /// </remarks>
    public static string? ResolveProcessHost(string? gitInternalBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(gitInternalBaseUrl))
            return null;

        var authority = Uri.TryCreate(gitInternalBaseUrl, UriKind.Absolute, out var url)
            && !string.IsNullOrEmpty(url.Authority)
                ? url.Authority
                : BareAuthority(gitInternalBaseUrl);

        return string.IsNullOrWhiteSpace(authority) ? null : authority.ToLowerInvariant();
    }

    /// <summary>An operator is free to write the setting as a bare host, with or without a trailing path.</summary>
    private static string BareAuthority(string gitInternalBaseUrl)
    {
        var trimmed = gitInternalBaseUrl.Trim();
        var pathStart = trimmed.IndexOf('/');
        return pathStart < 0 ? trimmed : trimmed[..pathStart];
    }

    /// <remarks>
    /// The brackets of an IPv6 authority are trimmed off because <see cref="IPAddress.TryParse(string?, out
    /// IPAddress?)"/> does not accept them, and an address that failed to parse would be mistaken for a
    /// service name.
    /// </remarks>
    private static bool IsDockerNetworkName(string host) =>
        !host.Contains('.')
        && !host.Equals(DaemonLoopbackHost, StringComparison.Ordinal)
        && !IPAddress.TryParse(host.Trim('[', ']'), out _);
}
