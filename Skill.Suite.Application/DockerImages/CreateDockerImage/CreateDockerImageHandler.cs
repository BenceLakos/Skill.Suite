using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages.CreateDockerImage;

public sealed class CreateDockerImageHandler(IAppDbContext db)
    : IRequestHandler<CreateDockerImageCommand, Result<DockerImageDto>>
{
    public async ValueTask<Result<DockerImageDto>> Handle(CreateDockerImageCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var taken = await db.DockerImages.AnyAsync(s => s.Name == name, cancellationToken);
        if (taken)
            return DockerImageErrors.NameConflict;

        var image = request.Source switch
        {
            DockerImageSource.Pull => DockerImage.CreatePullable(
                name,
                request.ImageName,
                request.NexusCredentialId),

            _ => DockerImage.CreateBuildable(
                name,
                request.ImageName,
                request.BuildContext!,
                request.DockerfilePath,
                request.BuildArgs ?? new Dictionary<string, string>(),
                request.NexusCredentialId),
        };

        db.DockerImages.Add(image);
        await db.SaveChangesAsync(cancellationToken);

        return DockerImageMapper.ToDto(image);
    }
}
