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
}
