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
