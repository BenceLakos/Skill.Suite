using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Experts.ListExperts;

public sealed class ListExpertsHandler(IAppDbContext db) : IRequestHandler<ListExpertsQuery, Result<List<ExpertDto>>>
{
    public async ValueTask<Result<List<ExpertDto>>> Handle(ListExpertsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Experts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(e => e.DisplayName.ToLower().Contains(term));
        }

        var experts = await query
            .OrderBy(e => e.DisplayName)
            .ToListAsync(cancellationToken);

        return ExpertMapper.ToDtoList(experts);
    }
}
