namespace Skill.Suite.Application.Tests.StarterPackages;

using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Domain.Common;
using Xunit;

/// <summary>
/// One new folder inside a package: where it may go, what it may be called, and what it never replaces.
/// </summary>
public sealed class FileSystemStarterPackageStoreCreateFolderTests : IDisposable
{
    private const string Package = "fibonacci-session";
    private const string Folder = $"{Package}/competitor-start";

    private readonly TemporaryStarterPackagesVolume _volume = new();

    public void Dispose() => _volume.Dispose();

    [Fact]
    public async Task AFolderIsCreatedInsideAnExistingOne()
    {
        _volume.CreateFolder(Folder);

        var result = await CreateFolderAsync($"{Folder}/assets");

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(_volume.PathOf($"{Folder}/assets")));
    }

    [Fact]
    public async Task AFolderIsCreatedDirectlyInAPackage()
    {
        _volume.CreateFolder(Package);

        var result = await CreateFolderAsync($"{Package}/seed");

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(_volume.PathOf($"{Package}/seed")));
    }

    [Fact]
    public async Task ANameMayHaveSpacesAccentsAndParentheses()
    {
        // Not a package name: what is inside a package is the author's own, and may be called anything Linux
        // accepts.
        _volume.CreateFolder(Folder);

        var result = await CreateFolderAsync($"{Folder}/Új mappa (2)");

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(_volume.PathOf($"{Folder}/Új mappa (2)")));
    }

    [Fact]
    public async Task AFolderThatIsAlreadyThereIsRefused()
    {
        _volume.CreateFolder($"{Folder}/assets");

        var result = await CreateFolderAsync($"{Folder}/assets");

        Assert.Equal(StarterPackageErrors.EntryAlreadyExists($"{Folder}/assets"), result.Error);
    }

    [Fact]
    public async Task AFileByThatNameIsRefusedAndLeftAsItWas()
    {
        await _volume.WriteAsync($"{Folder}/README.md", "# Fibonacci");

        var result = await CreateFolderAsync($"{Folder}/README.md");

        Assert.Equal(StarterPackageErrors.EntryAlreadyExists($"{Folder}/README.md"), result.Error);
        Assert.Equal("# Fibonacci", await _volume.ReadAsync($"{Folder}/README.md"));
    }

    [Fact]
    public async Task AMissingParentIsNotFoundAndNotCreated()
    {
        // One level only: a parent that is not there is not the folder the administrator is looking at.
        _volume.CreateFolder(Package);

        var result = await CreateFolderAsync($"{Package}/missing/assets");

        Assert.Equal(StarterPackageErrors.NotFound($"{Package}/missing"), result.Error);
        Assert.False(Directory.Exists(_volume.PathOf($"{Package}/missing")));
    }

    [Fact]
    public async Task AFileIsNotAParent()
    {
        await _volume.WriteAsync($"{Package}/README.md", "# Fibonacci");

        var result = await CreateFolderAsync($"{Package}/README.md/assets");

        Assert.Equal(StarterPackageErrors.NotFound($"{Package}/README.md"), result.Error);
    }

    [Theory]
    [InlineData($"{Folder}/tab\there")]
    [InlineData($"{Folder}/bell\u0007")]
    public async Task ANameWithAControlCharacterIsRefused(string relativePath)
    {
        _volume.CreateFolder(Folder);

        var result = await CreateFolderAsync(relativePath);

        Assert.Equal(StarterPackageErrors.InvalidEntryName, result.Error);
        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Fact]
    public async Task ANameLongerThanTheFilesystemAllowsIsRefused()
    {
        _volume.CreateFolder(Folder);

        var result = await CreateFolderAsync($"{Folder}/{new string('a', StarterPackageEntryName.MaxLength + 1)}");

        Assert.Equal(StarterPackageErrors.InvalidEntryName, result.Error);
        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Theory]
    [InlineData($"{Folder}/bin")]
    [InlineData($"{Folder}/src/obj")]
    [InlineData($"{Package}/.git")]
    [InlineData($"{Folder}/__MACOSX")]
    public async Task ANameThatPackagesLeaveOutIsRefused(string relativePath)
    {
        _volume.CreateFolder($"{Folder}/src");

        var result = await CreateFolderAsync(relativePath);

        Assert.Equal(StarterPackageErrors.ExcludedEntry(relativePath), result.Error);
        Assert.False(Directory.Exists(_volume.PathOf(relativePath)));
    }

    [Theory]
    [InlineData("new-package")]
    [InlineData("")]
    public async Task APackageIsNotCreatedHere(string relativePath)
    {
        var result = await CreateFolderAsync(relativePath);

        Assert.Equal(StarterPackageErrors.NotInAPackage, result.Error);
        Assert.Empty(_volume.NamesIn(string.Empty));
    }

    [Fact]
    public async Task AFolderInAPackageThatIsNotThereIsNotFound()
    {
        var result = await CreateFolderAsync("no-such-package/assets");

        Assert.Equal(StarterPackageErrors.NotFound("no-such-package"), result.Error);
        Assert.False(Directory.Exists(_volume.PathOf("no-such-package")));
    }

    [Theory]
    [InlineData("../elsewhere")]
    [InlineData($"{Package}/../../elsewhere")]
    [InlineData($"{Package}/..")]
    public async Task APathThatLeavesTheRootIsRefused(string relativePath)
    {
        _volume.CreateFolder(Package);

        var result = await CreateFolderAsync(relativePath);

        Assert.Equal(StarterPackageErrors.InvalidPath, result.Error);
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(_volume.Root)!, "elsewhere")));
    }

    [Fact]
    public async Task AMissingRootIsReportedRatherThanCreated()
    {
        _volume.Dispose();

        var result = await CreateFolderAsync($"{Folder}/assets");

        Assert.Equal(StarterPackageErrors.RootUnavailable(_volume.Root), result.Error);
        Assert.False(Directory.Exists(_volume.Root));
    }

    private async Task<Result> CreateFolderAsync(string relativePath) =>
        await _volume.CreateStore().CreateFolderAsync(relativePath, CancellationToken.None);
}
