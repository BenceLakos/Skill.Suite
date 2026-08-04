using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.MyTestRuns;

public sealed record MyTestRunDto(
    Guid Id,
    string? Branch,
    string? CommitSha,
    TestRunStatus Status,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    DateTime CreatedAt,
    /// <summary>
    /// Whether the judgement run stopped before finishing. Deliberately a flag, not the reason: the run's
    /// FailureReason is built from the judgement container's raw stderr, which for a white-box session
    /// contains hidden test names, assertion messages and reference-implementation stack traces. Staff see
    /// the detail; competitors get the category.
    /// </summary>
    bool HasJudgeError,
    IReadOnlyList<MyFixtureResultDto> Fixtures);
