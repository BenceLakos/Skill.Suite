namespace Skill.Suite.Application.Tests.StarterPackages;

using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Domain.Common;
using Xunit;

/// <summary>
/// Deleting a file or folder inside a package — and never the package, nor anything outside one.
/// </summary>
public sealed class FileSystemStarterPackageStoreDeleteEntryTests : IDisposable
{
    private const string Package = "fibonacci-session";
    private const string Folder = $"{Package}/competitor-start";

    private readonly TemporaryStarterPackagesVolume _volume = new();

    public void Dispose() => _volume.Dispose();

    [Fact]
    public async Task AFileIsDeleted()
    {
        await _volume.WriteAsync($"{Folder}/Program.cs", "class Program;");
        await _volume.WriteAsync($"{Folder}/README.md", "# Fibonacci");

        var result = await DeleteAsync($"{Folder}/Program.cs");

        Assert.True(result.IsSuccess);
        Assert.Equal(["README.md"], _volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AFolderIsDeletedWithEverythingInIt()
    {
        await _volume.WriteAsync($"{Folder}/src/Services/Fibonacci.cs", "class Fibonacci;");
        await _volume.WriteAsync($"{Folder}/src/Program.cs", "class Program;");

        var result = await DeleteAsync($"{Folder}/src");

        Assert.True(result.IsSuccess);
        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Fact]
    public async Task BuildOutputAShellPushBroughtInCanBeDeleted()
    {
        // Uploads leave it out, but it is exactly what an administrator needs to be able to clean up.
        await _volume.WriteAsync($"{Folder}/bin/Debug/Fibonacci.dll", "binary");

        var result = await DeleteAsync($"{Folder}/bin");

        Assert.True(result.IsSuccess);
        Assert.False(Directory.Exists(_volume.PathOf($"{Folder}/bin")));
    }

    [Fact]
    public async Task APackageItselfIsRefused()
    {
        _volume.CreateFolder(Folder);

        var result = await DeleteAsync(Package);

        Assert.Equal(StarterPackageErrors.EntryIsAPackage(Package), result.Error);
        Assert.True(Directory.Exists(_volume.PathOf(Folder)));
    }

    [Fact]
    public async Task SomethingThatIsNotThereIsNotFound()
    {
        _volume.CreateFolder(Folder);

        var result = await DeleteAsync($"{Folder}/Program.cs");

        Assert.Equal(StarterPackageErrors.NotFound($"{Folder}/Program.cs"), result.Error);
    }

    [Fact]
    public async Task SomethingInAPackageThatIsNotThereIsNotFound()
    {
        var result = await DeleteAsync("no-such-package/Program.cs");

        Assert.Equal(StarterPackageErrors.NotFound("no-such-package"), result.Error);
    }

    [Fact]
    public async Task AnUploadInProgressIsNotInAPackage()
    {
        // The store's own staging directory: dot-prefixed, so it is no package an administrator could open.
        var staging = $".{Package}.uploading-abcdef/competitor-start";
        _volume.CreateFolder(staging);

        var result = await DeleteAsync(staging);

        Assert.Equal(StarterPackageErrors.NotInAPackage, result.Error);
        Assert.True(Directory.Exists(_volume.PathOf(staging)));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../elsewhere")]
    [InlineData($"{Package}/../..")]
    [InlineData("/etc/passwd")]
    public async Task APathThatLeavesTheRootIsRefused(string relativePath)
    {
        _volume.CreateFolder(Folder);

        var result = await DeleteAsync(relativePath);

        Assert.Equal(StarterPackageErrors.InvalidPath, result.Error);
        Assert.True(Directory.Exists(_volume.PathOf(Folder)));
    }

    [Fact]
    public async Task AMissingRootIsReported()
    {
        _volume.Dispose();

        var result = await DeleteAsync($"{Folder}/Program.cs");

        Assert.Equal(StarterPackageErrors.RootUnavailable(_volume.Root), result.Error);
    }

    private async Task<Result> DeleteAsync(string relativePath) =>
        await _volume.CreateStore().DeleteEntryAsync(relativePath, CancellationToken.None);
}
