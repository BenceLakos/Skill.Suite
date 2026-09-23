namespace Skill.Suite.Application.Tests.StarterPackages;

using Skill.Suite.Application.StarterPackages;
using Xunit;

/// <summary>
/// Which package a path is in, and whether it names the package or something inside it — the one rule the
/// validators and the store both ask before anything in a package is changed.
/// </summary>
public sealed class StarterPackagePathPackageTests
{
    [Theory]
    [InlineData("fibonacci-session", "fibonacci-session")]
    [InlineData("fibonacci-session/competitor-start/src/Program.cs", "fibonacci-session")]
    [InlineData("Task.01\\competitor-start", "Task.01")]
    public void ThePackageIsTheFirstSegment(string relativePath, string expected)
    {
        Assert.Equal(expected, StarterPackagePath.PackageOf(relativePath));
        Assert.True(StarterPackagePath.IsInPackage(relativePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".fibonacci-session.uploading-abcdef/competitor-start")]
    [InlineData("with space/competitor-start")]
    [InlineData("-leading/competitor-start")]
    [InlineData("../fibonacci-session")]
    [InlineData("fibonacci-session/../../etc")]
    [InlineData("/fibonacci-session")]
    [InlineData("fibonacci-session//competitor-start")]
    [InlineData("fibonacci-session/")]
    public void APathThatIsUnsafeOrDoesNotStartWithAPackageIsInNone(string relativePath)
    {
        Assert.Null(StarterPackagePath.PackageOf(relativePath));
        Assert.False(StarterPackagePath.IsInPackage(relativePath));
        Assert.False(StarterPackagePath.IsBelowPackage(relativePath));
        Assert.Empty(StarterPackagePath.SegmentsBelowPackage(relativePath));
    }

    [Fact]
    public void NullIsInNoPackage()
    {
        Assert.Null(StarterPackagePath.PackageOf(null));
        Assert.False(StarterPackagePath.IsInPackage(null));
        Assert.False(StarterPackagePath.IsBelowPackage(null));
    }

    [Fact]
    public void APackageItselfIsInAPackageButNotBelowOne()
    {
        Assert.True(StarterPackagePath.IsInPackage("fibonacci-session"));
        Assert.False(StarterPackagePath.IsBelowPackage("fibonacci-session"));
        Assert.Empty(StarterPackagePath.SegmentsBelowPackage("fibonacci-session"));
    }

    [Theory]
    [InlineData("fibonacci-session/README.md", new[] { "README.md" })]
    [InlineData("fibonacci-session/competitor-start/src/Program.cs", new[] { "competitor-start", "src", "Program.cs" })]
    [InlineData("fibonacci-session\\competitor-start\\bin", new[] { "competitor-start", "bin" })]
    public void AnythingInsideAPackageIsBelowIt(string relativePath, string[] expected)
    {
        Assert.True(StarterPackagePath.IsBelowPackage(relativePath));
        Assert.Equal(expected, StarterPackagePath.SegmentsBelowPackage(relativePath));
    }
}
