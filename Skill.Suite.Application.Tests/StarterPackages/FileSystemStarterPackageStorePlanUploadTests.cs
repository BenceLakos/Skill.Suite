namespace Skill.Suite.Application.Tests.StarterPackages;

using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Domain.Common;
using Xunit;

/// <summary>
/// What an upload into a folder of a package would do, answered before any of its bytes move.
/// </summary>
/// <remarks>
/// The plan is what the administrator confirms, so it is held to the decisions a write makes — and to
/// writing nothing itself.
/// </remarks>
public sealed class FileSystemStarterPackageStorePlanUploadTests : IDisposable
{
    private const string Package = "fibonacci-session";
    private const string Folder = $"{Package}/competitor-start";

    private readonly TemporaryStarterPackagesVolume _volume = new();

    public void Dispose() => _volume.Dispose();

    [Fact]
    public async Task ANewFileIsCreatedAndAnExistingOneReplaced()
    {
        await _volume.WriteAsync($"{Folder}/Program.cs", "class Program;");

        var plan = await PlanAsync(Folder, ("Program.cs", 20), ("Extra.cs", 10));

        Assert.True(plan.IsSuccess);
        Assert.Equal(
            [StarterPackageUploadAction.Replace, StarterPackageUploadAction.Create],
            plan.Value.Select(item => item.Action));
    }

    [Fact]
    public async Task AFileInFoldersThatDoNotExistYetIsCreatedWithoutCreatingThem()
    {
        _volume.CreateFolder(Folder);

        var plan = await PlanAsync(Folder, ("src/Services/Fibonacci.cs", 10));

        Assert.Equal(StarterPackageUploadAction.Create, Assert.Single(plan.Value).Action);
        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Theory]
    [InlineData("bin/Debug/Fibonacci.dll")]
    [InlineData("obj/project.assets.json")]
    [InlineData(".git/config")]
    [InlineData(".vs/settings.json")]
    [InlineData(".idea/workspace.xml")]
    [InlineData("__MACOSX/._Program.cs")]
    [InlineData("src/.DS_Store")]
    public async Task BuildOutputRepositoryMetadataAndFinderFilesAreExcluded(string relativePath)
    {
        _volume.CreateFolder(Folder);

        var plan = await PlanAsync(Folder, (relativePath, 10));

        Assert.Equal(StarterPackageUploadAction.Excluded, Assert.Single(plan.Value).Action);
    }

    [Fact]
    public async Task TheSkipRuleLooksBelowThePackageRatherThanAtItsName()
    {
        // A package may be called bin; and inside a bin/ folder a shell push brought in, an upload is left out
        // exactly as a download of the package would leave it out.
        _volume.CreateFolder("bin/competitor-start");
        _volume.CreateFolder($"{Package}/bin");

        var intoAPackageCalledBin = await PlanAsync("bin/competitor-start", ("Program.cs", 10));
        var intoABinFolder = await PlanAsync($"{Package}/bin", ("Program.cs", 10));

        Assert.Equal(StarterPackageUploadAction.Create, Assert.Single(intoAPackageCalledBin.Value).Action);
        Assert.Equal(StarterPackageUploadAction.Excluded, Assert.Single(intoABinFolder.Value).Action);
    }

    [Fact]
    public async Task AFolderWhereTheFileWouldGoBlocksIt()
    {
        _volume.CreateFolder($"{Folder}/src");

        var plan = await PlanAsync(Folder, ("src", 10));

        Assert.Equal(StarterPackageUploadAction.Blocked, Assert.Single(plan.Value).Action);
    }

    [Fact]
    public async Task AFileWhereOneOfItsFoldersWouldGoBlocksIt()
    {
        await _volume.WriteAsync($"{Folder}/README.md", "# Fibonacci");

        var plan = await PlanAsync(Folder, ("README.md/notes.txt", 10), ("README.md/drafts/notes.txt", 10));

        Assert.All(plan.Value, item => Assert.Equal(StarterPackageUploadAction.Blocked, item.Action));
    }

    [Fact]
    public async Task ANameTheVolumeCannotHoldIsLeftToTheWriteToRefuse()
    {
        // None of the plan's answers means "that name cannot be written": it is planned as a Create, and the
        // write refuses it on its own — a problem with that one file rather than with the whole upload.
        _volume.CreateFolder(Folder);

        var plan = await PlanAsync(Folder, (new string('中', 86), 10), ("bell\u0007.txt", 10));

        Assert.All(plan.Value, item => Assert.Equal(StarterPackageUploadAction.Create, item.Action));
    }

    [Fact]
    public async Task EveryFileIsAnsweredInTheOrderItWasAskedAbout()
    {
        _volume.CreateFolder(Folder);

        var plan = await PlanAsync(Folder, ("zeta.txt", 3), ("alpha.txt", 1), ("middle/beta.txt", 2));

        Assert.Equal(["zeta.txt", "alpha.txt", "middle/beta.txt"], plan.Value.Select(item => item.RelativePath));
        Assert.Equal([3L, 1L, 2L], plan.Value.Select(item => item.SizeBytes));
    }

