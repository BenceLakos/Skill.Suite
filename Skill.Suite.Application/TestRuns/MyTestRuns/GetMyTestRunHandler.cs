using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Sessions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.MyTestRuns;

public sealed class GetMyTestRunHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyTestRunQuery, Result<MyTestRunDto>>
{
    public async ValueTask<Result<MyTestRunDto>> Handle(GetMyTestRunQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } competitorId)
            return Result.Failure<MyTestRunDto>(
                Error.Unauthorized("MyTestRuns.Unauthorized", "You must be signed in to view your test runs."));

        // Competitors are scoped to the currently active session. Anyone deep-linking
        // into an old run (e.g. notification, bookmark) gets a NotFound rather than
        // history that no longer belongs to them.
        var activeSessionId = await ActiveSessionResolver.ResolveAsync(db, cancellationToken);
        if (activeSessionId is null)
            return Result.Failure<MyTestRunDto>(TestRunErrors.NotFound(request.Id));

        var run = await db.TestRuns
            .AsNoTracking()
            .Include(r => r.Fixtures)
            .ThenInclude(f => f.UnitTests)
            .FirstOrDefaultAsync(
                r => r.Id == request.Id
                  && r.CompetitorId == competitorId
                  && r.SessionId == activeSessionId.Value,
                cancellationToken);

        if (run is null)
            return Result.Failure<MyTestRunDto>(TestRunErrors.NotFound(request.Id));

        // Judge diagnostics are staff material: they name hidden tests and quote compiler output against the
        // hidden suite's file paths. The competitor is told only that something went wrong.
        var fixtures = run.Fixtures
            .Where(f => f.Kind != TestFixtureKind.Diagnostics)
            .OrderBy(f => f.StartedAt)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => new MyFixtureResultDto(
                f.Id,
                f.Name,
                ScoreBucketCalculator.ForFixture(f),
                f.DurationMs,
                f.StartedAt,
                f.FinishedAt))
            .ToList();

        return new MyTestRunDto(
            run.Id,
            run.Branch,
            run.CommitSha,
            run.Status,
            run.StartedAt,
            run.FinishedAt,
            run.CreatedAt,
            run.Status is TestRunStatus.Failed
                || run.Fixtures.Any(f => f.Kind == TestFixtureKind.Diagnostics),
            fixtures);
    }
}
