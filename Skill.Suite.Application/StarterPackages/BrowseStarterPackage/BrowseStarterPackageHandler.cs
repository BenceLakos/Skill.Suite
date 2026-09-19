using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.BrowseStarterPackage;

public sealed class BrowseStarterPackageHandler(IStarterPackageStore store)
    : IRequestHandler<BrowseStarterPackageQuery, Result<IReadOnlyList<StarterPackageEntryDto>>>
{
    public ValueTask<Result<IReadOnlyList<StarterPackageEntryDto>>> Handle(
        BrowseStarterPackageQuery request,
        CancellationToken cancellationToken) =>
        store.BrowseAsync(request.RelativePath, cancellationToken);
}
