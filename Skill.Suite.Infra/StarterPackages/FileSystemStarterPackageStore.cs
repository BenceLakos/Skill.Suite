using System.Buffers;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Infra.StarterPackages;

internal sealed class FileSystemStarterPackageStore(
    IOptions<StarterPackagesOptions> options,
    ILogger<FileSystemStarterPackageStore> logger) : IStarterPackageStore
{
    private const string CompetitorStartFolderName = "competitor-start";
    private const string RootArchiveFileName = "starter-packages.zip";
    private const string ZipContentType = "application/zip";
    private const string FileContentType = "application/octet-stream";
    private const string ZipFileExtension = ".zip";

    /// <summary>Separator of the paths the container sees, regardless of the host this runs on.</summary>
    private const char ContainerSeparator = '/';

    /// <summary>
    /// Never imported and never offered for download. The same exclusions
    /// <see cref="Git.ProcessGitClient"/> applies when it seeds a competitor repository, plus the two
    /// folders a macOS or Windows author's zip tool adds on its own.
    /// </summary>
    private static readonly string[] SkippedSegments = [".git", "bin", "obj", ".vs", ".idea", "__MACOSX"];

    private const string SkippedFileName = ".DS_Store";

    /// <summary>Unix mode lives in the high half of a zip entry's external attributes.</summary>
    private const int ExternalAttributesModeShift = 16;

    /// <summary>
    /// Permission bits only. The setuid, setgid and sticky bits are dropped: a starter package is an
    /// untrusted upload, and nothing in one needs them.
    /// </summary>
    private const int UnixPermissionMask = 0x1FF;

    private const int FirstZipYear = 1980;

    /// <summary>
    /// Ends the name of the file an upload is written into until it is whole. An extension of its own rather
    /// than the target's, so the SQL picker never offers a script that is still arriving.
    /// </summary>
    private const string TemporaryFileExtension = ".uploading";

    /// <summary>The buffer <see cref="Stream.CopyToAsync(Stream)"/> uses by default.</summary>
    private const int CopyBufferSize = 81920;

    /// <summary>How a script announces itself on its first line.</summary>
    private static ReadOnlySpan<byte> Shebang => "#!"u8;

    private string Root => options.Value.RootPath;

    public ValueTask<Result<IReadOnlyList<StarterPackageDto>>> ListAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(List());

    public ValueTask<Result<IReadOnlyList<StarterPackageEntryDto>>> BrowseAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Browse(relativePath));

    public ValueTask<Result<IReadOnlyList<StarterPackageEntryDto>>> ListFilesAsync(
        string extension,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ListFiles(extension));

    public ValueTask<Result<string>> ReadTextAsync(string relativePath, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ReadText(relativePath));

    public ValueTask<Result<StarterPackageDownload>> PrepareDownloadAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(PrepareDownload(relativePath));

    public ValueTask<Result> DeleteAsync(string name, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Delete(name));

    public ValueTask<Result<IReadOnlyList<StarterPackageUploadPlanItemDto>>> PlanUploadAsync(
        string folderPath,
        IReadOnlyList<StarterPackageUploadFileDto> files,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(PlanUpload(folderPath, files));

    public ValueTask<Result> CreateFolderAsync(string relativePath, CancellationToken cancellationToken) =>
        ValueTask.FromResult(CreateFolder(relativePath));

    public ValueTask<Result> DeleteEntryAsync(string relativePath, CancellationToken cancellationToken) =>
        ValueTask.FromResult(DeleteEntry(relativePath));

    public async ValueTask<Result> ImportAsync(
        string name,
        Stream zipArchive,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var root = Root;
        if (!Directory.Exists(root))
            return Result.Failure(StarterPackageErrors.RootUnavailable(root));

        if (!StarterPackageName.IsValid(name))
            return Result.Failure(StarterPackageErrors.InvalidName);

        var target = Path.Combine(root, name);
        if (Directory.Exists(target) && !overwrite)
            return Result.Failure(StarterPackageErrors.AlreadyExists(name));

        // A browser upload arrives as a forward-only stream, and ZipArchive would answer that by buffering
        // the whole archive in memory before it could read the central directory. Spooling to a scratch file
        // keeps a half-gigabyte package out of the heap; the file deletes itself on dispose.
        Stream? spool = null;
        try
        {
            var seekable = zipArchive;
            if (!zipArchive.CanSeek)
            {
                spool = CreateSpoolFile();
                await zipArchive.CopyToAsync(spool, cancellationToken);
                spool.Position = 0;
                seekable = spool;
            }

            return await ImportFromAsync(root, name, target, seekable, cancellationToken);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Result.Failure(StarterPackageErrors.Failed(ex.Message));
        }
        finally
        {
            if (spool is not null)
                await spool.DisposeAsync();
        }
    }

    private async ValueTask<Result> ImportFromAsync(
        string root,
        string name,
        string target,
        Stream archiveStream,
        CancellationToken cancellationToken)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return Result.Failure(StarterPackageErrors.NotAZipArchive);
        }

        using (archive)
        {
            var plan = PlanExtraction(archive, options.Value.MaxUploadBytes);
            if (plan.IsFailure)
                return Result.Failure(plan.Error);

            // Assembled next to the package and moved into place, so a failed upload never leaves a
            // half-written package behind for a session to be started against. The leading dot keeps the
            // staging directory out of the packages list while it exists.
            var staging = Path.Combine(root, $".{name}.uploading-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(staging);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return Result.Failure(StarterPackageErrors.RootReadOnly(root));
            }

            try
            {
                foreach (var (entry, relativePath, isDirectory) in plan.Value)
                {
                    if (!StarterPackagePath.TryResolve(staging, relativePath, out var destination))
                        return Result.Failure(StarterPackageErrors.UnsafeArchiveEntry(entry.FullName));

                    if (isDirectory)
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                    await using (var source = entry.Open())
                    await using (var file = File.Create(destination))
                    {
                        await source.CopyToAsync(file, cancellationToken);
                    }

                    ApplyUnixFileMode(entry, destination);
                }

                if (Directory.Exists(target))
                    Directory.Delete(target, recursive: true);

                Directory.Move(staging, target);
                return Result.Success();
            }
            catch (UnauthorizedAccessException)
            {
                return Result.Failure(StarterPackageErrors.RootReadOnly(root));
            }
            catch (InvalidDataException)
            {
                return Result.Failure(StarterPackageErrors.NotAZipArchive);
            }
            catch (IOException ex)
            {
                return Result.Failure(StarterPackageErrors.Failed(ex.Message));
            }
            finally
            {
                RemoveStaging(staging);
            }
        }
    }

    public async ValueTask<Result> WriteFileAsync(
        string relativePath,
        Stream content,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var root = Root;
        var located = LocateInPackage(root, relativePath, _ => StarterPackageErrors.NotInAPackage);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var (target, packageDirectory, belowPackage) = located.Value;
        if (!AreValidNames(belowPackage))
            return Result.Failure(StarterPackageErrors.InvalidEntryName);

        var action = UploadActionFor(packageDirectory, belowPackage, target);

        Error? refusal = action switch
        {
            StarterPackageUploadAction.Excluded => StarterPackageErrors.ExcludedEntry(relativePath),
            StarterPackageUploadAction.Blocked => StarterPackageErrors.EntryKindConflict(relativePath),
            StarterPackageUploadAction.Replace when !overwrite => StarterPackageErrors.EntryAlreadyExists(relativePath),
            _ => null,
        };

        if (refusal is not null)
            return Result.Failure(refusal);

        return await WriteThroughTemporaryFileAsync(root, relativePath, target, content, overwrite, cancellationToken);
    }

    /// <summary>
    /// Streams an upload into a temporary file beside its target, and moves it into place once it is whole.
    /// </summary>
    /// <remarks>
    /// A session reads a package straight off the volume, so the target only ever holds a complete file: an
    /// upload that fails, runs over the limit or is cancelled stays in the temporary file, which is always
    /// removed. The temporary name is a fixed length, so a long file name cannot push it past the 255 bytes a
    /// name may take. The folders on the way are created first and stay if the file then fails — they are
    /// empty, and an empty folder is invisible to the git repository a competitor is seeded with.
    /// </remarks>
    private async ValueTask<Result> WriteThroughTemporaryFileAsync(
        string root,
        string relativePath,
        string target,
        Stream content,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var folder = Path.GetDirectoryName(target)!;
        var temporary = Path.Combine(folder, $".{Guid.NewGuid():N}{TemporaryFileExtension}");

        try
        {
            FileStream file;

            // The first thing an upload writes answers whether the volume can be written at all: the local
            // stack mounts it read-only, which surfaces as an IOException rather than an access error. A name
            // too long for the filesystem is the one IOException here that says nothing about the volume.
            try
            {
                Directory.CreateDirectory(folder);
                file = CreateTemporaryFile(temporary);
            }
            catch (PathTooLongException ex)
            {
                return Result.Failure(StarterPackageErrors.Failed(ex.Message));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return Result.Failure(StarterPackageErrors.RootReadOnly(root));
            }

            var limit = options.Value.MaxUploadBytes;
            (bool WithinLimit, bool StartsWithShebang) copied;
            await using (file)
            {
                copied = await CopyWithinLimitAsync(content, file, limit, cancellationToken);
            }

            if (!copied.WithinLimit)
                return Result.Failure(StarterPackageErrors.FileTooLarge(limit));

            ApplyUploadedFileMode(temporary, target, copied.StartsWithShebang);
            return MoveIntoPlace(temporary, target, relativePath, overwrite);
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Failure(StarterPackageErrors.RootReadOnly(root));
        }
        catch (IOException ex)
        {
            return Result.Failure(StarterPackageErrors.Failed(ex.Message));
        }
        finally
        {
            RemoveTemporaryFile(temporary);
        }
    }

    private Result<IReadOnlyList<StarterPackageDto>> List()
    {
        var root = Root;
        if (!Directory.Exists(root))
            return StarterPackageErrors.RootUnavailable(root);

        try
        {
            IReadOnlyList<StarterPackageDto> packages =
            [
                .. new DirectoryInfo(root)
                    .EnumerateDirectories()
                    .Where(directory => !directory.Name.StartsWith('.'))
                    .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(directory => new StarterPackageDto(
                        directory.Name,
                        TotalSize(directory),
                        directory.LastWriteTimeUtc,
                        Directory.Exists(Path.Combine(directory.FullName, CompetitorStartFolderName)),
                        TemplateFolderFor(root, directory.Name))),
            ];

            return Result.Success(packages);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return StarterPackageErrors.Failed(ex.Message);
        }
    }

    private Result<IReadOnlyList<StarterPackageEntryDto>> Browse(string relativePath)
    {
        var root = Root;
        if (!Directory.Exists(root))
            return StarterPackageErrors.RootUnavailable(root);

        if (!StarterPackagePath.TryResolve(root, relativePath, out var fullPath))
            return StarterPackageErrors.InvalidPath;

        if (!Directory.Exists(fullPath))
            return StarterPackageErrors.NotFound(relativePath);

        var prefix = relativePath.Length == 0
            ? string.Empty
            : relativePath.Replace('\\', ContainerSeparator) + ContainerSeparator;

        try
        {
            var directory = new DirectoryInfo(fullPath);

            IReadOnlyList<StarterPackageEntryDto> entries =
            [
                .. directory.EnumerateDirectories()
                    .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(child => new StarterPackageEntryDto(
                        child.Name, prefix + child.Name, true, TotalSize(child), child.LastWriteTimeUtc)),
                .. directory.EnumerateFiles()
                    .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(file => new StarterPackageEntryDto(
                        file.Name, prefix + file.Name, false, file.Length, file.LastWriteTimeUtc)),
            ];

            return Result.Success(entries);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return StarterPackageErrors.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Every file of one extension under the volume, whichever package and however deep it is.
    /// </summary>
    /// <remarks>
    /// The extension is matched here rather than handed to the enumerator as a glob: <c>*.sql</c> is
    /// case-sensitive on Linux and not on Windows, and on Windows a three-letter extension glob also matches
    /// longer ones. A picker whose contents depend on the host is worse than one extra pass over the names.
    /// </remarks>
    private Result<IReadOnlyList<StarterPackageEntryDto>> ListFiles(string extension)
    {
        var root = Root;
        if (!Directory.Exists(root))
            return StarterPackageErrors.RootUnavailable(root);

        try
        {
            IReadOnlyList<StarterPackageEntryDto> files =
            [
                .. new DirectoryInfo(root)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Select(file => (File: file, RelativePath: RelativePathOf(root, file.FullName)))
                    .Where(candidate =>
                        candidate.File.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                        && !IsSkipped(candidate.RelativePath)
                        && !IsInHiddenFolder(candidate.RelativePath))
                    .OrderBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(candidate => new StarterPackageEntryDto(
                        candidate.File.Name,
                        candidate.RelativePath,
                        false,
                        candidate.File.Length,
                        candidate.File.LastWriteTimeUtc)),
            ];

            return Result.Success(files);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return StarterPackageErrors.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Reads one file as text, decoding UTF-8 or the byte order mark the file carries.
    /// </summary>
    /// <remarks>
    /// The mark matters for the one caller there is: a script exported from SQL Server Management Studio is
    /// UTF-16, and read as UTF-8 it arrives as a statement with a null byte between every character.
    /// </remarks>
    private Result<string> ReadText(string relativePath)
    {
        var root = Root;
        if (!Directory.Exists(root))
            return StarterPackageErrors.RootUnavailable(root);

        if (!StarterPackagePath.IsSafeRelativePath(relativePath) ||
            !StarterPackagePath.TryResolve(root, relativePath, out var fullPath))
        {
            return StarterPackageErrors.InvalidPath;
        }

        if (!File.Exists(fullPath))
            return StarterPackageErrors.NotFound(relativePath);

        try
        {
            return Result.Success(File.ReadAllText(fullPath));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return StarterPackageErrors.Failed(ex.Message);
        }
    }

    private Result<StarterPackageDownload> PrepareDownload(string relativePath)
    {
        var root = Root;
        if (!Directory.Exists(root))
            return StarterPackageErrors.RootUnavailable(root);

        if (!StarterPackagePath.TryResolve(root, relativePath, out var fullPath))
            return StarterPackageErrors.InvalidPath;

        if (Directory.Exists(fullPath))
        {
            var fileName = relativePath.Length == 0
                ? RootArchiveFileName
                : LastSegment(relativePath) + ZipFileExtension;

            return Result.Success(new StarterPackageDownload(
                fileName,
                ZipContentType,
                (output, cancellationToken) => WriteZipAsync(fullPath, output, cancellationToken)));
        }

        if (File.Exists(fullPath))
            return Result.Success(new StarterPackageDownload(
                Path.GetFileName(fullPath),
                FileContentType,
                (output, cancellationToken) => CopyFileAsync(fullPath, output, cancellationToken)));

        return StarterPackageErrors.NotFound(relativePath);
    }

    private Result Delete(string name)
    {
        var root = Root;
        if (!Directory.Exists(root))
            return Result.Failure(StarterPackageErrors.RootUnavailable(root));

        if (!StarterPackageName.IsValid(name))
            return Result.Failure(StarterPackageErrors.InvalidName);

        var target = Path.Combine(root, name);
        if (!Directory.Exists(target))
            return Result.Failure(StarterPackageErrors.NotFound(name));

        try
        {
            Directory.Delete(target, recursive: true);
            return Result.Success();
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Failure(StarterPackageErrors.RootReadOnly(root));
        }
        catch (IOException ex)
        {
            return Result.Failure(StarterPackageErrors.Failed(ex.Message));
        }
    }

    /// <summary>
    /// What writing each file into the folder would do, decided file by file by the same rule the write
    /// applies — the plan resolves each path the way the upload's handler combines it.
    /// </summary>
    /// <remarks>
    /// The names are the one thing left to the write (see <see cref="AreValidNames"/>): none of the plan's
    /// answers means "that name cannot be written", so such a file is planned as a Create and refused on its
    /// own when it is sent.
    /// </remarks>
    private Result<IReadOnlyList<StarterPackageUploadPlanItemDto>> PlanUpload(
        string folderPath,
        IReadOnlyList<StarterPackageUploadFileDto> files)
    {
        var root = Root;
        var folder = LocateInPackage(root, folderPath, refusePackageItself: null);
        if (folder.IsFailure)
            return folder.Error;

        var (folderFullPath, packageDirectory, _) = folder.Value;
        if (!Directory.Exists(folderFullPath))
            return StarterPackageErrors.NotFound(folderPath);

        var plan = new List<StarterPackageUploadPlanItemDto>(files.Count);
        foreach (var file in files)
        {
            var relativePath = StarterPackagePath.Combine(folderPath, file.RelativePath);
            if (!StarterPackagePath.IsSafeRelativePath(file.RelativePath) ||
                !StarterPackagePath.TryResolve(root, relativePath, out var target))
            {
                return StarterPackageErrors.InvalidPath;
            }

            var action = UploadActionFor(
                packageDirectory, StarterPackagePath.SegmentsBelowPackage(relativePath), target);

            plan.Add(new StarterPackageUploadPlanItemDto(file.RelativePath, file.SizeBytes, action));
        }

        var limit = options.Value.MaxUploadBytes;
        if (ExceedsUploadLimit(plan, limit))
            return StarterPackageErrors.UploadTooLarge(limit);

        return Result.Success<IReadOnlyList<StarterPackageUploadPlanItemDto>>(plan);
    }

    private Result CreateFolder(string relativePath)
    {
        var root = Root;
        var located = LocateInPackage(root, relativePath, _ => StarterPackageErrors.NotInAPackage);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var (fullPath, _, belowPackage) = located.Value;
        if (!StarterPackageEntryName.IsValid(belowPackage[^1]))
            return Result.Failure(StarterPackageErrors.InvalidEntryName);

        if (IsSkipped(string.Join(ContainerSeparator, belowPackage)))
            return Result.Failure(StarterPackageErrors.ExcludedEntry(relativePath));

        // One level only: a parent that is not there is a folder the administrator is not looking at, and
        // creating it on their behalf would be a guess about a path they may have mistyped.
        if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
            return Result.Failure(StarterPackageErrors.NotFound(ParentOf(relativePath)));

        if (Directory.Exists(fullPath) || File.Exists(fullPath))
            return Result.Failure(StarterPackageErrors.EntryAlreadyExists(relativePath));

        try
        {
            Directory.CreateDirectory(fullPath);
            return Result.Success();
        }
        catch (PathTooLongException ex)
        {
            return Result.Failure(StarterPackageErrors.Failed(ex.Message));
        }
        catch (IOException) when (File.Exists(fullPath))
        {
            return Result.Failure(StarterPackageErrors.EntryAlreadyExists(relativePath));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Result.Failure(StarterPackageErrors.RootReadOnly(root));
        }
    }

    private Result DeleteEntry(string relativePath)
    {
        var root = Root;
        var located = LocateInPackage(root, relativePath, StarterPackageErrors.EntryIsAPackage);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var fullPath = located.Value.FullPath;
        try
        {
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
            else if (File.Exists(fullPath))
                File.Delete(fullPath);
            else
                return Result.Failure(StarterPackageErrors.NotFound(relativePath));

            return Result.Success();
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Failure(StarterPackageErrors.RootReadOnly(root));
        }
        catch (IOException ex)
        {
            return Result.Failure(StarterPackageErrors.Failed(ex.Message));
        }
    }

    /// <summary>
    /// The checks every change inside a package starts with, in the order an administrator can act on them:
    /// the volume is mounted, the path stays inside it and names a package, and that package exists.
    /// </summary>
    /// <param name="refusePackageItself">
    /// What a path naming the package itself, rather than something inside it, is refused with;
    /// <see langword="null"/> accepts it. Checked before the package's existence, because it is the request
    /// that is wrong, not the volume: a file directly in the root is not in a package, whether or not a
    /// package of that name happens to exist.
    /// </param>
    /// <returns>Where the path lands, its package's directory, and its segments below the package.</returns>
    private static Result<(string FullPath, string PackageDirectory, string[] BelowPackage)> LocateInPackage(
        string root,
        string relativePath,
        Func<string, Error>? refusePackageItself)
    {
        if (!Directory.Exists(root))
            return StarterPackageErrors.RootUnavailable(root);

        // The root itself: nothing is added to or removed from it here — that is what packages are for.
        if (string.IsNullOrWhiteSpace(relativePath))
            return StarterPackageErrors.NotInAPackage;

        if (!StarterPackagePath.IsSafeRelativePath(relativePath) ||
            !StarterPackagePath.TryResolve(root, relativePath, out var fullPath))
        {
            return StarterPackageErrors.InvalidPath;
        }

        if (StarterPackagePath.PackageOf(relativePath) is not { } package)
            return StarterPackageErrors.NotInAPackage;

        if (refusePackageItself is not null && !StarterPackagePath.IsBelowPackage(relativePath))
            return refusePackageItself(package);

        var packageDirectory = Path.Combine(Path.GetFullPath(root), package);
        if (!Directory.Exists(packageDirectory))
            return StarterPackageErrors.NotFound(package);

        return (fullPath, packageDirectory, StarterPackagePath.SegmentsBelowPackage(relativePath));
    }

    /// <summary>
    /// What writing a file at <paramref name="target"/> would do: the plan reports it, and the write acts on
    /// it.
    /// </summary>
    /// <remarks>
    /// One decision for both, so what the administrator confirmed is what the upload then does. The skip
    /// rule looks at the path below the package and not at the package's own name, which may well be
    /// <c>bin</c>; inside a <c>bin/</c> folder a shell push brought in, an upload is still left out, exactly as
    /// a download of the package would leave it out.
    /// </remarks>
    private static StarterPackageUploadAction UploadActionFor(
        string packageDirectory,
        string[] belowPackage,
        string target)
    {
        if (IsSkipped(string.Join(ContainerSeparator, belowPackage)))
            return StarterPackageUploadAction.Excluded;

        if (Directory.Exists(target) || IsFileInTheWay(packageDirectory, belowPackage))
            return StarterPackageUploadAction.Blocked;

        return File.Exists(target) ? StarterPackageUploadAction.Replace : StarterPackageUploadAction.Create;
    }

    /// <summary>
    /// Whether every name on the way to an upload is one <see cref="StarterPackageEntryName"/> accepts — the
    /// rule a folder created here is held to.
    /// </summary>
    /// <remarks>
    /// Asked by the write because an upload's names are not typed here: they come from the browser, as
    /// whatever the uploader's own filesystem allowed. A control character is refused for the same reason it
    /// is when a folder is created. A name past the 255 bytes Linux allows — a CJK file name from macOS, which
    /// counts characters rather than bytes — would otherwise reach the filesystem and come back as a
    /// <see cref="PathTooLongException"/>, reported as a failure; and a failure is what stops the rest of a
    /// batch, because it normally means the volume cannot be written at all. Refused here, before anything is
    /// written, the name is a validation error about this one file, and the rest of the upload goes on.
    /// </remarks>
    private static bool AreValidNames(IEnumerable<string> names) => names.All(StarterPackageEntryName.IsValid);

    /// <summary>
    /// Whether a file sits where one of the folders between the package and the target would have to be.
    /// </summary>
    private static bool IsFileInTheWay(string packageDirectory, string[] belowPackage)
    {
        var folder = packageDirectory;
        foreach (var segment in belowPackage.SkipLast(1))
        {
            folder = Path.Combine(folder, segment);
            if (File.Exists(folder))
                return true;

            // Nothing further down can exist, so nothing further down can be in the way.
            if (!Directory.Exists(folder))
                return false;
        }

        return false;
    }

    /// <summary>
    /// Whether the files an upload would write add up to more than one upload may be.
    /// </summary>
    /// <remarks>
    /// Only what would be written counts: an excluded or blocked file is never sent. Checked against what is
    /// left rather than summed into a total, because the sizes are the browser's to declare and a total of
    /// declared sizes can overflow; a negative one — the validator's to refuse — buys nothing.
    /// </remarks>
    private static bool ExceedsUploadLimit(IEnumerable<StarterPackageUploadPlanItemDto> plan, long limitBytes)
    {
        var remaining = limitBytes;
        foreach (var item in plan)
        {
            if (item.Action is not (StarterPackageUploadAction.Create or StarterPackageUploadAction.Replace))
                continue;

            if (item.SizeBytes > remaining)
                return true;

            remaining -= Math.Max(item.SizeBytes, 0);
        }

        return false;
    }

    /// <summary>
    /// Decides what an archive would put on disk, before a byte of it is written: what each entry's path
    /// under the package would be, what is dropped, and whether the whole archive is wrapped in one folder.
    /// </summary>
    private static Result<List<(ZipArchiveEntry Entry, string Path, bool IsDirectory)>> PlanExtraction(
        ZipArchive archive,
        long maxUploadBytes)
    {
        var kept = new List<(ZipArchiveEntry Entry, string Path, bool IsDirectory)>();
        var totalBytes = 0L;

        foreach (var entry in archive.Entries)
        {
            totalBytes += entry.Length;
            if (totalBytes > maxUploadBytes)
                return StarterPackageErrors.ArchiveTooLarge(maxUploadBytes);

            var normalized = entry.FullName.Replace('\\', ContainerSeparator);
            var isDirectory = normalized.EndsWith(ContainerSeparator);
            var path = isDirectory ? normalized.TrimEnd(ContainerSeparator) : normalized;

            if (!IsSafeEntryPath(path))
                return StarterPackageErrors.UnsafeArchiveEntry(entry.FullName);

            if (IsSkipped(path))
                continue;

            kept.Add((entry, path, isDirectory));
        }

        if (kept.All(candidate => candidate.IsDirectory))
            return StarterPackageErrors.EmptyArchive;

        var wrapper = SingleWrappingFolder(kept);
        if (wrapper is null)
            return kept;

        return kept
            .Where(candidate => candidate.Path.Length > wrapper.Length)
            .Select(candidate => (
                candidate.Entry,
                Path: candidate.Path[(wrapper.Length + 1)..],
                candidate.IsDirectory))
            .ToList();
    }

    /// <summary>
    /// The one top-level folder every entry sits under, if there is one — zipping a folder is the usual way
    /// an author produces the archive, and the folder name is theirs, not the package's.
    /// </summary>
    private static string? SingleWrappingFolder(List<(ZipArchiveEntry Entry, string Path, bool IsDirectory)> entries)
    {
        var candidate = FirstSegment(entries[0].Path);

        if (entries.Any(entry => !string.Equals(FirstSegment(entry.Path), candidate, StringComparison.Ordinal)))
            return null;

        // A file at the top level means the archive is the package, not a folder containing it.
        if (entries.Any(entry => !entry.IsDirectory && !entry.Path.Contains(ContainerSeparator)))
            return null;

        // Except for competitor-start: that folder is the package's own content, and stripping it would move
        // the starter files up a level and leave every session's TemplateFolder pointing at nothing.
        return string.Equals(candidate, CompetitorStartFolderName, StringComparison.OrdinalIgnoreCase)
            ? null
            : candidate;
    }

    private static bool IsSafeEntryPath(string path)
    {
        if (path.Length == 0 || path.StartsWith(ContainerSeparator) || Path.IsPathRooted(path))
            return false;

        return path.Split(ContainerSeparator)
            .All(segment => segment.Length > 0 && segment is not ("." or "..") && !segment.Contains('\0'));
    }

    private static string RelativePathOf(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace('\\', ContainerSeparator);

    /// <summary>
    /// Whether any folder on the way to the file is a hidden one.
    /// </summary>
    /// <remarks>
    /// The same rule the packages list applies to the top level, carried down the tree: an upload in progress
    /// stages itself in a dot-prefixed folder, and offering its half-written contents to be picked is worse
    /// than waiting for the upload to land.
    /// </remarks>
    private static bool IsInHiddenFolder(string relativePath)
    {
        var segments = relativePath.Split(ContainerSeparator);
        return segments[..^1].Any(segment => segment.StartsWith('.'));
    }

    private static bool IsSkipped(string relativePath)
    {
        var segments = relativePath.Split(ContainerSeparator);

        return segments.Any(segment => SkippedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase))
               || string.Equals(segments[^1], SkippedFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteZipAsync(string directory, Stream output, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(directory, file).Replace('\\', ContainerSeparator);
            if (IsSkipped(relativePath))
                continue;

            var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);

            // The zip timestamp format cannot express anything earlier, and assigning one throws.
            var lastWrite = File.GetLastWriteTime(file);
            if (lastWrite.Year >= FirstZipYear)
                entry.LastWriteTime = lastWrite;

            // Carried so a package survives a download/upload round trip with its shell scripts still
            // executable — a competitor's starter kit is useless if pack-contracts.sh comes back as data.
            if (!OperatingSystem.IsWindows())
                entry.ExternalAttributes =
                    ((int)File.GetUnixFileMode(file) & UnixPermissionMask) << ExternalAttributesModeShift;

            await using var source = File.OpenRead(file);
            await using var target = entry.Open();
            await source.CopyToAsync(target, cancellationToken);
        }
    }

    private static async Task CopyFileAsync(string path, Stream output, CancellationToken cancellationToken)
    {
        await using var source = File.OpenRead(path);
        await source.CopyToAsync(output, cancellationToken);
    }

    private static void ApplyUnixFileMode(ZipArchiveEntry entry, string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        var mode = (UnixFileMode)((entry.ExternalAttributes >> ExternalAttributesModeShift) & UnixPermissionMask);
        if (mode == UnixFileMode.None)
            return;

        File.SetUnixFileMode(path, mode);
    }

    private static FileStream CreateSpoolFile() =>
        new(Path.Combine(Path.GetTempPath(), $"starter-package-{Guid.NewGuid():N}{ZipFileExtension}"),
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
            });

    private void RemoveStaging(string staging)
    {
        if (!Directory.Exists(staging))
            return;

        try
        {
            Directory.Delete(staging, recursive: true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            logger.LogWarning(ex, "Could not remove the staging directory {Directory}", staging);
        }
    }

    /// <summary>
    /// Copies an upload, counting the bytes that actually arrive rather than trusting a declared length — a
    /// browser upload is forward-only and could not report one anyway.
    /// </summary>
    /// <returns>
    /// Whether it stayed within the limit — the copy stops at the first read that passes it — and whether
    /// it began with a shebang, noted as the bytes went by so the upload is never read twice.
    /// </returns>
    private static async ValueTask<(bool WithinLimit, bool StartsWithShebang)> CopyWithinLimitAsync(
        Stream source,
        Stream destination,
        long limitBytes,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            var head = new byte[Shebang.Length];
            var headLength = 0;
            var copied = 0L;

            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                copied += read;
                if (copied > limitBytes)
                    return (false, false);

                var taken = Math.Min(read, head.Length - headLength);
                buffer.AsSpan(0, taken).CopyTo(head.AsSpan(headLength));
                headLength += taken;

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            return (true, head.AsSpan(0, headLength).SequenceEqual(Shebang));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Gives an upload the mode it would have had if its author had put it there by hand.</summary>
    /// <remarks>
    /// A browser never sends a file's mode, and a starter kit is useless if its scripts arrive as data. So a
    /// replacement keeps the mode of the file it replaces — whoever put that file there decided it — and a
    /// new file that starts with <c>#!</c> becomes executable for everyone who may read it, 0644 turning into
    /// 0755. Permission bits only, as with an archive: setuid, setgid and sticky never come from an upload.
    /// </remarks>
    private static void ApplyUploadedFileMode(string temporary, string target, bool startsWithShebang)
    {
        if (OperatingSystem.IsWindows())
            return;

        if (File.Exists(target))
        {
            File.SetUnixFileMode(temporary, File.GetUnixFileMode(target) & (UnixFileMode)UnixPermissionMask);
            return;
        }

        if (!startsWithShebang)
            return;

        var created = File.GetUnixFileMode(temporary);
        File.SetUnixFileMode(temporary, created | ExecuteWhereReadable(created));
    }

    private static UnixFileMode ExecuteWhereReadable(UnixFileMode mode) =>
        (mode.HasFlag(UnixFileMode.UserRead) ? UnixFileMode.UserExecute : UnixFileMode.None)
        | (mode.HasFlag(UnixFileMode.GroupRead) ? UnixFileMode.GroupExecute : UnixFileMode.None)
        | (mode.HasFlag(UnixFileMode.OtherRead) ? UnixFileMode.OtherExecute : UnixFileMode.None);

    /// <summary>
    /// Puts a whole upload in its target's place, and names what got there first if something did while it
    /// was arriving.
    /// </summary>
    private static Result MoveIntoPlace(string temporary, string target, string relativePath, bool overwrite)
    {
        try
        {
            File.Move(temporary, target, overwrite);
            return Result.Success();
        }
        catch (IOException) when (Directory.Exists(target))
        {
            return Result.Failure(StarterPackageErrors.EntryKindConflict(relativePath));
        }
        catch (IOException) when (!overwrite && File.Exists(target))
        {
            return Result.Failure(StarterPackageErrors.EntryAlreadyExists(relativePath));
        }
    }

    private static FileStream CreateTemporaryFile(string path) =>
        new(path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            });

    private void RemoveTemporaryFile(string temporary)
    {
        if (!File.Exists(temporary))
            return;

        try
        {
            File.Delete(temporary);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            logger.LogWarning(ex, "Could not remove the temporary upload file {File}", temporary);
        }
    }

    private static long TotalSize(DirectoryInfo directory) =>
        directory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

    private static string TemplateFolderFor(string root, string name) =>
        string.Join(ContainerSeparator, root.TrimEnd(ContainerSeparator, '\\'), name, CompetitorStartFolderName);

    private static string FirstSegment(string path)
    {
        var separator = path.IndexOf(ContainerSeparator);
        return separator < 0 ? path : path[..separator];
    }

    private static string LastSegment(string path) =>
        path.Replace('\\', ContainerSeparator).Split(ContainerSeparator)[^1];

    private static string ParentOf(string path)
    {
        var normalized = path.Replace('\\', ContainerSeparator);
        return normalized[..normalized.LastIndexOf(ContainerSeparator)];
    }
}
