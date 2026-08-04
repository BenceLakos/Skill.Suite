using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.GetSession;

public sealed class GetSessionBySlugHandler(IAppDbContext db) : IRequestHandler<GetSessionBySlugQuery, Result<SessionDto>>
{
    public async ValueTask<Result<SessionDto>> Handle(GetSessionBySlugQuery request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        var session = await db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Slug == slug, cancellationToken);

        return session is null
            ? Error.NotFound("Session.NotFound", $"Session with slug '{slug}' was not found.")
            : SessionMapper.ToDto(session);
    }
}
