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
    int LastResultTestsFailed,
    /// <summary>
    /// Commit of the most recent run, or <see langword="null"/> when this competitor has never been judged.
    /// </summary>
    /// <remarks>
    /// This is the reconciliation signal. A push is judged only if its single webhook delivery arrived, and the
    /// git host does not retry — so a competitor can have work pushed and no run at all, which was invisible
    /// until the session closed and the marks were wrong. Null here, or a commit the operator knows is stale,
    /// means use "Judge again" before the session ends.
    /// </remarks>
    string? LastCommitSha);