    [Fact]
    public async Task APackageItselfIsAFolderToUploadInto()
    {
        _volume.CreateFolder(Package);

        var plan = await PlanAsync(Package, ("README.md", 10));

        Assert.Equal(StarterPackageUploadAction.Create, Assert.Single(plan.Value).Action);
    }

    [Fact]
    public async Task OnlyFilesThatWouldBeWrittenCountTowardsTheLimit()
    {
        await _volume.WriteAsync($"{Folder}/Program.cs", "class Program;");
        _volume.CreateFolder($"{Folder}/src");

        var plan = await PlanAsync(10, Folder,
            ("Extra.cs", 6), ("Program.cs", 4), ("bin/Fibonacci.dll", 1000), ("src", 1000));

        Assert.True(plan.IsSuccess);
        Assert.Equal(
            [
                StarterPackageUploadAction.Create,
                StarterPackageUploadAction.Replace,
                StarterPackageUploadAction.Excluded,
                StarterPackageUploadAction.Blocked,
            ],
            plan.Value.Select(item => item.Action));
    }

    [Fact]
    public async Task AnUploadOverTheLimitIsRefusedAsAWhole()
    {
        await _volume.WriteAsync($"{Folder}/Program.cs", "class Program;");

        var plan = await PlanAsync(10, Folder, ("Extra.cs", 6), ("Program.cs", 5));

        Assert.Equal(StarterPackageErrors.UploadTooLarge(10), plan.Error);
    }

    [Fact]
    public async Task DeclaredSizesTooLargeToAddUpAreStillTooLarge()
    {
        // The sizes are the browser's to declare: a total that overflowed would come out small, or throw.
        _volume.CreateFolder(Folder);

        var plan = await PlanAsync(Folder, ("a.bin", long.MaxValue), ("b.bin", long.MaxValue));

        Assert.Equal(
            StarterPackageErrors.UploadTooLarge(TemporaryStarterPackagesVolume.DefaultMaxUploadBytes),
            plan.Error);
    }

    [Fact]
    public async Task AFolderThatIsNotThereIsNotFound()
    {
        _volume.CreateFolder(Package);

        var plan = await PlanAsync($"{Package}/missing", ("a.txt", 10));

        Assert.Equal(StarterPackageErrors.NotFound($"{Package}/missing"), plan.Error);
    }

    [Fact]
    public async Task AFileIsNotAFolderToUploadInto()
    {
        await _volume.WriteAsync($"{Package}/README.md", "# Fibonacci");

        var plan = await PlanAsync($"{Package}/README.md", ("a.txt", 10));

        Assert.Equal(StarterPackageErrors.NotFound($"{Package}/README.md"), plan.Error);
    }

    [Fact]
    public async Task AFolderOfAPackageThatIsNotThereIsNotFound()
    {
        var plan = await PlanAsync("no-such-package/competitor-start", ("a.txt", 10));

        Assert.Equal(StarterPackageErrors.NotFound("no-such-package"), plan.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData($".{Package}.uploading-abcdef")]
    public async Task NeitherTheRootNorAnUploadInProgressIsInAPackage(string folderPath)
    {
        _volume.CreateFolder(folderPath);

        var plan = await PlanAsync(folderPath, ("a.txt", 10));

        Assert.Equal(StarterPackageErrors.NotInAPackage, plan.Error);
    }

    [Theory]
    [InlineData("../elsewhere")]
    [InlineData($"{Package}/../../elsewhere")]
    [InlineData("/etc")]
    public async Task AFolderOutsideTheRootIsRefused(string folderPath)
    {
        var plan = await PlanAsync(folderPath, ("a.txt", 10));

        Assert.Equal(StarterPackageErrors.InvalidPath, plan.Error);
    }

    [Theory]
    [InlineData("../../evil.txt")]
    [InlineData("src/../../../evil.txt")]
    [InlineData("/etc/passwd")]
    public async Task AFilePathThatLeavesTheFolderIsRefused(string relativePath)
    {
        _volume.CreateFolder(Folder);

        var plan = await PlanAsync(Folder, ("fine.txt", 10), (relativePath, 10));

        Assert.Equal(StarterPackageErrors.InvalidPath, plan.Error);
    }

    [Fact]
    public async Task AMissingRootIsReportedRatherThanCreated()
    {
        _volume.Dispose();

        var plan = await PlanAsync(Folder, ("a.txt", 10));

        Assert.Equal(StarterPackageErrors.RootUnavailable(_volume.Root), plan.Error);
        Assert.False(Directory.Exists(_volume.Root));
    }

    private Task<Result<IReadOnlyList<StarterPackageUploadPlanItemDto>>> PlanAsync(
        string folderPath,
        params (string RelativePath, long SizeBytes)[] files) =>
        PlanAsync(TemporaryStarterPackagesVolume.DefaultMaxUploadBytes, folderPath, files);

    private async Task<Result<IReadOnlyList<StarterPackageUploadPlanItemDto>>> PlanAsync(
        long maxUploadBytes,
        string folderPath,
        params (string RelativePath, long SizeBytes)[] files) =>
        await _volume.CreateStore(maxUploadBytes).PlanUploadAsync(
            folderPath,
            [.. files.Select(file => new StarterPackageUploadFileDto(file.RelativePath, file.SizeBytes))],
            CancellationToken.None);
}
