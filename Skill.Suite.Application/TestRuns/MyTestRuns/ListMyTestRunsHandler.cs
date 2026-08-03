using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.MyTestRuns;

public sealed class ListMyTestRunsHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<ListMyTestRunsQuery, Result<List<MyTestRunSummaryDto>>>
{
    public async ValueTask<Result<List<MyTestRunSummaryDto>>> Handle(ListMyTestRunsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } competitorId)
            return Result.Failure<List<MyTestRunSummaryDto>>(
                Error.Unauthorized("MyTestRuns.Unauthorized", "You must be signed in to view your test runs."));

        // Competitors only see runs for the currently active session. If there is no
        // active session, return an empty list rather than leaking historical runs.
        var activeSessionId = await ActiveSessionResolver.ResolveAsync(db, cancellationToken);
        if (activeSessionId is null)
            return new List<MyTestRunSummaryDto>();

        var runs = await db.TestRuns
            .AsNoTracking()
            .Where(r => r.CompetitorId == competitorId && r.SessionId == activeSessionId.Value)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new MyTestRunSummaryDto(
                r.Id,
                r.Branch,
                r.CommitSha,
                r.Status,
                r.StartedAt,
                r.FinishedAt,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        return runs;
    }
}
