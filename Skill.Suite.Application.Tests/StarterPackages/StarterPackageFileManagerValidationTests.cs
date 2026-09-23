namespace Skill.Suite.Application.Tests.StarterPackages;

using FluentValidation.Results;
using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Application.StarterPackages.CreateStarterPackageFolder;
using Skill.Suite.Application.StarterPackages.DeleteStarterPackageEntry;
using Skill.Suite.Application.StarterPackages.PlanStarterPackageUpload;
using Skill.Suite.Application.StarterPackages.UploadStarterPackageFile;
using Xunit;

/// <summary>
/// What the file manager's commands are refused for before they reach the volume.
/// </summary>
/// <remarks>
/// The path rules are the store's own, repeated only so a malformed request is answered without touching the
/// disk; what is asserted here is mostly what the validators add: a whole package refused by delete, and a
/// folder name judged the way the handler will use it.
/// </remarks>
public sealed class StarterPackageFileManagerValidationTests
{
    private const string Package = "fibonacci-session";
    private const string Folder = $"{Package}/competitor-start";

    [Theory]
    [InlineData(Package)]
    [InlineData(Folder)]
    public void APlanIntoAPackageOrAFolderInOneIsAccepted(string folderPath) =>
        Assert.True(Plan(folderPath, new StarterPackageUploadFileDto("src/Program.cs", 10)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("../elsewhere")]
    [InlineData(".fibonacci-session.uploading-abcdef")]
    public void APlanIntoSomethingThatIsNotInAPackageIsRejected(string folderPath)
    {
        var result = Plan(folderPath, new StarterPackageUploadFileDto("Program.cs", 10));

        Assert.Equal(StarterPackageErrors.NotInAPackage.Message, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void APlanNeedsAFile() => Assert.False(Plan(Folder).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("../Program.cs")]
    [InlineData("/etc/passwd")]
    [InlineData("src//Program.cs")]
    public void APlannedFileWhosePathLeavesTheFolderIsRejected(string relativePath)
    {
        var result = Plan(Folder,
            new StarterPackageUploadFileDto("Program.cs", 10),
            new StarterPackageUploadFileDto(relativePath, 10));

        Assert.Equal(StarterPackageErrors.InvalidPath.Message, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void APlannedFileCannotBeSmallerThanEmpty()
    {
        Assert.True(Plan(Folder, new StarterPackageUploadFileDto("empty.txt", 0)).IsValid);
        Assert.False(Plan(Folder, new StarterPackageUploadFileDto("negative.txt", -1)).IsValid);
    }

    [Fact]
    public void AnUploadedFileNeedsAFolderInAPackageASafePathAndContent()
    {
        using var content = new MemoryStream();

        Assert.True(Upload(Folder, "src/Program.cs", content).IsValid);
        Assert.False(Upload(string.Empty, "Program.cs", content).IsValid);
        Assert.False(Upload(Folder, "../Program.cs", content).IsValid);
        Assert.False(Upload(Folder, "Program.cs", null!).IsValid);
    }

    [Theory]
    [InlineData("assets")]
    [InlineData("  assets  ")]
    [InlineData("Új mappa (2)")]
    public void AFolderNameIsJudgedWithoutTheSpaceAroundIt(string name) =>
        Assert.True(CreateFolder(Folder, name).IsValid);

    [Theory]
    [InlineData("   ")]
    [InlineData(" .. ")]
    [InlineData("assets/images")]
    [InlineData("assets\\images")]
    public void AFolderNameThatIsNotASingleNameIsRejected(string name)
    {
        var result = CreateFolder(Folder, name);

        Assert.Equal(StarterPackageErrors.InvalidEntryName.Message, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void AFolderIsNotCreatedAtTheRoot()
    {
        var result = CreateFolder(string.Empty, "new-package");

        Assert.Equal(StarterPackageErrors.NotInAPackage.Message, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData($"{Folder}/Program.cs")]
    [InlineData($"{Folder}/bin")]
    public void DeletingAFileOrFolderInAPackageIsAccepted(string relativePath) =>
        Assert.True(Delete(relativePath).IsValid);

    [Fact]
    public void DeletingAPackageItselfIsRejectedWithWhereToDoItInstead()
    {
        var result = Delete(Package);

        Assert.Equal(StarterPackageErrors.EntryIsAPackage(Package).Message, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../elsewhere")]
    [InlineData(".fibonacci-session.uploading-abcdef/competitor-start")]
    public void DeletingSomethingThatIsNotInAPackageIsRejectedForThatAlone(string relativePath)
    {
        // Only the one reason: a path outside every package is not also told it is a whole package.
        var result = Delete(relativePath);

        Assert.Equal(StarterPackageErrors.NotInAPackage.Message, Assert.Single(result.Errors).ErrorMessage);
    }

    private static ValidationResult Plan(string folderPath, params StarterPackageUploadFileDto[] files) =>
        new PlanStarterPackageUploadValidator().Validate(new PlanStarterPackageUploadQuery(folderPath, files));

    private static ValidationResult Upload(string folderPath, string relativePath, Stream content) =>
        new UploadStarterPackageFileValidator().Validate(
            new UploadStarterPackageFileCommand(folderPath, relativePath, content, Overwrite: false));

    private static ValidationResult CreateFolder(string parentPath, string name) =>
        new CreateStarterPackageFolderValidator().Validate(new CreateStarterPackageFolderCommand(parentPath, name));

    private static ValidationResult Delete(string relativePath) =>
        new DeleteStarterPackageEntryValidator().Validate(new DeleteStarterPackageEntryCommand(relativePath));
}
