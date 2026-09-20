namespace Skill.Suite.Application.Sessions.Services;

using System.Net;

/// <summary>
/// The SQL Server as a SERVICE CONTAINER reaches it, derived from the name this process reaches it by.
/// </summary>
/// <remarks>
/// A third view of one server, and the third one that cannot be borrowed from the other two.
/// <c>MsSqlOptions.Server</c> is the compose service name this application resolves over the
/// <c>skill-suite</c> network; <see cref="Skill.Suite.Application.Sessions.MySession.CompetitorSqlServerName"/>
/// is the published hostname a competitor's laptop resolves. A session's services are started on the HOST
/// daemon over the mounted socket and join its default bridge, so they resolve neither: a compose service
/// name is not on their network, and <c>localhost</c> is the service container itself.
/// <para>
/// What such a container can always reach is the docker host, which is where the stack publishes SQL Server
/// — so a name that only the docker network or the loopback resolves becomes
/// <see cref="DockerHostAlias"/> on the same port. <c>DockerServiceArguments</c> passes
/// <c>--add-host host.docker.internal:host-gateway</c> so that alias resolves on Linux engines too, where it
/// is not built in.
/// </para>
/// <para>
/// Anything else — an FQDN or an IP address — is a server the host can already reach by that name, so it is
/// returned unchanged. Derived rather than configured for the reason the registry host is: the deployment
/// already fixes the relationship, and a second setting saying the same thing is a second setting to drift.
/// </para>
/// </remarks>
internal static class ServiceSqlServerName
{
    /// <summary>The docker host, as a container on the host daemon addresses it.</summary>
    private const string DockerHostAlias = "host.docker.internal";

    /// <summary>What separates host from port in a SQL Server data source. Not a colon.</summary>
    private const char PortSeparator = ',';

    private const string LoopbackHost = "localhost";

    public static string? For(string? processServer)
    {
        if (string.IsNullOrWhiteSpace(processServer))
            return null;

        var server = processServer.Trim();

        var separator = server.IndexOf(PortSeparator);
        var host = separator < 0 ? server : server[..separator];
        var port = separator < 0 ? string.Empty : server[separator..];

        return IsUnreachableFromAServiceContainer(host) ? DockerHostAlias + port : server;
    }

    /// <summary>
    /// Whether the host names something a container on the host daemon's default bridge cannot resolve to
    /// the SQL Server: a single-label compose service name, or the loopback, which is the container itself.
    /// </summary>
    private static bool IsUnreachableFromAServiceContainer(string host) =>
        host.Equals(LoopbackHost, StringComparison.OrdinalIgnoreCase)
        || IPAddress.IsLoopback(Parse(host))
        || (!host.Contains('.') && !IPAddress.TryParse(host.Trim('[', ']'), out _));

    /// <summary>The address, or <see cref="IPAddress.None"/> when the host is a name rather than one.</summary>
    private static IPAddress Parse(string host) =>
        IPAddress.TryParse(host.Trim('[', ']'), out var address) ? address : IPAddress.None;
}
