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

    private string Root => options.Value.RootPath;

    public ValueTask<Result<IReadOnlyList<StarterPackageDto>>> ListAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(List());

    public ValueTask<Result<IReadOnlyList<StarterPackageEntryDto>>> BrowseAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Browse(relativePath));

    public ValueTask<Result<StarterPackageDownload>> PrepareDownloadAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(PrepareDownload(relativePath));

    public ValueTask<Result> DeleteAsync(string name, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Delete(name));

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
}
