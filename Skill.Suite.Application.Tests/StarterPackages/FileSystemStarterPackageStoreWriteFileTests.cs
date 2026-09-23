namespace Skill.Suite.Application.Tests.StarterPackages;

using System.Text;
using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Domain.Common;
using Xunit;

/// <summary>
/// What one file uploaded into a package leaves on the volume — and what a failed one does not.
/// </summary>
/// <remarks>
/// A session reads its starter kit straight off the volume, so the assertions that matter most are about
/// what is not there afterwards: no truncated file, no temporary copy, nothing outside the root.
/// </remarks>
public sealed class FileSystemStarterPackageStoreWriteFileTests : IDisposable
{
    private const string Package = "fibonacci-session";
    private const string Folder = $"{Package}/competitor-start";
    private const string Script = $"{Folder}/pack-contracts.sh";
    private const string ScriptContent = "#!/usr/bin/env bash\necho packing\n";

    private const UnixFileMode AnyExecute =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    // Three bytes each in UTF-8: 85 of them are exactly the 255 bytes Linux allows one name, 86 are 258.
    // macOS counts characters instead, which is how a name like the second comes to be uploaded at all.
    private static readonly string NameAtTheLimit = new('中', 85);
    private static readonly string NamePastTheLimit = new('中', 86);

    private static readonly UnixFileMode Executable = (UnixFileMode)Convert.ToInt32("755", 8);
    private static readonly UnixFileMode ReadWrite = (UnixFileMode)Convert.ToInt32("644", 8);
    private static readonly UnixFileMode ExecutableWithSetUserId = (UnixFileMode)Convert.ToInt32("4755", 8);

    private readonly TemporaryStarterPackagesVolume _volume = new();

    public void Dispose() => _volume.Dispose();

    [Fact]
    public async Task ANewFileIsWrittenWithTheFoldersOnTheWayToIt()
    {
        _volume.CreateFolder(Folder);

        var result = await WriteAsync($"{Folder}/src/Services/Fibonacci.cs", "class Fibonacci;");

        Assert.True(result.IsSuccess);
        Assert.Equal("class Fibonacci;", await _volume.ReadAsync($"{Folder}/src/Services/Fibonacci.cs"));
    }

