using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Domain.Common;
using Skill.Suite.Infra.StarterPackages;
using Xunit;

namespace Skill.Suite.Application.Tests.StarterPackages;

/// <summary>
/// What an uploaded archive is allowed to put on the starter packages volume, and what comes back out.
/// </summary>
/// <remarks>
/// Against a real temporary directory rather than an abstraction over the filesystem: the behaviour under
/// test is the filesystem's — path containment, a replace that is a swap rather than a merge, and the
/// executable bits a competitor's starter scripts need.
/// </remarks>
public sealed class FileSystemStarterPackageStoreTests : IDisposable
{
    private const string PackageName = "fibonacci-session";
    private const string CompetitorStart = "competitor-start";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"skill-suite-starter-packages-{Guid.NewGuid():N}");

    public FileSystemStarterPackageStoreTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ImportStripsASingleWrappingFolder()
    {
        var result = await ImportAsync(PackageName, overwrite: false,
            ($"{PackageName}/{CompetitorStart}/Program.cs", "class Program;"),
            ($"{PackageName}/README.md", "# Fibonacci"));

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(Path.Combine(_root, PackageName, CompetitorStart, "Program.cs")));
        Assert.True(File.Exists(Path.Combine(_root, PackageName, "README.md")));

        var packages = await CreateStore().ListAsync(CancellationToken.None);
        var package = Assert.Single(packages.Value);
        Assert.Equal(PackageName, package.Name);
        Assert.True(package.HasCompetitorStart);
        Assert.Equal($"{_root}/{PackageName}/{CompetitorStart}", package.TemplateFolder);
    }

    [Fact]
    public async Task ImportKeepsACompetitorStartFolderAtTheTopLevel()
    {
        // The one wrapping folder that is not a wrapper: stripping it would move the starter files up a
        // level and leave every session's TemplateFolder pointing at nothing.
        var result = await ImportAsync(PackageName, overwrite: false,
            ($"{CompetitorStart}/Program.cs", "class Program;"));

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(Path.Combine(_root, PackageName, CompetitorStart, "Program.cs")));
    }

    [Fact]
    public async Task ImportRejectsEntriesThatWouldEscapeThePackage()
    {
        var result = await ImportAsync(PackageName, overwrite: false,
            ("../evil.txt", "owned"),
            ($"{CompetitorStart}/Program.cs", "class Program;"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(StarterPackageErrors.UnsafeArchiveEntry("../evil.txt").Code, result.Error.Code);
        Assert.False(Directory.Exists(Path.Combine(_root, PackageName)));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_root)!, "evil.txt")));
    }

    [Fact]
    public async Task ImportOntoAnExistingPackageNeedsOverwrite()
    {
        await ImportAsync(PackageName, overwrite: false, ($"{CompetitorStart}/Program.cs", "first"));

        var result = await ImportAsync(PackageName, overwrite: false, ($"{CompetitorStart}/Program.cs", "second"));

        Assert.True(result.IsFailure);
        Assert.Equal(StarterPackageErrors.AlreadyExists(PackageName), result.Error);
    }

    [Fact]
    public async Task OverwriteReplacesRatherThanMerges()
    {
        await ImportAsync(PackageName, overwrite: false,
            ($"{CompetitorStart}/Old.cs", "old"),
            ($"{CompetitorStart}/Program.cs", "first"));

        var result = await ImportAsync(PackageName, overwrite: true,
            ($"{CompetitorStart}/Program.cs", "second"));

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(Path.Combine(_root, PackageName, CompetitorStart, "Old.cs")));
        Assert.Equal("second",
            await File.ReadAllTextAsync(Path.Combine(_root, PackageName, CompetitorStart, "Program.cs")));
    }

    [Fact]
    public async Task ImportLeavesOutBuildOutputAndRepositoryMetadata()
    {
        var result = await ImportAsync(PackageName, overwrite: false,
            ($"{PackageName}/bin/x.dll", "binary"),
            ($"{PackageName}/.git/config", "[core]"),
            ($"{PackageName}/{CompetitorStart}/obj/stale.cache", "stale"),
            ($"{PackageName}/{CompetitorStart}/.DS_Store", "finder"),
            ($"{PackageName}/{CompetitorStart}/Program.cs", "class Program;"));

        Assert.True(result.IsSuccess);
        Assert.False(Directory.Exists(Path.Combine(_root, PackageName, "bin")));
        Assert.False(Directory.Exists(Path.Combine(_root, PackageName, ".git")));
        Assert.False(Directory.Exists(Path.Combine(_root, PackageName, CompetitorStart, "obj")));
        Assert.False(File.Exists(Path.Combine(_root, PackageName, CompetitorStart, ".DS_Store")));
        Assert.True(File.Exists(Path.Combine(_root, PackageName, CompetitorStart, "Program.cs")));
    }

    [Fact]
    public async Task ImportRejectsAnArchiveWithNoFiles()
    {
        using var empty = new MemoryStream();
        using (var archive = new ZipArchive(empty, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry($"{PackageName}/");
        }

        empty.Position = 0;

        var result = await CreateStore().ImportAsync(PackageName, empty, overwrite: false, CancellationToken.None);

        Assert.Equal(StarterPackageErrors.EmptyArchive, result.Error);
    }

    [Fact]
    public async Task ImportRejectsSomethingThatIsNotAZip()
    {
        using var garbage = new MemoryStream("not a zip"u8.ToArray());

        var result = await CreateStore().ImportAsync(PackageName, garbage, overwrite: false, CancellationToken.None);

        Assert.Equal(StarterPackageErrors.NotAZipArchive, result.Error);
    }

    [Fact]
    public async Task ImportRejectsAnArchiveOverTheConfiguredLimit()
    {
        var store = CreateStore(maxUploadBytes: 8);
        using var archive = BuildZip(($"{CompetitorStart}/Program.cs", new string('x', 64)));

        var result = await store.ImportAsync(PackageName, archive, overwrite: false, CancellationToken.None);

        Assert.Equal(StarterPackageErrors.ArchiveTooLarge(8), result.Error);
    }

    [Fact]
    public async Task ImportAcceptsAForwardOnlyStream()
    {
        // A browser upload arrives forward-only, and a zip entry's stream is the closest thing the framework
        // offers: if the store depended on seeking, this is the test that would fail.
        var payload = BuildZip(($"{CompetitorStart}/Program.cs", "class Program;")).ToArray();

        using var envelope = new MemoryStream();
        using (var outer = new ZipArchive(envelope, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var target = outer.CreateEntry("upload.zip").Open();
            await target.WriteAsync(payload);
        }

        envelope.Position = 0;
        using var reader = new ZipArchive(envelope, ZipArchiveMode.Read);
        await using var forwardOnly = reader.Entries[0].Open();
        Assert.False(forwardOnly.CanSeek);

        var result = await CreateStore().ImportAsync(PackageName, forwardOnly, overwrite: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(Path.Combine(_root, PackageName, CompetitorStart, "Program.cs")));
    }

    [Fact]
    public async Task DownloadingAFolderZipsTheFilesUnderIt()
    {
        await ImportAsync(PackageName, overwrite: false,
            ($"{PackageName}/{CompetitorStart}/Program.cs", "class Program;"),
            ($"{PackageName}/{CompetitorStart}/src/Extra.cs", "class Extra;"),
            ($"{PackageName}/README.md", "# Fibonacci"));

        var prepared = await CreateStore()
            .PrepareDownloadAsync($"{PackageName}/{CompetitorStart}", CancellationToken.None);

        Assert.True(prepared.IsSuccess);
        Assert.Equal($"{CompetitorStart}.zip", prepared.Value.FileName);

        using var output = new MemoryStream();
        await prepared.Value.WriteToAsync(output, CancellationToken.None);
        output.Position = 0;

        using var archive = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.Equal(
            ["Program.cs", "src/Extra.cs"],
            archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ExecutableBitsSurviveTheRoundTrip()
    {
        if (OperatingSystem.IsWindows())
            return;

        const string script = $"{CompetitorStart}/pack-contracts.sh";
        var executable = (UnixFileMode)Convert.ToInt32("755", 8);

        using var upload = new MemoryStream();
        using (var archive = new ZipArchive(upload, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(script);
            entry.ExternalAttributes = (int)executable << 16;
            await using var writer = entry.Open();
            await writer.WriteAsync("#!/usr/bin/env bash\n"u8.ToArray());
        }

        upload.Position = 0;
        var store = CreateStore();
        Assert.True((await store.ImportAsync(PackageName, upload, overwrite: false, CancellationToken.None)).IsSuccess);

        // A competitor's starter kit is useless if its scripts come back as data.
        Assert.Equal(executable, File.GetUnixFileMode(Path.Combine(_root, PackageName, script)));

        var prepared = await store.PrepareDownloadAsync(PackageName, CancellationToken.None);
        using var output = new MemoryStream();
        await prepared.Value.WriteToAsync(output, CancellationToken.None);
        output.Position = 0;

        using var downloaded = new ZipArchive(output, ZipArchiveMode.Read);
        var roundTripped = Assert.Single(downloaded.Entries);
        Assert.Equal(script, roundTripped.FullName);
        Assert.Equal((int)executable, (roundTripped.ExternalAttributes >> 16) & 0x1FF);
    }

    [Fact]
    public async Task DownloadingTheRootTakesEverything()
    {
        await ImportAsync(PackageName, overwrite: false, ($"{CompetitorStart}/Program.cs", "class Program;"));

        var prepared = await CreateStore().PrepareDownloadAsync(string.Empty, CancellationToken.None);

        Assert.True(prepared.IsSuccess);
        Assert.Equal("starter-packages.zip", prepared.Value.FileName);

        using var output = new MemoryStream();
        await prepared.Value.WriteToAsync(output, CancellationToken.None);
        output.Position = 0;

        using var archive = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.Equal($"{PackageName}/{CompetitorStart}/Program.cs", Assert.Single(archive.Entries).FullName);
    }

    [Fact]
    public async Task DownloadingAFileServesItAsItIs()
    {
        await ImportAsync(PackageName, overwrite: false,
            ($"{CompetitorStart}/Program.cs", "class Program;"));

        var prepared = await CreateStore()
            .PrepareDownloadAsync($"{PackageName}/{CompetitorStart}/Program.cs", CancellationToken.None);

        Assert.True(prepared.IsSuccess);
        Assert.Equal("Program.cs", prepared.Value.FileName);

        using var output = new MemoryStream();
        await prepared.Value.WriteToAsync(output, CancellationToken.None);

        Assert.Equal("class Program;", System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task DownloadingSomethingThatIsNotThereIsNotFound()
    {
        var prepared = await CreateStore().PrepareDownloadAsync("no-such-package", CancellationToken.None);

        Assert.Equal(StarterPackageErrors.NotFound("no-such-package"), prepared.Error);
    }

    [Fact]
    public async Task DownloadingOutsideTheRootIsRefused()
    {
        var prepared = await CreateStore().PrepareDownloadAsync("../etc/passwd", CancellationToken.None);

        Assert.Equal(StarterPackageErrors.InvalidPath, prepared.Error);
    }

    [Fact]
    public async Task BrowseListsDirectoriesBeforeFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, PackageName, "zeta"));
        Directory.CreateDirectory(Path.Combine(_root, PackageName, "alpha"));
        await File.WriteAllTextAsync(Path.Combine(_root, PackageName, "b.txt"), "b");
        await File.WriteAllTextAsync(Path.Combine(_root, PackageName, "A.txt"), "a");

        var entries = await CreateStore().BrowseAsync(PackageName, CancellationToken.None);

        Assert.True(entries.IsSuccess);
        Assert.Equal(["alpha", "zeta", "A.txt", "b.txt"], entries.Value.Select(entry => entry.Name));
        Assert.Equal([true, true, false, false], entries.Value.Select(entry => entry.IsDirectory));
        Assert.Equal($"{PackageName}/alpha", entries.Value[0].RelativePath);
    }

    [Fact]
    public async Task StagingDirectoriesAreNotPackages()
    {
        Directory.CreateDirectory(Path.Combine(_root, $".{PackageName}.uploading-abcdef"));

        var packages = await CreateStore().ListAsync(CancellationToken.None);

        Assert.Empty(packages.Value);
    }

    [Fact]
    public async Task AMissingRootIsReportedRatherThanCreated()
    {
        var missing = Path.Combine(_root, "not-mounted");
        var store = CreateStore(root: missing);

        var packages = await store.ListAsync(CancellationToken.None);

        Assert.Equal(StarterPackageErrors.RootUnavailable(missing), packages.Error);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public async Task DeleteRemovesThePackage()
    {
        await ImportAsync(PackageName, overwrite: false, ($"{CompetitorStart}/Program.cs", "class Program;"));

        var result = await CreateStore().DeleteAsync(PackageName, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(Directory.Exists(Path.Combine(_root, PackageName)));
    }

    [Fact]
    public async Task DeleteOfAnUnknownPackageIsNotFound()
    {
        var result = await CreateStore().DeleteAsync("no-such-package", CancellationToken.None);

        Assert.Equal(StarterPackageErrors.NotFound("no-such-package"), result.Error);
    }

    [Fact]
    public async Task DeleteRefusesANameThatIsAPath()
    {
        var result = await CreateStore().DeleteAsync("../" + Path.GetFileName(_root), CancellationToken.None);

        Assert.Equal(StarterPackageErrors.InvalidName, result.Error);
        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public async Task ListFilesFindsTheExtensionAnywhereInAnyPackage()
    {
        // A seed script is as likely to live in a db/ or seed/ folder as beside the starter kit, and the
        // administrator picking one is choosing a script rather than walking a tree.
        await WriteAsync($"{PackageName}/seed/init.sql", "CREATE TABLE dbo.Orders (Id INT);");
        await WriteAsync($"{PackageName}/{CompetitorStart}/Program.cs", "class Program;");
        await WriteAsync("other-session/reset.SQL", "DELETE FROM dbo.Orders;");
        await WriteAsync("other-session/README.md", "# Other");

        var files = await CreateStore().ListFilesAsync(".sql", CancellationToken.None);

        Assert.True(files.IsSuccess);
        Assert.Equal(
            [$"{PackageName}/seed/init.sql", "other-session/reset.SQL"],
            files.Value.Select(file => file.RelativePath));
        Assert.Equal(["init.sql", "reset.SQL"], files.Value.Select(file => file.Name));
        Assert.All(files.Value, file => Assert.False(file.IsDirectory));
        Assert.All(files.Value, file => Assert.True(file.SizeBytes > 0));
    }

    [Fact]
    public async Task ListFilesLeavesOutBuildOutputAndUploadsInProgress()
    {
        await WriteAsync($"{PackageName}/seed/init.sql", "CREATE TABLE dbo.Orders (Id INT);");
        await WriteAsync($"{PackageName}/obj/generated.sql", "-- build output");
        await WriteAsync($"{PackageName}/.git/hooks/pre-commit.sql", "-- repository metadata");
        await WriteAsync($".{PackageName}.uploading-abcdef/seed/init.sql", "-- half an upload");

        var files = await CreateStore().ListFilesAsync(".sql", CancellationToken.None);

        Assert.Equal($"{PackageName}/seed/init.sql", Assert.Single(files.Value).RelativePath);
    }

    [Fact]
    public async Task ListFilesOnAnEmptyVolumeIsAnEmptyListRatherThanAnError()
    {
        var files = await CreateStore().ListFilesAsync(".sql", CancellationToken.None);

        Assert.True(files.IsSuccess);
        Assert.Empty(files.Value);
    }

    [Fact]
    public async Task ReadTextReturnsTheFileAsItWasWritten()
    {
        const string script = "CREATE TABLE dbo.Orders (Id INT);\nGO\nINSERT INTO dbo.Orders VALUES (1);\n";
        await WriteAsync($"{PackageName}/seed/init.sql", script);

        var text = await CreateStore().ReadTextAsync($"{PackageName}/seed/init.sql", CancellationToken.None);

        Assert.True(text.IsSuccess);
        Assert.Equal(script, text.Value);
    }

    [Fact]
    public async Task ReadTextDecodesAScriptExportedAsUtf16()
    {
        // What Management Studio writes by default. Read as UTF-8 it arrives as statements with a null byte
        // between every character, which the server rejects in a way that says nothing about the cause.
        const string script = "CREATE TABLE dbo.Orders (Id INT);";
        var path = Path.Combine(_root, PackageName, "seed");
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(
            Path.Combine(path, "init.sql"), script, new System.Text.UnicodeEncoding(false, true));

        var text = await CreateStore().ReadTextAsync($"{PackageName}/seed/init.sql", CancellationToken.None);

        Assert.Equal(script, text.Value);
    }

    [Fact]
    public async Task ReadTextOfSomethingThatIsNotThereIsNotFound()
    {
        var text = await CreateStore().ReadTextAsync($"{PackageName}/seed/init.sql", CancellationToken.None);

        Assert.Equal(StarterPackageErrors.NotFound($"{PackageName}/seed/init.sql"), text.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../etc/passwd")]
    [InlineData("a-package/../../etc/passwd")]
    [InlineData("/etc/passwd")]
    public async Task ReadTextOutsideTheRootIsRefused(string relativePath)
    {
        var text = await CreateStore().ReadTextAsync(relativePath, CancellationToken.None);

        Assert.Equal(StarterPackageErrors.InvalidPath, text.Error);
    }

    [Fact]
    public async Task ReadTextOfADirectoryIsNotAFile()
    {
        Directory.CreateDirectory(Path.Combine(_root, PackageName, "seed"));

        var text = await CreateStore().ReadTextAsync($"{PackageName}/seed", CancellationToken.None);

        Assert.Equal(StarterPackageErrors.NotFound($"{PackageName}/seed"), text.Error);
    }

    private async Task WriteAsync(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content);
    }

    private FileSystemStarterPackageStore CreateStore(string? root = null, long maxUploadBytes = 16 * 1024 * 1024) =>
        new(Options.Create(new StarterPackagesOptions { RootPath = root ?? _root, MaxUploadBytes = maxUploadBytes }),
            NullLogger<FileSystemStarterPackageStore>.Instance);

    private async Task<Result> ImportAsync(string name, bool overwrite, params (string Path, string Content)[] entries)
    {
        using var archive = BuildZip(entries);
        return await CreateStore().ImportAsync(name, archive, overwrite, CancellationToken.None);
    }

    private static MemoryStream BuildZip(params (string Path, string Content)[] entries)
    {
        var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(content);
            }
        }

        buffer.Position = 0;
        return buffer;
    }
}
