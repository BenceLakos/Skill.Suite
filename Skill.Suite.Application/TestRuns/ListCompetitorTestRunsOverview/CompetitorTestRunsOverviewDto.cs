using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.ListCompetitorTestRunsOverview;

public sealed record CompetitorTestRunsOverviewDto(
    Guid CompetitorId,
    string Username,
    string FullName,
    string CountryCode,
    int TestRunCount,
    TestRunStatus? CurrentStatus,
    DateTime? LastRunAt,
    int LastResultTestsRun,
    int LastResultTestsPassed,
    int LastResultTestsFailed);
