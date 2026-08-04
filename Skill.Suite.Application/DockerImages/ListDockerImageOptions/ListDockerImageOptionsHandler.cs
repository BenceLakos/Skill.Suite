using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.DockerImages.ListDockerImageOptions;

public sealed class ListDockerImageOptionsHandler(IAppDbContext db)
    : IRequestHandler<ListDockerImageOptionsQuery, Result<List<string>>>
{
    public async ValueTask<Result<List<string>>> Handle(ListDockerImageOptionsQuery request, CancellationToken cancellationToken)
    {
        var images = await db.DockerImages
            .AsNoTracking()
            .Select(s => s.ImageName)
            .Distinct()
            .OrderBy(i => i)
            .ToListAsync(cancellationToken);

        return images;
    }
}
