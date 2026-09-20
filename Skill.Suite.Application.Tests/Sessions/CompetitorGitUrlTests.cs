namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.MySession;
using Xunit;

/// <summary>
/// The clone URL a competitor is handed, rewritten from the one the git server reported.
/// </summary>
/// <remarks>
/// The stored URL is whatever the git server calls itself, which in this deployment is a name only the
/// docker network resolves. A competitor copies it, runs <c>git clone</c>, and gets a host-not-found at the
/// start of the competition — so the public name is derived from the host they reached this page on, exactly
/// as SQL Server's is.
/// </remarks>
public sealed class CompetitorGitUrlTests
{
    private const string Stored = "http://gitea:3000/round-1/c01.git";

    [Fact]
    public void TheSchemeAndAuthorityBecomeThePublicGitHost()
    {
        Assert.Equal(
            "http://git.skills.local/round-1/c01.git",
            CompetitorGitUrl.For(Stored, "suite.skills.local"));
    }

    [Fact]
    public void ThePathIsKeptBecauseItNamesTheOrganisationAndTheRepository()
    {
        Assert.EndsWith(
            "/round-1/c01.git",
            CompetitorGitUrl.For(Stored, "suite.skills.local"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ThePrefixIsMatchedWithoutRegardToCase()
    {
        Assert.Equal(
            "http://git.Skills.Local/round-1/c01.git",
            CompetitorGitUrl.For(Stored, "SUITE.Skills.Local"));
    }

    [Fact]
    public void ThePortTheBrowserReachedTheSuiteOnIsNotCarriedOver()
    {
        // The git host is published on the proxy's own port, not on the one this page happened to be
        // reached on; a port here would be a second guess in an address a competitor pastes.
        Assert.Equal(
            "http://git.skills.local/round-1/c01.git",
            CompetitorGitUrl.For(Stored, "suite.skills.local:8080"));
    }

    [Fact]
    public void AQueryStringSurvives()
    {
        Assert.Equal(
            "http://git.skills.local/round-1/c01.git?x=1",
            CompetitorGitUrl.For("http://gitea:3000/round-1/c01.git?x=1", "suite.skills.local"));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("10.0.0.5")]
    [InlineData("mysuite.local")]
    [InlineData(null)]
    [InlineData("")]
    public void AHostThatSaysNothingAboutTheDomainLeavesTheUrlAlone(string? requestHost)
    {
        // A local stack reaches the git server at the name it reported. Unchanged is at least usable;
        // a rewritten guess is not.
        Assert.Equal(Stored, CompetitorGitUrl.For(Stored, requestHost));
    }

    [Fact]
    public void SomethingThatIsNotAnAbsoluteUrlIsLeftAlone()
    {
        Assert.Equal("not a url", CompetitorGitUrl.For("not a url", "suite.skills.local"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoStoredUrlStaysNoUrl(string? stored)
    {
        // Provisioning has not written it yet, which the page reports as "not provisioned" rather than as
        // an address.
        Assert.Equal(stored, CompetitorGitUrl.For(stored, "suite.skills.local"));
    }
}
