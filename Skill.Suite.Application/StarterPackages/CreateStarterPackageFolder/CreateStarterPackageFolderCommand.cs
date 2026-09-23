namespace Skill.Suite.Application.StarterPackages.CreateStarterPackageFolder;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>Creates one folder inside an existing folder of a package.</summary>
/// <param name="ParentPath">
/// The existing folder, from the starter packages root; a package itself is a folder too.
/// </param>
/// <param name="Name">The new folder's name as the administrator typed it; surrounding whitespace is dropped.</param>
public sealed record CreateStarterPackageFolderCommand(string ParentPath, string Name) : IRequest<Result>;
