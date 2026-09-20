namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.Services;
using Xunit;

/// <summary>
/// The address printed for a routed service, and where its port comes from.
/// </summary>
/// <remarks>
/// The port is the whole reason this is not string concatenation. The proxy listens on 80 inside its
/// container and the stack publishes it on <c>TRAEFIK_HTTP_PORT</c>, which this application has no setting
/// for and must not grow one — so it is read off the host the caller reached this application on, which came
/// through that very proxy on that very port. A URL missing it is one a competitor pastes, watches fail, and
/// spends competition time on.
/// </remarks>
public sealed class ServiceUrlTests
{
    [Fact]
    public void AHostWithNoPortGivesABareHttpUrl()
    {
        Assert.Equal("http://shop.skills.local", ServiceUrl.For("shop.skills.local", "suite.skills.local"));
    }

    [Fact]
    public void ThePortTheCallerReachedTheApplicationOnIsCarriedOver()
    {
        // The browser reached the suite through the proxy on this port, so the service behind the same proxy
        // is on it too.
        Assert.Equal(
            "http://shop.skills.local:8080",
            ServiceUrl.For("shop.skills.local", "suite.skills.local:8080"));
    }

    [Fact]
    public void TheDefaultHttpPortIsNotRestated()
    {
        Assert.Equal("http://shop.skills.local", ServiceUrl.For("shop.skills.local", "suite.skills.local:80"));
    }

    [Fact]
    public void NoCallerHostGivesTheBareUrl()
    {
        // What the admin page gets: it does not receive its own request host, and a bare URL is right
        // whenever the proxy is published on 80, which is the default.
        Assert.Equal("http://shop.skills.local", ServiceUrl.For("shop.skills.local", requestHost: null));
    }

    [Fact]
    public void AnIpv6CallerHostIsNotMistakenForAPort()
    {
        Assert.Equal("http://shop.skills.local", ServiceUrl.For("shop.skills.local", "[::1]"));
        Assert.Equal("http://shop.skills.local:8080", ServiceUrl.For("shop.skills.local", "[::1]:8080"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AServiceWithNoDomainHasNoUrl(string? host)
    {
        Assert.Null(ServiceUrl.For(host, "suite.skills.local:8080"));
    }
}
