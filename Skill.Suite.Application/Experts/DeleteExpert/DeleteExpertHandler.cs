using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Experts;

namespace Skill.Suite.Application.Experts.DeleteExpert;

public sealed class DeleteExpertHandler(IAppDbContext db) : IRequestHandler<DeleteExpertCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteExpertCommand request, CancellationToken cancellationToken)
    {
        var expert = await db.Experts.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (expert is null)
            return Result.Failure(ExpertErrors.NotFound(request.Id));

        expert.MarkRemoved();
        db.Experts.Remove(expert);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
