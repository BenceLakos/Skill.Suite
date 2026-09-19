namespace Skill.Suite.Application.Tests.Competitors;

using Skill.Suite.Application.Competitors.Accounts;
using Xunit;

/// <summary>
/// Defaults on the competitor-account options that are deployment decisions rather than preferences.
/// </summary>
public sealed class CompetitorAccountOptionsTests
{
    [Fact]
    public void TheSqlServerDefaultNamesTheComposeServiceRatherThanTheHost() =>
        // The app container joins the same external skill-suite network the server is on. A public name would
        // work on a developer machine and fail in the deployment that matters.
        Assert.Equal("mssql,1433", new MsSqlOptions().Server);

    [Fact]
    public void TheSelfSignedCertificateIsTrustedByDefault() =>
        // The image presents a self-signed certificate and a competition LAN has no CA to have signed a real
        // one. Off by default would make every provision fail with a handshake error.
        Assert.True(new MsSqlOptions().TrustServerCertificate);

    [Fact]
    public void TheConnectTimeoutIsShortBecauseTheUiWaitsOnIt() =>
        Assert.Equal(5, new MsSqlOptions().ConnectTimeoutSeconds);

    [Fact]
    public void TheCommandTimeoutLeavesRoomForCreateDatabase() =>
        Assert.Equal(30, new MsSqlOptions().CommandTimeoutSeconds);

    [Fact]
    public void TheGitHostEmailDomainIsNotRoutable() =>
        // Gitea demands a unique address per user and never sends to it. A real domain here would mail a
        // stranger for every competitor created.
        Assert.Equal("competitors.local", new CompetitorAccountsOptions().GiteaEmailDomain);
}
