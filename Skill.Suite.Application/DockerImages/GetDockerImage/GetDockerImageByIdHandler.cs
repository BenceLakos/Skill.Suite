using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages.GetDockerImage;

public sealed class GetDockerImageByIdHandler(IAppDbContext db)
    : IRequestHandler<GetDockerImageByIdQuery, Result<DockerImageDto>>
{
    public async ValueTask<Result<DockerImageDto>> Handle(GetDockerImageByIdQuery request, CancellationToken cancellationToken)
    {
        var image = await db.DockerImages
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        return image is null
            ? DockerImageErrors.NotFound(request.Id)
            : DockerImageMapper.ToDto(image);
    }
}
