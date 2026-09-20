namespace Skill.Suite.Application.Sessions.MySession;

/// <summary>
/// The SQL Server as a COMPETITOR'S machine reaches it, derived from the host they reached this application on.
/// </summary>
/// <remarks>
/// Derived rather than configured, because the deployment already fixes the relationship: everything is
/// published off one proxy under one domain, so a browser that reached this page at <c>suite.&lt;rest&gt;</c>
/// reaches the server at <c>mssql.&lt;rest&gt;,1433</c>. TDS gets a TCP entrypoint of its own on 1433 because
/// a raw TDS stream carries no SNI to route on. Any other host — <c>localhost</c> in the local stack, a bare
/// address on a venue LAN — is the docker host itself, which publishes the port directly, so it is used as it
/// stands.
/// <para>
/// <c>MsSqlOptions.Server</c> deliberately does not answer this question: it is the name THIS PROCESS uses over
/// the container network, and <c>mssql,1433</c> resolves to nothing on a competitor's laptop.
/// </para>
/// </remarks>
internal static class CompetitorSqlServerName
{
    /// <summary>Hostname prefix SQL Server is published under, in place of the Suite's own.</summary>
    private const string SqlPrefix = "mssql.";

    /// <summary>The TDS port, as the proxy's own mssql entrypoint publishes it.</summary>
    private const int TdsPort = 1433;

    /// <summary>What separates host from port in a SQL Server data source. Not a colon.</summary>
    private const char PortSeparator = ',';

    public static string? For(string? requestHost)
    {
        // The port the browser reached the Suite on is dropped either way: the data source's port is the TDS
        // one, and carrying the Suite's over would produce something like "mssql.x:8080,1433".
        var host = CompetitorHostName.WithPrefix(requestHost, SqlPrefix)
                   ?? CompetitorHostName.WithoutPort(requestHost);

        return host is null ? null : $"{host}{PortSeparator}{TdsPort}";
    }
}
