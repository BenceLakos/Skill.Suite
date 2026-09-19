using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.DownloadStarterPackage;

public sealed class DownloadStarterPackageHandler(IStarterPackageStore store)
    : IRequestHandler<DownloadStarterPackageQuery, Result<StarterPackageDownload>>
{
    public ValueTask<Result<StarterPackageDownload>> Handle(
        DownloadStarterPackageQuery request,
        CancellationToken cancellationToken) =>
        store.PrepareDownloadAsync(request.RelativePath, cancellationToken);
}
