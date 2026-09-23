using Skill.Suite.Application.StarterPackages;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Reads and writes the starter packages volume — the directory session authors put competitor starter
/// kits in, and that <c>Session.TemplateFolder</c> points into.
/// </summary>
/// <remarks>
/// Every member returns a <see cref="Result"/>: a missing volume, a read-only mount and a hostile archive
/// are all ordinary outcomes an administrator has to be told about, not exceptions.
/// </remarks>
public interface IStarterPackageStore
{
    ValueTask<Result<IReadOnlyList<StarterPackageDto>>> ListAsync(CancellationToken cancellationToken);

    ValueTask<Result<IReadOnlyList<StarterPackageEntryDto>>> BrowseAsync(
        string relativePath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every file with the given extension, in every package, at any depth.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="BrowseAsync"/> rather than a flag on it: browsing answers "what is in this
    /// folder" for a file manager, while this answers "which files of this kind exist at all" for a picker,
    /// and the two disagree about recursion, about the root and about what is worth hiding.
    /// </remarks>
    /// <param name="extension">Including the dot, e.g. <c>.sql</c>. Matched case-insensitively.</param>
    ValueTask<Result<IReadOnlyList<StarterPackageEntryDto>>> ListFilesAsync(
        string extension,
        CancellationToken cancellationToken);

    /// <summary>Reads one file's text, for something in the application to act on rather than download.</summary>
    ValueTask<Result<string>> ReadTextAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Imports a zip archive as the package <paramref name="name"/>, replacing it when asked to.</summary>
    /// <remarks>
    /// Replacing is a swap, not a merge — the same semantics as <c>scripts/starter-packages.sh push</c>, so a
    /// file deleted from the source does not linger in the package.
    /// </remarks>
    ValueTask<Result> ImportAsync(string name, Stream zipArchive, bool overwrite, CancellationToken cancellationToken);

    ValueTask<Result<StarterPackageDownload>> PrepareDownloadAsync(
        string relativePath,
        CancellationToken cancellationToken);

    ValueTask<Result> DeleteAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// What uploading <paramref name="files"/> into <paramref name="folderPath"/> would do with each of them,
    /// without writing anything.
    /// </summary>
    /// <remarks>
    /// Asked before a byte is sent, so replacements can be confirmed first and a batch that is too large is
    /// refused whole. The answer describes the folder as it was at that moment, which is why
    /// <see cref="WriteFileAsync"/> decides everything again for itself — by the same rules.
    /// </remarks>
    /// <param name="folderPath">An existing folder inside a package, or a package itself.</param>
    /// <returns>One item per file, in the order given.</returns>
    ValueTask<Result<IReadOnlyList<StarterPackageUploadPlanItemDto>>> PlanUploadAsync(
        string folderPath,
        IReadOnlyList<StarterPackageUploadFileDto> files,
        CancellationToken cancellationToken);

    /// <summary>Writes one file inside a package, creating any folder missing on the way to it.</summary>
    /// <remarks>
    /// The file takes its place only once all of it has arrived, so a failed or cancelled upload never leaves
    /// a truncated file where a session reads it. A browser sends no file mode, so a replacement keeps the
    /// mode of the file it replaces, and a new file that starts with <c>#!</c> is made executable.
    /// </remarks>
    /// <param name="relativePath">The file's path from the root, at least one level inside a package.</param>
    /// <param name="content">Read once and left open for the caller to dispose.</param>
    /// <param name="overwrite">Replaces an existing file; without it, one is refused and left as it was.</param>
    ValueTask<Result> WriteFileAsync(
        string relativePath,
        Stream content,
        bool overwrite,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates one folder inside an existing folder of a package — never the folders above it, and never a
    /// package.
    /// </summary>
    ValueTask<Result> CreateFolderAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Deletes a file, or a folder with everything in it, inside a package.</summary>
    /// <remarks>
    /// A package itself is refused: removing one is <see cref="DeleteAsync"/>, which the packages list offers.
    /// Unlike an upload, a path under build output or repository metadata may be deleted — that is how a
    /// stray <c>bin/</c> from a shell push gets cleaned up.
    /// </remarks>
    ValueTask<Result> DeleteEntryAsync(string relativePath, CancellationToken cancellationToken);
}
