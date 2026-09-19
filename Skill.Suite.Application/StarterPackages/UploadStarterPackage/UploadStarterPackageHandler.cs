using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.UploadStarterPackage;

public sealed class UploadStarterPackageHandler(IStarterPackageStore store)
    : IRequestHandler<UploadStarterPackageCommand, Result>
{
    public ValueTask<Result> Handle(UploadStarterPackageCommand request, CancellationToken cancellationToken) =>
        store.ImportAsync(request.Name.Trim(), request.Archive, request.Overwrite, cancellationToken);
}
