namespace Skill.Suite.Application.StarterPackages.UploadStarterPackageFile;

using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

public sealed class UploadStarterPackageFileHandler(IStarterPackageStore store)
    : IRequestHandler<UploadStarterPackageFileCommand, Result>
{
    public ValueTask<Result> Handle(UploadStarterPackageFileCommand request, CancellationToken cancellationToken) =>
        store.WriteFileAsync(
            StarterPackagePath.Combine(request.FolderPath, request.RelativePath),
            request.Content,
            request.Overwrite,
            cancellationToken);
}
