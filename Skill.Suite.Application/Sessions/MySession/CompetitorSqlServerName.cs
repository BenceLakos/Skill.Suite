namespace Skill.Suite.Application.Sessions.MySession;

/// <summary>
/// The SQL Server as a COMPETITOR'S machine reaches it, derived from the host they reached this application on.
/// </summary>
/// <remarks>
/// Derived rather than configured, because the deployment already fixes the relationship. <c>infra/compose.yaml</c>
/// publishes this application at <c>suite.${DOMAIN}</c> and SQL Server at <c>sql.${DOMAIN}</c>, both as
/// aliases of the one Traefik container, and gives TDS a TCP entrypoint of its own on 1433 because a raw TDS
/// stream carries no SNI to route on. A browser that reached this page at <c>suite.&lt;rest&gt;</c> therefore
/// reaches the server at <c>sql.&lt;rest&gt;,1433</c>. Any other host — <c>localhost</c> in the local stack, a
/// bare address on a venue LAN — is the docker host itself, which publishes the port directly, so it is
/// returned unchanged.
/// <para>
/// <c>MsSqlOptions.Server</c> deliberately does not answer this question: it is the name THIS PROCESS uses over
/// the container network, and <c>mssql,1433</c> resolves to nothing on a competitor's laptop.
/// </para>
/// </remarks>
internal static class CompetitorSqlServerName
{
    /// <summary>Hostname prefix this application is published under.</summary>
    private const string SuitePrefix = "suite.";

    /// <summary>Hostname prefix SQL Server is published under, in place of <see cref="SuitePrefix"/>.</summary>
    private const string SqlPrefix = "sql.";

    /// <summary>The TDS port, as the proxy's own mssql entrypoint publishes it.</summary>
    private const int TdsPort = 1433;

    /// <summary>What separates host from port in a SQL Server data source. Not a colon.</summary>
    private const char PortSeparator = ',';

    public static string? For(string? requestHost)
    {
        if (string.IsNullOrWhiteSpace(requestHost))
            return null;

        var host = requestHost.Trim();

        if (host.StartsWith(SuitePrefix, StringComparison.OrdinalIgnoreCase))
            host = string.Concat(SqlPrefix, host.AsSpan(SuitePrefix.Length));

        return $"{host}{PortSeparator}{TdsPort}";
    }
}
