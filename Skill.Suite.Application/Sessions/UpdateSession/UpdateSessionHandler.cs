using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.UpdateSession;

public sealed class UpdateSessionHandler(IAppDbContext db) : IRequestHandler<UpdateSessionCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (session is null)
            return Result.Failure(SessionErrors.NotFound(request.Id));

        var result = session.UpdateDetails(
            request.Name,
            request.Description,
            request.StartsAt,
            request.EndsAt,
            request.Status,
            request.TemplateFolder,
            request.JudgementImage,
            request.DatabaseName,
            request.DatabaseReadAccess,
            request.DatabaseWriteAccess,
            request.GitCredentialId,
            request.JudgementImagePullCredentialId,
            request.DockerImages);

        if (result.IsFailure)
            return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
