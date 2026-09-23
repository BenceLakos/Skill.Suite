namespace Skill.Suite.Application.StarterPackages.UploadStarterPackageFile;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>Writes one file into a folder of a package — one command per file of an upload.</summary>
/// <param name="FolderPath">
/// The folder being uploaded into, from the starter packages root; a package itself is a folder too.
/// </param>
/// <param name="RelativePath">
/// The file's path from that folder, separated by <c>/</c>: its name, or its path inside an uploaded folder,
/// whose missing folders are created on the way.
/// </param>
/// <param name="Content">Read once and left open for the caller to dispose.</param>
/// <param name="Overwrite">
/// Replaces an existing file — what the administrator confirmed when the plan answered
/// <see cref="StarterPackageUploadAction.Replace"/>.
/// </param>
public sealed record UploadStarterPackageFileCommand(
    string FolderPath,
    string RelativePath,
    Stream Content,
    bool Overwrite) : IRequest<Result>;
