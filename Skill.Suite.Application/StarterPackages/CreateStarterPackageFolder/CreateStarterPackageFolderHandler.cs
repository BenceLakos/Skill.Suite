namespace Skill.Suite.Application.StarterPackages.CreateStarterPackageFolder;

using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

public sealed class CreateStarterPackageFolderHandler(IStarterPackageStore store)
    : IRequestHandler<CreateStarterPackageFolderCommand, Result>
{
    public ValueTask<Result> Handle(CreateStarterPackageFolderCommand request, CancellationToken cancellationToken) =>
        store.CreateFolderAsync(StarterPackagePath.Combine(request.ParentPath, request.Name.Trim()), cancellationToken);
}
