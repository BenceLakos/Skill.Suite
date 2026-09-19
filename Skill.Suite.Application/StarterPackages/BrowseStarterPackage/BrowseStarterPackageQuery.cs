using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.BrowseStarterPackage;

/// <param name="RelativePath">Path from the starter packages root; empty lists the packages themselves.</param>
public sealed record BrowseStarterPackageQuery(string RelativePath)
    : IRequest<Result<IReadOnlyList<StarterPackageEntryDto>>>;
