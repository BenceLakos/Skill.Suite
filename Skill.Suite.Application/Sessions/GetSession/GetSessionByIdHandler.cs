using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.GetSession;

public sealed class GetSessionByIdHandler(IAppDbContext db) : IRequestHandler<GetSessionByIdQuery, Result<SessionDto>>
{
    public async ValueTask<Result<SessionDto>> Handle(GetSessionByIdQuery request, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        return session is null
            ? SessionErrors.NotFound(request.Id)
            : SessionMapper.ToDto(session);
    }
}
