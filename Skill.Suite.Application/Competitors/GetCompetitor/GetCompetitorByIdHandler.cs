using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;

namespace Skill.Suite.Application.Competitors.GetCompetitor;

public sealed class GetCompetitorByIdHandler(IAppDbContext db, IPasswordVault vault)
    : IRequestHandler<GetCompetitorByIdQuery, Result<CompetitorDto>>
{
    public async ValueTask<Result<CompetitorDto>> Handle(GetCompetitorByIdQuery request, CancellationToken cancellationToken)
    {
        var competitor = await db.Competitors
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        return competitor is null
            ? CompetitorErrors.NotFound(request.Id)
            : CompetitorMapper.ToDto(competitor, vault);
    }
}
