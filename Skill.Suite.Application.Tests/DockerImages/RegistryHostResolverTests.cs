namespace Skill.Suite.Application.Tests.DockerImages;

using Skill.Suite.Application.DockerImages;
using Xunit;

/// <summary>
/// Which host the offered image references are built against.
/// </summary>
/// <remarks>
/// The registry lives on the same host and port as the Gitea API, so the internal base URL is reduced to its
/// authority. An operator who pastes a bare host, a trailing slash or a path must still end up with a
/// reference a daemon can parse.
/// </remarks>
public sealed class RegistryHostResolverTests
{
    [Theory]
    [InlineData("http://gitea:3000", "gitea:3000")]
    [InlineData("https://git.skills.local/", "git.skills.local")]
    [InlineData("http://localhost:3300/some/path", "localhost:3300")]
    public void TheSchemeAndPathAreStripped(string gitInternalBaseUrl, string expected) =>
        Assert.Equal(expected, RegistryHostResolver.Resolve(gitInternalBaseUrl));

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
    public void AnUnsetUrlFallsBackToTheComposeServiceName(string? gitInternalBaseUrl) =>
        Assert.Equal(RegistryHostResolver.DefaultRegistryHost, RegistryHostResolver.Resolve(gitInternalBaseUrl));
}