    [Fact]
    public async Task NothingButTheFileIsLeftAfterAWrite()
    {
        _volume.CreateFolder(Folder);

        await WriteAsync($"{Folder}/Program.cs", "class Program;");

        Assert.Equal(["Program.cs"], _volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AnExistingFileIsRefusedWithoutOverwriteAndLeftAsItWas()
    {
        await _volume.WriteAsync($"{Folder}/Program.cs", "original");

        var result = await WriteAsync($"{Folder}/Program.cs", "replacement");

        Assert.Equal(StarterPackageErrors.EntryAlreadyExists($"{Folder}/Program.cs"), result.Error);
        Assert.Equal("original", await _volume.ReadAsync($"{Folder}/Program.cs"));
        Assert.Equal(["Program.cs"], _volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AnExistingFileIsReplacedWithOverwrite()
    {
        await _volume.WriteAsync($"{Folder}/Program.cs", "original");

        var result = await WriteAsync($"{Folder}/Program.cs", "replacement", overwrite: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("replacement", await _volume.ReadAsync($"{Folder}/Program.cs"));
        Assert.Equal(["Program.cs"], _volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AForwardOnlyUploadIsWrittenWhole()
    {
        // A browser upload can neither seek nor say how long it is, and arrives in whatever pieces the
        // circuit delivers.
        _volume.CreateFolder(Folder);
        var content = Encoding.UTF8.GetBytes(new string('x', 1000));
        await using var upload = new ForwardOnlyStream(content, chunkSize: 7);

        var result = await _volume.CreateStore()
            .WriteFileAsync($"{Folder}/data.txt", upload, overwrite: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(content, await File.ReadAllBytesAsync(_volume.PathOf($"{Folder}/data.txt")));
    }

    [Fact]
    public async Task ANewScriptIsExecutableForWhoeverMayReadIt()
    {
        if (OperatingSystem.IsWindows())
            return;

        _volume.CreateFolder(Folder);

        // One byte at a time, so the shebang has to be recognised across reads rather than within the first.
        await using (var upload = new ForwardOnlyStream(Encoding.UTF8.GetBytes(ScriptContent), chunkSize: 1))
        {
            Assert.True((await _volume.CreateStore()
                .WriteFileAsync(Script, upload, overwrite: false, CancellationToken.None)).IsSuccess);
        }

        // A heading is not a shebang: the first two bytes decide, not the first one.
        Assert.True((await WriteAsync($"{Folder}/README.md", "# Fibonacci\n")).IsSuccess);

        var plain = File.GetUnixFileMode(_volume.PathOf($"{Folder}/README.md"));
        var script = File.GetUnixFileMode(_volume.PathOf(Script));

        // Whatever the umask made of a new file — 0644 under the usual one, which the script turns into 0755.
        Assert.Equal(UnixFileMode.None, plain & AnyExecute);
        Assert.Equal(plain, script & ~AnyExecute);
        Assert.Equal(plain.HasFlag(UnixFileMode.UserRead), script.HasFlag(UnixFileMode.UserExecute));
        Assert.Equal(plain.HasFlag(UnixFileMode.GroupRead), script.HasFlag(UnixFileMode.GroupExecute));
        Assert.Equal(plain.HasFlag(UnixFileMode.OtherRead), script.HasFlag(UnixFileMode.OtherExecute));
    }

    [Fact]
    public async Task AReplacementKeepsTheModeOfTheFileItReplaces()
    {
        if (OperatingSystem.IsWindows())
            return;

        await _volume.WriteAsync(Script, ScriptContent);
        File.SetUnixFileMode(_volume.PathOf(Script), Executable);

        // No shebang this time: the mode is the replaced file's, not a guess made from the new content.
        var result = await WriteAsync(Script, "echo packing differently\n", overwrite: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(Executable, File.GetUnixFileMode(_volume.PathOf(Script)));
    }

    [Fact]
    public async Task AReplacementKeepsANonExecutableModeEvenForAScript()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Whoever put the file there decided its mode; an upload has none of its own to set against it.
        await _volume.WriteAsync(Script, "echo packing\n");
        File.SetUnixFileMode(_volume.PathOf(Script), ReadWrite);

        var result = await WriteAsync(Script, ScriptContent, overwrite: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReadWrite, File.GetUnixFileMode(_volume.PathOf(Script)));
    }

    [Fact]
    public async Task AReplacementNeverCarriesSpecialBits()
    {
        if (OperatingSystem.IsWindows())
            return;

        await _volume.WriteAsync(Script, ScriptContent);
        File.SetUnixFileMode(_volume.PathOf(Script), ExecutableWithSetUserId);

        var result = await WriteAsync(Script, ScriptContent, overwrite: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(Executable, File.GetUnixFileMode(_volume.PathOf(Script)));
    }

    [Fact]
    public async Task AFolderWhereTheFileWouldGoIsAConflict()
    {
        _volume.CreateFolder($"{Folder}/src");

        var result = await WriteAsync($"{Folder}/src", "class Program;", overwrite: true);

        Assert.Equal(StarterPackageErrors.EntryKindConflict($"{Folder}/src"), result.Error);
        Assert.True(Directory.Exists(_volume.PathOf($"{Folder}/src")));
    }

    [Fact]
    public async Task AFileWhereOneOfItsFoldersWouldGoIsAConflict()
    {
        await _volume.WriteAsync($"{Folder}/README.md", "# Fibonacci");

        var result = await WriteAsync($"{Folder}/README.md/drafts/notes.txt", "notes", overwrite: true);

        Assert.Equal(StarterPackageErrors.EntryKindConflict($"{Folder}/README.md/drafts/notes.txt"), result.Error);
        Assert.Equal("# Fibonacci", await _volume.ReadAsync($"{Folder}/README.md"));
    }

    [Theory]
    [InlineData($"{Folder}/bin/Debug/Fibonacci.dll")]
    [InlineData($"{Folder}/src/.DS_Store")]
    [InlineData($"{Package}/.git/config")]
    public async Task BuildOutputRepositoryMetadataAndFinderFilesAreNotWritten(string relativePath)
    {
        _volume.CreateFolder(Folder);

        var result = await WriteAsync(relativePath, "left out");

        Assert.Equal(StarterPackageErrors.ExcludedEntry(relativePath), result.Error);
        Assert.False(File.Exists(_volume.PathOf(relativePath)));
    }

    [Fact]
    public async Task AFileNamePastTheBytesLinuxAllowsIsRefusedBeforeAnythingIsWritten()
    {
        // Left to the filesystem it would fail like a volume that cannot be written, which stops the rest of
        // an upload; refused here, it is a problem with this one file.
        _volume.CreateFolder(Folder);
        Assert.True(Encoding.UTF8.GetByteCount(NamePastTheLimit) > StarterPackageEntryName.MaxLength);

        var result = await WriteAsync($"{Folder}/{NamePastTheLimit}", "content");

        Assert.Equal(StarterPackageErrors.InvalidEntryName, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AFolderNamePastTheBytesLinuxAllowsIsRefusedWithoutCreatingIt()
    {
        _volume.CreateFolder(Folder);

        var result = await WriteAsync($"{Folder}/{NamePastTheLimit}/Program.cs", "class Program;");

        Assert.Equal(StarterPackageErrors.InvalidEntryName, result.Error);
        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AFileNameExactlyAtTheBytesLinuxAllowsIsWritten()
    {
        _volume.CreateFolder(Folder);
        Assert.Equal(StarterPackageEntryName.MaxLength, Encoding.UTF8.GetByteCount(NameAtTheLimit));

        var result = await WriteAsync($"{Folder}/{NameAtTheLimit}", "content");

        Assert.True(result.IsSuccess);
        Assert.Equal("content", await _volume.ReadAsync($"{Folder}/{NameAtTheLimit}"));
        Assert.Equal([NameAtTheLimit], _volume.NamesIn(Folder));
    }

    [Theory]
    [InlineData($"{Folder}/bell\u0007.txt")]
    [InlineData($"{Folder}/line\nbreak.txt")]
    [InlineData($"{Folder}/tab\tfolder/Program.cs")]
    public async Task ANameWithAControlCharacterIsRefused(string relativePath)
    {
        // The rule a folder created here is held to, applied to the names an upload brings with it.
        _volume.CreateFolder(Folder);

        var result = await WriteAsync(relativePath, "content");

        Assert.Equal(StarterPackageErrors.InvalidEntryName, result.Error);
        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("notes and ideas.txt")]
    [InlineData(Package)]
    public async Task AFileDirectlyInTheRootIsNotInAPackage(string relativePath)
    {
        // Named like a package or not, and whether or not that package exists: this is not a way to make one.
        _volume.CreateFolder(Package);

        var result = await WriteAsync(relativePath, "stray");

        Assert.Equal(StarterPackageErrors.NotInAPackage, result.Error);
        Assert.False(File.Exists(_volume.PathOf(relativePath)));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData($"{Package}/../../evil.txt")]
    [InlineData("/tmp/evil.txt")]
    public async Task APathThatLeavesTheRootIsRefused(string relativePath)
    {
        _volume.CreateFolder(Package);

        var result = await WriteAsync(relativePath, "owned");

        Assert.Equal(StarterPackageErrors.InvalidPath, result.Error);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_volume.Root)!, "evil.txt")));
    }

    [Fact]
    public async Task AFileInAPackageThatIsNotThereIsNotFound()
    {
        var result = await WriteAsync("no-such-package/competitor-start/Program.cs", "class Program;");

        Assert.Equal(StarterPackageErrors.NotFound("no-such-package"), result.Error);
        Assert.False(Directory.Exists(_volume.PathOf("no-such-package")));
    }

    [Fact]
    public async Task AFileOverTheLimitIsRefusedAndLeavesNothingBehind()
    {
        // Counted as it arrives: a forward-only upload has no length to check up front.
        _volume.CreateFolder(Folder);
        await using var upload = new ForwardOnlyStream(new byte[64], chunkSize: 4);

        var result = await _volume.CreateStore(maxUploadBytes: 8)
            .WriteFileAsync($"{Folder}/big.bin", upload, overwrite: false, CancellationToken.None);

        Assert.Equal(StarterPackageErrors.FileTooLarge(8), result.Error);
        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AFileExactlyAtTheLimitIsWritten()
    {
        _volume.CreateFolder(Folder);
        await using var upload = new ForwardOnlyStream(new byte[8], chunkSize: 3);

        var result = await _volume.CreateStore(maxUploadBytes: 8)
            .WriteFileAsync($"{Folder}/exact.bin", upload, overwrite: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, new FileInfo(_volume.PathOf($"{Folder}/exact.bin")).Length);
    }

    [Fact]
    public async Task ACancelledUploadLeavesNeitherTheFileNorItsTemporaryCopy()
    {
        _volume.CreateFolder(Folder);
        using var cancellation = new CancellationTokenSource();
        await using var upload = new ForwardOnlyStream(new byte[64], chunkSize: 8, afterRead: sent =>
        {
            if (sent >= 16)
                cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _volume.CreateStore()
            .WriteFileAsync($"{Folder}/Program.cs", upload, overwrite: false, cancellation.Token)
            .AsTask());

        Assert.Empty(_volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AFileThatAppearsWhileTheUploadArrivesIsNotOverwritten()
    {
        _volume.CreateFolder(Folder);
        var target = _volume.PathOf($"{Folder}/Program.cs");
        await using var upload = new ForwardOnlyStream("class Mine;"u8.ToArray(), chunkSize: 4, afterRead: _ =>
        {
            if (!File.Exists(target))
                File.WriteAllText(target, "class Theirs;");
        });

        var result = await _volume.CreateStore()
            .WriteFileAsync($"{Folder}/Program.cs", upload, overwrite: false, CancellationToken.None);

        Assert.Equal(StarterPackageErrors.EntryAlreadyExists($"{Folder}/Program.cs"), result.Error);
        Assert.Equal("class Theirs;", await File.ReadAllTextAsync(target));
        Assert.Equal(["Program.cs"], _volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AFolderThatAppearsWhileTheUploadArrivesIsAConflict()
    {
        _volume.CreateFolder(Folder);
        var target = _volume.PathOf($"{Folder}/src");
        await using var upload = new ForwardOnlyStream("class Mine;"u8.ToArray(), chunkSize: 4, afterRead: _ =>
            Directory.CreateDirectory(target));

        var result = await _volume.CreateStore()
            .WriteFileAsync($"{Folder}/src", upload, overwrite: true, CancellationToken.None);

        Assert.Equal(StarterPackageErrors.EntryKindConflict($"{Folder}/src"), result.Error);
        Assert.True(Directory.Exists(target));
        Assert.Equal(["src"], _volume.NamesIn(Folder));
    }

    [Fact]
    public async Task AMissingRootIsReportedRatherThanCreated()
    {
        _volume.Dispose();

        var result = await WriteAsync($"{Folder}/Program.cs", "class Program;");

        Assert.Equal(StarterPackageErrors.RootUnavailable(_volume.Root), result.Error);
        Assert.False(Directory.Exists(_volume.Root));
    }

    private async Task<Result> WriteAsync(string relativePath, string content, bool overwrite = false)
    {
        using var upload = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return await _volume.CreateStore().WriteFileAsync(relativePath, upload, overwrite, CancellationToken.None);
    }
}
