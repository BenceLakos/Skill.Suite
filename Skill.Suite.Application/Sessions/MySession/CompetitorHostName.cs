namespace Skill.Suite.Application.Sessions.MySession;

/// <summary>
/// Derives the other services' public hostnames from the one the competitor reached this application on.
/// </summary>
/// <remarks>
/// The deployment publishes everything off one proxy under one domain — <c>suite.&lt;domain&gt;</c> for the
/// platform, <c>git.&lt;domain&gt;</c> for the git host, <c>mssql.&lt;domain&gt;</c> for SQL Server — so a
/// browser that reached this page at <c>suite.&lt;rest&gt;</c> reaches the others by swapping the first
/// label. Derived rather than configured because the relationship is already fixed by the deployment, and a
/// second setting saying the same thing is a second setting to drift.
/// <para>
/// Shared by every such name so they cannot disagree about what "the domain" is. A name derived one way for
/// SQL Server and another for the git host would be two bugs to find instead of one.
/// </para>
/// </remarks>
internal static class CompetitorHostName
{
    /// <summary>Hostname prefix this application itself is published under.</summary>
    private const string SuitePrefix = "suite.";

    private const char PortSeparator = ':';

    /// <summary>
    /// <paramref name="requestHost"/> with its leading <c>suite.</c> replaced by <paramref name="prefix"/>,
    /// or null when it carries no such label and so says nothing about the domain.
    /// </summary>
    /// <remarks>
    /// Null rather than the host unchanged, so each caller decides what "not a proxied deployment" means for
    /// it — SQL Server falls back to the host itself, which is the docker host publishing the port directly,
    /// while the git host falls back to the URL the server itself reported.
    /// </remarks>
    public static string? WithPrefix(string? requestHost, string prefix)
    {
        var host = WithoutPort(requestHost);

        return host is not null && host.StartsWith(SuitePrefix, StringComparison.OrdinalIgnoreCase)
            ? prefix + host[SuitePrefix.Length..]
            : null;
    }

    /// <summary>
    /// The host alone, trimmed and with any port dropped, or null when there is nothing to read.
    /// </summary>
    /// <remarks>
    /// The port is dropped because these names are derived from the DOMAIN, not from the port the browser
    /// happened to reach this application on: SQL Server's port is the TDS one and the git host's is the
    /// proxy's, and neither is the Suite's. The separator is the last colon outside an IPv6 literal's
    /// brackets, so <c>[::1]</c> is not mistaken for a host and a port.
    /// </remarks>
    public static string? WithoutPort(string? requestHost)
    {
        if (string.IsNullOrWhiteSpace(requestHost))
            return null;

        var host = requestHost.Trim();

        var separator = host.LastIndexOf(PortSeparator);

        return separator < 0 || separator < host.LastIndexOf(']') ? host : host[..separator];
    }
}
