namespace Skill.Suite.Application.StarterPackages.DeleteStarterPackageEntry;

using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

public sealed class DeleteStarterPackageEntryHandler(IStarterPackageStore store)
    : IRequestHandler<DeleteStarterPackageEntryCommand, Result>
{
    public ValueTask<Result> Handle(DeleteStarterPackageEntryCommand request, CancellationToken cancellationToken) =>
        store.DeleteEntryAsync(request.RelativePath, cancellationToken);
}
