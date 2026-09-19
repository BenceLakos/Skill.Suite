using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.ListStarterPackages;

public sealed class ListStarterPackagesHandler(IStarterPackageStore store)
    : IRequestHandler<ListStarterPackagesQuery, Result<IReadOnlyList<StarterPackageDto>>>
{
    public ValueTask<Result<IReadOnlyList<StarterPackageDto>>> Handle(
        ListStarterPackagesQuery request,
        CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);
}
