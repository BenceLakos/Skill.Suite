namespace Skill.Suite.Application.StarterPackages.ListStarterPackageSqlFiles;

using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

public sealed class ListStarterPackageSqlFilesHandler(IStarterPackageStore store)
    : IRequestHandler<ListStarterPackageSqlFilesQuery, Result<IReadOnlyList<StarterPackageEntryDto>>>
{
    public ValueTask<Result<IReadOnlyList<StarterPackageEntryDto>>> Handle(
        ListStarterPackageSqlFilesQuery request,
        CancellationToken cancellationToken) =>
        store.ListFilesAsync(SqlScriptFile.Extension, cancellationToken);
}
