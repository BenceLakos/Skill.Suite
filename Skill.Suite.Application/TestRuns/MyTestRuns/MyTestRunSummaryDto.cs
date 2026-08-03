using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.MyTestRuns;

public sealed record MyTestRunSummaryDto(
    Guid Id,
    string? Branch,
    string? CommitSha,
    TestRunStatus Status,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    DateTime CreatedAt);
