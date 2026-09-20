namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.Services;
using Xunit;

/// <summary>
/// The address printed for a routed service.
/// </summary>
/// <remarks>
/// Deliberately nothing but the scheme and the hostname. An earlier version appended the port the caller
/// reached this application on, reasoning that the browser had come through the same proxy — a guess, wrong
/// for anyone reaching the Suite by another route, and a wrong port in an address a competitor pastes costs
/// competition time. The proxy answers on the default port; if that ever changes it is fixed at the proxy.
/// </remarks>
public sealed class ServiceUrlTests
{
    [Fact]
    public void ADomainBecomesAPlainHttpUrl()
    {
        Assert.Equal("http://shop.skills.local", ServiceUrl.For("shop.skills.local"));
    }

    [Fact]
    public void NoPortIsEverAppended()
    {
        var authority = ServiceUrl.For("shop.skills.local")!["http://".Length..];

        Assert.DoesNotContain(":", authority, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSchemeIsHttpBecauseTheStackHasNoTls()
    {
        // A venue LAN has no certificate authority, and a self-signed certificate would have to be trusted
        // on every competitor machine.
        Assert.StartsWith("http://", ServiceUrl.For("shop.skills.local"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AServiceWithNoDomainHasNoUrl(string? host)
    {
        Assert.Null(ServiceUrl.For(host));
    }
}
