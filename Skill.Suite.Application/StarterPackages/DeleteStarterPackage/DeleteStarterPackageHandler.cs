using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.DeleteStarterPackage;

public sealed class DeleteStarterPackageHandler(IStarterPackageStore store)
    : IRequestHandler<DeleteStarterPackageCommand, Result>
{
    public ValueTask<Result> Handle(DeleteStarterPackageCommand request, CancellationToken cancellationToken) =>
        store.DeleteAsync(request.Name.Trim(), cancellationToken);
}
