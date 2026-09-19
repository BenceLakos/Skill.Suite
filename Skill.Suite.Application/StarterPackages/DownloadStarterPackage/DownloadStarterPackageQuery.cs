using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.DownloadStarterPackage;

/// <param name="RelativePath">
/// A package, a folder inside one — both served as a zip — or a single file, served as it is. Empty takes
/// the whole starter packages directory.
/// </param>
public sealed record DownloadStarterPackageQuery(string RelativePath) : IRequest<Result<StarterPackageDownload>>;
