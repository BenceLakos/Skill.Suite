namespace Skill.Suite.Application.StarterPackages.PlanStarterPackageUpload;

using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

public sealed class PlanStarterPackageUploadHandler(IStarterPackageStore store)
    : IRequestHandler<PlanStarterPackageUploadQuery, Result<IReadOnlyList<StarterPackageUploadPlanItemDto>>>
{
    public ValueTask<Result<IReadOnlyList<StarterPackageUploadPlanItemDto>>> Handle(
        PlanStarterPackageUploadQuery request,
        CancellationToken cancellationToken) =>
        store.PlanUploadAsync(request.FolderPath, request.Files, cancellationToken);
}
