using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages.UpdateDockerImage;

public sealed class UpdateDockerImageHandler(IAppDbContext db) : IRequestHandler<UpdateDockerImageCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateDockerImageCommand request, CancellationToken cancellationToken)
    {
        var image = await db.DockerImages.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (image is null)
            return Result.Failure(DockerImageErrors.NotFound(request.Id));

        var name = request.Name.Trim();
        if (!string.Equals(image.Name, name, StringComparison.Ordinal))
        {
            var taken = await db.DockerImages.AnyAsync(s => s.Id != request.Id && s.Name == name, cancellationToken);
            if (taken)
                return Result.Failure(DockerImageErrors.NameConflict);
        }

        if (request.Source == DockerImageSource.Pull)
        {
            image.UpdatePullable(name, request.ImageName, request.NexusCredentialId);
        }
        else
        {
            image.UpdateBuildable(
                name,
                request.ImageName,
                request.BuildContext!,
                request.DockerfilePath,
                request.BuildArgs ?? new Dictionary<string, string>(),
                request.NexusCredentialId);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
