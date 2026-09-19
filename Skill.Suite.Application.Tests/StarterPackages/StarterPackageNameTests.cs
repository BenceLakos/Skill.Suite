using Skill.Suite.Application.StarterPackages;
using Xunit;

namespace Skill.Suite.Application.Tests.StarterPackages;

/// <summary>
/// The package names the UI accepts, which have to be the ones
/// <c>scripts/starter-packages.sh</c> accepts too — the two write into the same volume.
/// </summary>
public sealed class StarterPackageNameTests
{
    [Theory]
    [InlineData("fibonacci-session")]
    [InlineData("a")]
    [InlineData("0")]
    [InlineData("Task.01")]
    [InlineData("checkout_rules-2026.1")]
    public void AcceptsNamesTheShellScriptAccepts(string name) =>
        Assert.True(StarterPackageName.IsValid(name));

    [Theory]
    [InlineData("")]
    [InlineData(".hidden")]
    [InlineData("-leading")]
    [InlineData("_leading")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("with space")]
    [InlineData("café")]
    public void RejectsEverythingElse(string name) =>
        Assert.False(StarterPackageName.IsValid(name));

    [Fact]
    public void RejectsNull() => Assert.False(StarterPackageName.IsValid(null));

    [Fact]
    public void RejectsNamesLongerThanTheLimit() =>
        Assert.False(StarterPackageName.IsValid(new string('a', StarterPackageName.MaxLength + 1)));
}
