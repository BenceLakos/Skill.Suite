using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.DockerImages.ListDockerImages;

public sealed class ListDockerImagesHandler(IAppDbContext db)
    : IRequestHandler<ListDockerImagesQuery, Result<List<DockerImageDto>>>
{
    public async ValueTask<Result<List<DockerImageDto>>> Handle(ListDockerImagesQuery request, CancellationToken cancellationToken)
    {
        var query = db.DockerImages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term) || s.ImageName.ToLower().Contains(term));
        }

        var images = await query
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return DockerImageMapper.ToDtoList(images);
    }
}
