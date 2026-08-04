using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.ListTestRuns;

public sealed class ListTestRunsHandler(IAppDbContext db) : IRequestHandler<ListTestRunsQuery, Result<List<TestRunSummaryDto>>>
{
    public async ValueTask<Result<List<TestRunSummaryDto>>> Handle(ListTestRunsQuery request, CancellationToken cancellationToken)
    {
        // If the caller didn't pick a session, default to the active one so this query
        // matches the admin overview and the competitor's "my runs" page out of the box.
        // An explicit non-null SessionId is honoured — that's how the admin views runs
        // from a previous event.
        var sessionId = request.SessionId
            ?? await ActiveSessionResolver.ResolveAsync(db, cancellationToken);

        if (sessionId is null)
            return new List<TestRunSummaryDto>();

        var query = db.TestRuns
            .AsNoTracking()
            .Where(r => r.SessionId == sessionId.Value);

        if (request.CompetitorId.HasValue)
            query = query.Where(r => r.CompetitorId == request.CompetitorId.Value);

        if (request.Status.HasValue)
            query = query.Where(r => r.Status == request.Status.Value);

        var runs = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new TestRunSummaryDto(
                r.Id,
                r.SessionId,
                r.CompetitorId,
                r.RepositoryUrl,
                r.Branch,
                r.CommitSha,
                r.Status,
                r.StartedAt,
                r.FinishedAt,
                r.CreatedAt,
                r.Fixtures.Sum(f => f.TestsRun),
                r.Fixtures.Sum(f => f.TestsPassed),
                r.Fixtures.Sum(f => f.TestsFailed)))
            .ToListAsync(cancellationToken);

        return runs;
    }
}
