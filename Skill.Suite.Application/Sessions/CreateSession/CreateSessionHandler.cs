using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.CreateSession;

public sealed class CreateSessionHandler(IAppDbContext db)
    : IRequestHandler<CreateSessionCommand, Result<SessionDto>>
{
    public async ValueTask<Result<SessionDto>> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        var slugTaken = await db.Sessions.AnyAsync(s => s.Slug == slug, cancellationToken);
        if (slugTaken)
            return SessionErrors.SlugConflict;

        var sessionResult = Session.Create(
            request.Name,
            slug,
            request.Description,
            request.StartsAt,
            request.EndsAt,
            request.TemplateFolder,
            request.JudgementImage,
            request.DatabaseName,
            request.DatabaseReadAccess,
            request.DatabaseWriteAccess,
            request.GitCredentialId,
            request.JudgementImagePullCredentialId,
            request.DockerImages);

        if (sessionResult.IsFailure)
            return sessionResult.Error;

        var session = sessionResult.Value;
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return SessionMapper.ToDto(session);
    }
}
