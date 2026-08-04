using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Competitors.ListCompetitors;

public sealed class ListCompetitorsHandler(IAppDbContext db, IPasswordVault vault)
    : IRequestHandler<ListCompetitorsQuery, Result<List<CompetitorDto>>>
{
    public async ValueTask<Result<List<CompetitorDto>>> Handle(ListCompetitorsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Competitors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.Username.ToLower().Contains(term)
                                     || c.IpAddress.Contains(term)
                                     || c.CountryCode.ToLower().Contains(term));
        }

        var competitors = await query
            .OrderBy(c => c.Username)
            .ToListAsync(cancellationToken);

        return CompetitorMapper.ToDtoList(competitors, vault);
    }
}
