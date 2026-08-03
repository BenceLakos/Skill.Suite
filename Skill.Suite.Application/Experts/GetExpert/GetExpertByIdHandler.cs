using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Experts;

namespace Skill.Suite.Application.Experts.GetExpert;

public sealed class GetExpertByIdHandler(IAppDbContext db) : IRequestHandler<GetExpertByIdQuery, Result<ExpertDto>>
{
    public async ValueTask<Result<ExpertDto>> Handle(GetExpertByIdQuery request, CancellationToken cancellationToken)
    {
        var expert = await db.Experts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        return expert is null
            ? ExpertErrors.NotFound(request.Id)
            : ExpertMapper.ToDto(expert);
    }
}
