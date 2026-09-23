namespace Skill.Suite.Application.Tests.StarterPackages;

using Skill.Suite.Application.StarterPackages;
using Xunit;

/// <summary>
/// The names an administrator may give a file or folder inside a package — far looser than a package's own,
/// because the author's files may be called anything the volume can hold.
/// </summary>
public sealed class StarterPackageEntryNameTests
{
    [Theory]
    [InlineData("Program.cs")]
    [InlineData("pack-contracts.sh")]
    [InlineData("my notes.txt")]
    [InlineData("Új mappa (2)")]
    [InlineData(".gitignore")]
    [InlineData("...")]
    [InlineData("_leading-underscore")]
    public void AcceptsWhatLinuxAccepts(string name) =>
        Assert.True(StarterPackageEntryName.IsValid(name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a\0b")]
    [InlineData("line\nbreak")]
    [InlineData("\t")]
    [InlineData("delete\u007F")]
    public void RejectsWhatCannotBeASingleName(string name) =>
        Assert.False(StarterPackageEntryName.IsValid(name));

    [Fact]
    public void RejectsNull() => Assert.False(StarterPackageEntryName.IsValid(null));

    [Fact]
    public void AcceptsANameExactlyAtTheLimit() =>
        Assert.True(StarterPackageEntryName.IsValid(new string('a', StarterPackageEntryName.MaxLength)));

    [Fact]
    public void RejectsANameLongerThanTheLimit() =>
        Assert.False(StarterPackageEntryName.IsValid(new string('a', StarterPackageEntryName.MaxLength + 1)));

    [Fact]
    public void CountsTheLengthInBytesTheWayLinuxDoes()
    {
        // Two bytes each in UTF-8: few enough characters, too many bytes for one name on the volume.
        var accented = new string('é', StarterPackageEntryName.MaxLength / 2 + 1);

        Assert.True(accented.Length <= StarterPackageEntryName.MaxLength);
        Assert.False(StarterPackageEntryName.IsValid(accented));
    }
}
