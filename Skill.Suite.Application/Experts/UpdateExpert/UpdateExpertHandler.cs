using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Experts;

namespace Skill.Suite.Application.Experts.UpdateExpert;

public sealed class UpdateExpertHandler(IAppDbContext db) : IRequestHandler<UpdateExpertCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateExpertCommand request, CancellationToken cancellationToken)
    {
        var expert = await db.Experts.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (expert is null)
            return Result.Failure(ExpertErrors.NotFound(request.Id));

        expert.Rename(request.DisplayName);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
