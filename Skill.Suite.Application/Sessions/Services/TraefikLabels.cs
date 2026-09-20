namespace Skill.Suite.Application.Sessions.Services;

using System.Globalization;
using System.Text;

/// <summary>
/// Builds the docker labels that make the reverse proxy route a session service.
/// </summary>
/// <remarks>
/// Generated rather than typed by the administrator, because getting them right is not a matter of care: the
/// router name has to be unique across the whole daemon, the service name has to match between two labels,
/// the port has to be the container's rather than the published one, and the source-address rule has to be
/// exactly the competitor's. One of those wrong produces a proxy that routes every competitor to the first
/// container, which looks like a working session until two of them compare notes.
/// <para>
/// PRIORITY is deliberately left to Traefik's default, which is the length of the rule. A per-competitor
/// rule is <c>Host(...) &amp;&amp; ClientIP(...)</c> and a shared one is <c>Host(...)</c>, so the specific
/// rule is always the longer and always wins — but only reliably so while no two services of a session share
/// a domain, which the session validators enforce. Two equal-length rules on one hostname would be resolved
/// by a tie-break nothing here controls.
/// </para>
/// </remarks>
internal static class TraefikLabels
{
    private const string EnableKey = "traefik.enable";
    private const string DockerNetworkKey = "traefik.docker.network";

    private const string RouterPrefix = "traefik.http.routers.";
    private const string ServicePrefix = "traefik.http.services.";

    private const string EntrypointsSuffix = ".entrypoints";
    private const string RuleSuffix = ".rule";
    private const string RouterServiceSuffix = ".service";
    private const string ServerPortSuffix = ".loadbalancer.server.port";

    private const string Enabled = "true";

    /// <summary>
    /// The HTTP entrypoint, as <c>infra/compose.yaml</c> names it on the proxy.
    /// </summary>
    /// <remarks>
    /// Named explicitly rather than left to Traefik's default of "every entrypoint": the stack also has a
    /// raw TCP entrypoint for SQL Server, and a router bound to it as well would try to answer TDS with HTTP.
    /// </remarks>
    internal const string WebEntrypoint = "web";

    /// <summary>Traefik's rule matcher for the <c>Host</c> header.</summary>
    private const string HostMatcher = "Host";

    /// <summary>Traefik's rule matcher for the source address of the request.</summary>
    private const string ClientIpMatcher = "ClientIP";

    private const string RuleAnd = " && ";
    private const char RuleQuote = '`';

    /// <summary>The one character a Traefik router or service name may carry besides letters and digits.</summary>
    private const char NameSeparator = '-';

    public static Dictionary<string, string> For(TraefikRoute route)
    {
        var name = NameOf(route.ContainerName);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EnableKey] = Enabled,
            [DockerNetworkKey] = route.Network,
            [RouterPrefix + name + EntrypointsSuffix] = WebEntrypoint,
            [RouterPrefix + name + RouterServiceSuffix] = name,
            [RouterPrefix + name + RuleSuffix] = Rule(route),
            [ServicePrefix + name + ServerPortSuffix] =
                route.ContainerPort.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// The router's — and the service's — name, derived from the container name.
    /// </summary>
    /// <remarks>
    /// From the container name because that is already unique on the daemon and already carries the session,
    /// the service position and the competitor; anything else would need a second uniqueness argument. Router
    /// and service share the name deliberately: they live in different Traefik namespaces, and one name means
    /// one thing to look for in the dashboard.
    /// <para>
    /// Sanitised rather than assumed: a competitor username may legitimately contain a dot or an underscore,
    /// and a dot in a router name would be read as another level of Traefik's own label tree.
    /// </para>
    /// </remarks>
    internal static string NameOf(string containerName)
    {
        var name = new StringBuilder(containerName.Length);

        foreach (var character in containerName.ToLowerInvariant())
            name.Append(char.IsAsciiLetterOrDigit(character) ? character : NameSeparator);

        return name.ToString();
    }

    private static string Rule(TraefikRoute route)
    {
        var host = Matcher(HostMatcher, route.Host);

        return route.ClientIp is null ? host : host + RuleAnd + Matcher(ClientIpMatcher, route.ClientIp);
    }

    private static string Matcher(string matcher, string value) =>
        $"{matcher}({RuleQuote}{value}{RuleQuote})";
}
