namespace Skill.Suite.Configuration;

using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

/// <summary>
/// Says which proxies are allowed to tell this application who the client is.
/// </summary>
/// <remarks>
/// The deployment puts Traefik in front of the application, so every request arrives from the proxy's own
/// address on the docker network. Anything that acts on the caller's address — the sign-in page's workstation
/// pre-fill — therefore sees one address for the whole venue unless the forwarded header is honoured.
/// <para>
/// Honouring it is opt-in and empty by default, deliberately. <c>X-Forwarded-For</c> is a request header:
/// trusting it from an untrusted peer lets anyone claim to be sitting at any workstation, which for the
/// pre-fill means asking the server for another competitor's password. With nothing configured the middleware
/// is not even added, and the application behaves exactly as it did before.
/// </para>
/// <para>
/// <see cref="ForwardedHeadersOptions.ForwardLimit"/> stays at one, and only <c>X-Forwarded-For</c> is
/// processed. Traefik APPENDS the peer it saw to whatever the client sent, so the right-most entry — the only
/// one a limit of one consumes — is the proxy's own observation, and a value the client made up stays to the
/// left of it and is never read.
/// </para>
/// </remarks>
internal static class ForwardedHeadersSetup
{
    /// <summary>Configuration section the trusted proxies are read from.</summary>
    private const string SectionName = "ForwardedHeaders";

    /// <summary>Individual proxy addresses, e.g. <c>172.18.0.2</c>.</summary>
    private const string KnownProxiesKey = "KnownProxies";

    /// <summary>Proxy address ranges in CIDR form, e.g. <c>172.16.0.0/12</c>.</summary>
    private const string KnownNetworksKey = "KnownNetworks";

    /// <summary>How many <c>X-Forwarded-For</c> entries are read, counting from the right.</summary>
    private const int TrustedProxyHops = 1;

    /// <summary>
    /// Registers the forwarded-header options, and says whether the middleware is worth adding at all.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when at least one proxy or network is configured. <see langword="false"/> means
    /// the caller must not add the middleware: with no trusted peer it would do nothing but cost a hop, and
    /// leaving it out keeps "no configuration" and "no forwarding" the same thing.
    /// </returns>
    public static bool AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        var proxies = Parse<IPAddress>(section.GetSection(KnownProxiesKey).Get<string[]>());
        var networks = Parse<IPNetwork>(section.GetSection(KnownNetworksKey).Get<string[]>());

        if (proxies.Count == 0 && networks.Count == 0)
            return false;

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = TrustedProxyHops;

            // The defaults trust loopback. Cleared rather than added to: a deployment that names its proxy has
            // said which peer it trusts, and silently keeping another one would be the opposite of that.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in proxies)
                options.KnownProxies.Add(proxy);

            foreach (var network in networks)
                options.KnownIPNetworks.Add(network);
        });

        return true;
    }

    /// <summary>
    /// The values that parse, skipping blanks.
    /// </summary>
    /// <remarks>
    /// Blanks are skipped rather than rejected because these arrive through compose as environment variables
    /// with empty defaults, and an unset one has to read as "not configured" instead of stopping the
    /// application from starting.
    /// </remarks>
    private static List<T> Parse<T>(string[]? values) where T : IParsable<T>
    {
        var parsed = new List<T>();

        foreach (var value in values ?? [])
        {
            if (!string.IsNullOrWhiteSpace(value) && T.TryParse(value.Trim(), provider: null, out var result))
                parsed.Add(result);
        }

        return parsed;
    }
}
