using Skill.Suite.Application.StarterPackages;
using Xunit;

namespace Skill.Suite.Application.Tests.StarterPackages;

/// <summary>
/// The containment rule for every path this feature accepts from a browser.
/// </summary>
/// <remarks>
/// Worth asserting directly because the volume holds the starter kits of a live competition, and the only
/// thing between a crafted <c>?path=</c> and the rest of the container filesystem is this one method.
/// </remarks>
public sealed class StarterPackagePathTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "skill-suite-starter-packages");

    [Fact]
    public void RootItselfResolves()
    {
        Assert.True(StarterPackagePath.TryResolve(Root, string.Empty, out var resolved));
        Assert.Equal(Path.GetFullPath(Root), resolved);
    }

    [Theory]
    [InlineData("fibonacci-session")]
    [InlineData("fibonacci-session/competitor-start")]
    [InlineData("fibonacci-session/competitor-start/src/Program.cs")]
    [InlineData("a.b/c-d/e_f")]
    public void NestedRelativePathsResolveUnderTheRoot(string relativePath)
    {
        Assert.True(StarterPackagePath.TryResolve(Root, relativePath, out var resolved));
        Assert.StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, resolved, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../evil")]
    [InlineData("fibonacci-session/../../etc/passwd")]
    [InlineData("..\\evil")]
    [InlineData("fibonacci-session\\..\\..\\evil")]
    [InlineData("/etc/passwd")]
    [InlineData("a//b")]
    [InlineData("a/./b")]
    [InlineData("a/")]
    public void PathsThatLeaveTheRootAreRejected(string relativePath)
    {
        Assert.False(StarterPackagePath.TryResolve(Root, relativePath, out var resolved));
        Assert.Equal(string.Empty, resolved);
    }
}
