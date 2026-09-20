namespace Skill.Suite.Application.Tests.DockerImages;

using Skill.Suite.Application.DockerImages;
using Xunit;

/// <summary>
/// What the docker daemon is handed for a reference that was stored against the process view of the registry.
/// </summary>
/// <remarks>
/// Only the platform's own registry host is rewritten. Anything else is somebody's deliberate choice — a public
/// image, a registry that really is reachable by that name — and silently moving it to <c>localhost</c> would
/// point the pull at the wrong server.
/// </remarks>
public sealed class DaemonImageReferenceTests
{
    private const string InternalBaseUrl = "http://gitea:3000";

    [Fact]
    public void TheComposeServiceHostIsReplacedWithTheDaemonFacingOne() =>
        Assert.Equal(
            "localhost:3000/admin/x:1",
            DaemonImageReference.ForDaemon("gitea:3000/admin/x:1", InternalBaseUrl));

    [Fact]
    public void TheHostComparisonIgnoresCase() =>
        Assert.Equal(
            "localhost:3000/admin/x:1",
            DaemonImageReference.ForDaemon("GITEA:3000/admin/x:1", InternalBaseUrl));

    [Theory]
    [InlineData("localhost:3000/admin/x:1")]
    [InlineData("git.skills.local/admin/x:1")]
    [InlineData("postgres:17")]
    [InlineData("library/redis:8")]
    public void AnyOtherReferenceIsLeftAlone(string image) =>
        Assert.Equal(image, DaemonImageReference.ForDaemon(image, InternalBaseUrl));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithNoInternalUrlThereIsNothingToRecognise(string? gitInternalBaseUrl) =>
        Assert.Equal(
            "gitea:3000/admin/x:1",
            DaemonImageReference.ForDaemon("gitea:3000/admin/x:1", gitInternalBaseUrl));
}
