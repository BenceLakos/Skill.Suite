namespace Skill.Suite.Application.Tests.DockerImages;

using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.DockerImages;
using Xunit;

/// <summary>
/// How a registry package becomes the reference a docker daemon is asked to pull.
/// </summary>
/// <remarks>
/// Everything here is about references that would fail at pull time rather than at compose time. A reference
/// offered in a dropdown looks authoritative, so an unpullable one costs a competitor the run that discovers
/// it — which is why the untagged and incomplete cases are dropped from the list rather than rendered.
/// </remarks>
public sealed class RegistryImageReferenceTests
{
    private const string Host = "localhost:3000";

    [Fact]
    public void APackageBecomesHostOwnerNameAndTag() =>
        Assert.Equal(
            "localhost:3000/admin/judge-fibonacci:1.2.0",
            RegistryImageReference.Compose(Host, new ContainerPackage("admin", "judge-fibonacci", "1.2.0")));

    [Fact]
    public void OwnerAndNameAreLowerCased() =>
        // Registries reject uppercase path components and Gitea account names are free to contain them. The
        // tag is left alone: tags are case-sensitive, and lower-casing one invents a tag that does not exist.
        Assert.Equal(
            "localhost:3000/skill09/judge:Release-1",
            RegistryImageReference.Compose(Host, new ContainerPackage("Skill09", "Judge", "Release-1")));

    [Fact]
    public void AnUntaggedDigestIsNotOffered() =>
        // Every tagged image also appears under its digest, so offering these would double the list with
        // entries nobody picks an image by.
        Assert.Null(RegistryImageReference.Compose(
            Host,
            new ContainerPackage("admin", "judge", "sha256:9f2c1e4b8a0d")));

    [Theory]
    [InlineData("", "admin", "judge", "1.0")]
    [InlineData(Host, " ", "judge", "1.0")]
    [InlineData(Host, "admin", "", "1.0")]
    [InlineData(Host, "admin", "judge", "  ")]
    public void AnIncompleteReferenceIsNotOffered(string host, string owner, string name, string version) =>
        Assert.Null(RegistryImageReference.Compose(host, new ContainerPackage(owner, name, version)));

    [Fact]
    public void ATrailingSlashOnTheHostIsTolerated() =>
        // The host comes from configuration an operator types, and a trailing slash there would otherwise
        // produce a double slash that the daemon reads as an empty path component.
        Assert.Equal(
            "localhost:3000/admin/judge:1.0",
            RegistryImageReference.Compose("localhost:3000/", new ContainerPackage("admin", "judge", "1.0")));
}
