namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.Services;
using Xunit;

/// <summary>
/// The SQL Server as a session's service container reaches it — the third view of one server.
/// </summary>
/// <remarks>
/// Worth pinning because the wrong answer here does not fail: it produces a service container whose
/// connection string points at itself or at a name nothing resolves, which starts perfectly and refuses
/// every query once the competition has begun.
/// </remarks>
public sealed class ServiceSqlServerNameTests
{
    [Fact]
    public void AComposeServiceNameBecomesTheDockerHost()
    {
        // The default MsSqlOptions value: this process resolves it over the skill-suite network, and a
        // service container on the host daemon's default bridge does not.
        Assert.Equal("host.docker.internal,1433", ServiceSqlServerName.For("mssql,1433"));
    }

    [Fact]
    public void LocalhostBecomesTheDockerHostToo()
    {
        // Inside a container, localhost is the container. This is the value a developer running the app
        // against the compose stack sets, and it is the one that would look right and connect to nothing.
        Assert.Equal("host.docker.internal,1433", ServiceSqlServerName.For("localhost,1433"));
        Assert.Equal("host.docker.internal,1433", ServiceSqlServerName.For("127.0.0.1,1433"));
    }

    [Fact]
    public void AnAddressWithNoPortKeepsHavingNone()
    {
        Assert.Equal("host.docker.internal", ServiceSqlServerName.For("mssql"));
    }

    [Theory]
    [InlineData("sql.skills.example.com,1433")]
    [InlineData("10.0.0.5,1433")]
    public void AnAddressTheHostCanAlreadyReachIsLeftAlone(string server)
    {
        Assert.Equal(server, ServiceSqlServerName.For(server));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoConfiguredServerResolvesToNothing(string? server)
    {
        // Null rather than a guess: a connection string built around a made-up host is something an expert
        // would paste, watch fail, and spend competition time on.
        Assert.Null(ServiceSqlServerName.For(server));
    }

    [Fact]
    public void SurroundingWhitespaceIsIgnored()
    {
        Assert.Equal("host.docker.internal,1433", ServiceSqlServerName.For("  mssql,1433  "));
    }
}
