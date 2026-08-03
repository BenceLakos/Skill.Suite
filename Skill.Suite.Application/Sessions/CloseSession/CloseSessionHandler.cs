using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.CloseSession;

public sealed class CloseSessionHandler(IAppDbContext db) : IRequestHandler<CloseSessionCommand, Result>
{
    public async ValueTask<Result> Handle(CloseSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (session is null)
            return Result.Failure(SessionErrors.NotFound(request.Id));

        var result = session.Close();
        if (result.IsFailure)
            return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
