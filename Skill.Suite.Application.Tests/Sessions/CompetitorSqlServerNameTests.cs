namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.MySession;
using Xunit;

/// <summary>
/// The SQL Server name a competitor is handed, derived from the address they reached the platform on.
/// </summary>
/// <remarks>
/// The derivation replaces a configuration knob, so these cases are the contract: the deployment publishes
/// <c>suite.${DOMAIN}</c> and <c>sql.${DOMAIN}</c> off the same proxy, and everything else — a local stack, a
/// bare address on the venue LAN — is the docker host publishing the port itself.
/// </remarks>
public sealed class CompetitorSqlServerNameTests
{
    [Fact]
    public void TheProxiedSuiteHostBecomesTheProxiedSqlHost()
    {
        Assert.Equal("sql.skills.local,1433", CompetitorSqlServerName.For("suite.skills.local"));
    }

    [Fact]
    public void ThePrefixIsMatchedWithoutRegardToCase()
    {
        // Host names are case-insensitive, and a browser is free to send the one the competitor typed.
        Assert.Equal("sql.Skills.Local,1433", CompetitorSqlServerName.For("SUITE.Skills.Local"));
    }

    [Fact]
    public void ALocalStackKeepsItsHost()
    {
        Assert.Equal("localhost,1433", CompetitorSqlServerName.For("localhost"));
    }

    [Fact]
    public void AnAddressKeepsItsHost()
    {
        Assert.Equal("10.0.0.5,1433", CompetitorSqlServerName.For("10.0.0.5"));
    }

    [Fact]
    public void OnlyALeadingSuiteLabelIsReplaced()
    {
        // "mysuite.local" is somebody's host name, not this application's published route.
        Assert.Equal("mysuite.local,1433", CompetitorSqlServerName.For("mysuite.local"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoHostMeansNoServerName(string? host)
    {
        Assert.Null(CompetitorSqlServerName.For(host));
    }
}
