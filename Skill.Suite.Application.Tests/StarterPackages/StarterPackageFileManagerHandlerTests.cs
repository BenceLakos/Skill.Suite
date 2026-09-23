namespace Skill.Suite.Application.Tests.StarterPackages;

using System.Text;
using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Application.StarterPackages.CreateStarterPackageFolder;
using Skill.Suite.Application.StarterPackages.PlanStarterPackageUpload;
using Skill.Suite.Application.StarterPackages.UploadStarterPackageFile;
using Xunit;

/// <summary>
/// The one thing the file manager's handlers do themselves: turn the folder the administrator is browsing and
/// what they chose into the path the store writes.
/// </summary>
public sealed class StarterPackageFileManagerHandlerTests : IDisposable
{
    private const string Folder = "fibonacci-session/competitor-start";

    private readonly TemporaryStarterPackagesVolume _volume = new();

    public void Dispose() => _volume.Dispose();

    [Fact]
    public async Task AnUploadedFileLandsAtItsPathInsideTheFolder()
    {
        _volume.CreateFolder(Folder);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("class Program;"));

        var result = await new UploadStarterPackageFileHandler(_volume.CreateStore()).Handle(
            new UploadStarterPackageFileCommand(Folder, "src/Program.cs", content, Overwrite: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("class Program;", await _volume.ReadAsync($"{Folder}/src/Program.cs"));
    }

    [Fact]
    public async Task ThePlanAndTheUploadResolveAFileToTheSamePlace()
    {
        // What the administrator confirmed has to be what gets written, down to which file is replaced.
        await _volume.WriteAsync($"{Folder}/src/Program.cs", "original");
        var store = _volume.CreateStore();

        var plan = await new PlanStarterPackageUploadHandler(store).Handle(
            new PlanStarterPackageUploadQuery(Folder, [new StarterPackageUploadFileDto("src/Program.cs", 11)]),
            CancellationToken.None);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("replacement"));
        var upload = await new UploadStarterPackageFileHandler(store).Handle(
            new UploadStarterPackageFileCommand(Folder, "src/Program.cs", content, Overwrite: true),
            CancellationToken.None);

        Assert.Equal(StarterPackageUploadAction.Replace, Assert.Single(plan.Value).Action);
        Assert.True(upload.IsSuccess);
        Assert.Equal("replacement", await _volume.ReadAsync($"{Folder}/src/Program.cs"));
    }

    [Fact]
    public async Task ANewFolderIsNamedWithoutTheSpaceAroundIt()
    {
        _volume.CreateFolder(Folder);

        var result = await new CreateStarterPackageFolderHandler(_volume.CreateStore()).Handle(
            new CreateStarterPackageFolderCommand(Folder, "  assets  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["assets"], _volume.NamesIn(Folder));
    }
}
