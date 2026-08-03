using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages.DeleteDockerImage;

public sealed class DeleteDockerImageHandler(IAppDbContext db) : IRequestHandler<DeleteDockerImageCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteDockerImageCommand request, CancellationToken cancellationToken)
    {
        var image = await db.DockerImages.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (image is null)
            return Result.Failure(DockerImageErrors.NotFound(request.Id));

        image.MarkRemoved();
        db.DockerImages.Remove(image);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
