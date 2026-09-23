namespace Skill.Suite.Application.Tests.StarterPackages;

using Skill.Suite.Application.StarterPackages;
using Xunit;

/// <summary>
/// How a folder being uploaded into and a file's path inside the upload become one path from the root.
/// </summary>
public sealed class StarterPackagePathCombineTests
{
    private const string Folder = "fibonacci-session/competitor-start";

    [Theory]
    [InlineData(Folder, "src/Program.cs", $"{Folder}/src/Program.cs")]
    [InlineData("fibonacci-session", "README.md", "fibonacci-session/README.md")]
    public void JoinsWithASlash(string folderPath, string relativePath, string expected) =>
        Assert.Equal(expected, StarterPackagePath.Combine(folderPath, relativePath));

    [Theory]
    [InlineData("fibonacci-session\\competitor-start", "src\\Program.cs")]
    [InlineData("fibonacci-session/competitor-start\\", "src/Program.cs")]
    public void NormalisesBackslashes(string folderPath, string relativePath) =>
        Assert.Equal($"{Folder}/src/Program.cs", StarterPackagePath.Combine(folderPath, relativePath));

    [Theory]
    [InlineData("fibonacci-session/", "README.md")]
    [InlineData("fibonacci-session", "/README.md")]
    [InlineData("fibonacci-session//", "//README.md")]
    [InlineData("fibonacci-session", "README.md/")]
    public void NeverDoublesOrEndsInASeparator(string folderPath, string relativePath) =>
        Assert.Equal("fibonacci-session/README.md", StarterPackagePath.Combine(folderPath, relativePath));

    [Theory]
    [InlineData("", "README.md", "README.md")]
    [InlineData("fibonacci-session", "", "fibonacci-session")]
    [InlineData("", "", "")]
    public void AnEmptyPartAddsNothing(string folderPath, string relativePath, string expected) =>
        Assert.Equal(expected, StarterPackagePath.Combine(folderPath, relativePath));

    [Fact]
    public void ATraversalSurvivesTheJoinForResolutionToRefuse()
    {
        // A join, not a check: the ".." stays where it was, so the path is refused rather than quietly turned
        // into a different one.
        var combined = StarterPackagePath.Combine("fibonacci-session", "../../etc/passwd");

        Assert.Equal("fibonacci-session/../../etc/passwd", combined);
        Assert.False(StarterPackagePath.IsSafeRelativePath(combined));
    }
}
