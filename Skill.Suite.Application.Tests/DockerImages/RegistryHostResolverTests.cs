namespace Skill.Suite.Application.Tests.DockerImages;

using Skill.Suite.Application.DockerImages;
using Xunit;

/// <summary>
/// Which host the offered image references are built against.
/// </summary>
/// <remarks>
/// The registry lives on the same host and port as the Gitea API, so the internal base URL is reduced to its
/// authority — but that authority is how this <em>process</em> reaches Gitea over the docker network, and every
/// pull runs on the host daemon instead. A single-label host is a compose service name the host cannot look up,
/// so it is answered with the published port on <c>localhost</c>, which is also the one host docker treats as an
/// insecure registry without being configured to. An operator who pastes a bare host, a trailing slash or a path
/// must still end up with a reference a daemon can parse.
/// </remarks>
public sealed class RegistryHostResolverTests
{
    [Theory]
    [InlineData("http://gitea:3000", "localhost:3000")]
    [InlineData("https://git.skills.local/", "git.skills.local")]
    [InlineData("http://localhost:3300/some/path", "localhost:3300")]
    public void TheSchemeAndPathAreStripped(string gitInternalBaseUrl, string expected) =>
        Assert.Equal(expected, RegistryHostResolver.Resolve(gitInternalBaseUrl));

    [Fact]
    public void AComposeServiceNameBecomesLocalhostOnTheSamePort() =>
        // The port compose publishes is assumed to be the internal one, which is how the stack maps it.
        Assert.Equal("localhost:3000", RegistryHostResolver.Resolve("http://gitea:3000"));

    [Fact]
    public void AComposeServiceNameWithNoPortBecomesBareLocalhost() =>
        // Nothing in the URL says which port was published, so none is invented.
        Assert.Equal("localhost", RegistryHostResolver.Resolve("http://gitea"));

    [Fact]
    public void AnIpAddressIsLeftAlone() =>
        // The host daemon can route to it; over plain http it needs an insecure-registries entry.
        Assert.Equal("10.0.0.5:3000", RegistryHostResolver.Resolve("http://10.0.0.5:3000"));

    [Fact]
    public void ABareHostIsAcceptedAsIs() =>
        Assert.Equal("git.skills.local:3000", RegistryHostResolver.Resolve("git.skills.local:3000/"));

    [Fact]
    public void TheHostIsLowerCased() =>
        Assert.Equal("git.skills.local", RegistryHostResolver.Resolve("http://Git.Skills.Local"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnUnsetUrlFallsBackToThePublishedRegistryPort(string? gitInternalBaseUrl) =>
        Assert.Equal("localhost:3000", RegistryHostResolver.Resolve(gitInternalBaseUrl));

    [Fact]
    public void TheFallbackIsTheDaemonFacingDefault() =>
        Assert.Equal("localhost:3000", RegistryHostResolver.DefaultRegistryHost);
}
