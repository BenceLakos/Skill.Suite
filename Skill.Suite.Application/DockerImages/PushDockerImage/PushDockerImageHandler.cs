using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages.PushDockerImage;

/// <summary>
/// Records intent to push a built docker image to its configured Nexus registry.
/// The actual `docker login` + `docker push` exec is performed by an
/// infrastructure subscriber to <see cref="Skill.Suite.Domain.DockerImages.Events.DockerImagePushedEvent"/>;
/// this handler only persists the event so the runner picks it up.
/// </summary>
public sealed class PushDockerImageHandler(IAppDbContext db) : IRequestHandler<PushDockerImageCommand, Result>
{
    public async ValueTask<Result> Handle(PushDockerImageCommand request, CancellationToken cancellationToken)
    {
        var image = await db.DockerImages.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (image is null)
            return Result.Failure(DockerImageErrors.NotFound(request.Id));

        var result = image.MarkPushed(request.Tag.Trim());
        if (result.IsFailure)
            return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
