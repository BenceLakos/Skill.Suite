using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.ListCompetitorTestRunsOverview;

public sealed class ListCompetitorTestRunsOverviewHandler(IAppDbContext db)
    : IRequestHandler<ListCompetitorTestRunsOverviewQuery, Result<List<CompetitorTestRunsOverviewDto>>>
{
    public async ValueTask<Result<List<CompetitorTestRunsOverviewDto>>> Handle(
        ListCompetitorTestRunsOverviewQuery request,
        CancellationToken cancellationToken)
    {
        // Default to whatever session is live right now; admin can override via the
        // session picker on the page.
        var sessionId = request.SessionId
            ?? await ActiveSessionResolver.ResolveAsync(db, cancellationToken);

        if (sessionId is null)
            return new List<CompetitorTestRunsOverviewDto>();

        var rows = await db.Competitors
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Username,
                c.FullName,
                c.CountryCode,
                Runs = db.TestRuns
                    .Where(r => r.CompetitorId == c.Id && r.SessionId == sessionId.Value)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.Status,
                        r.CreatedAt,
                        r.CommitSha,
                        TestsRun = r.Fixtures.Sum(f => f.TestsRun),
                        TestsPassed = r.Fixtures.Sum(f => f.TestsPassed),
                        TestsFailed = r.Fixtures.Sum(f => f.TestsFailed),
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var overview = rows
            .Where(c => c.Runs.Count > 0)
            .Select(c =>
            {
                var latest = c.Runs[0];
                return new CompetitorTestRunsOverviewDto(
                    c.Id,
                    c.Username,
                    c.FullName,
                    c.CountryCode,
                    c.Runs.Count,
                    latest.Status,
                    latest.CreatedAt,
                    latest.TestsRun,
                    latest.TestsPassed,
                    latest.TestsFailed,
                    latest.CommitSha);
            })
            .OrderBy(c => c.Username)
            .ToList();

        return overview;
    }
}
