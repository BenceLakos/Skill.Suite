using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Sessions.ListSessions;

public sealed class ListSessionsHandler(IAppDbContext db) : IRequestHandler<ListSessionsQuery, Result<List<SessionDto>>>
{
    public async ValueTask<Result<List<SessionDto>>> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Sessions.AsNoTracking();

        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term) || s.Slug.Contains(term));
        }

        var sessions = await query
            .OrderByDescending(s => s.StartsAt)
            .ToListAsync(cancellationToken);

        return SessionMapper.ToDtoList(sessions);
    }
}
