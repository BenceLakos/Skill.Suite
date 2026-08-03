using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns;

public sealed record TestRunDto(
    Guid Id,
    Guid SessionId,
    Guid? CompetitorId,
    string RepositoryUrl,
    string? RepositoryName,
    string? Branch,
    string? CommitSha,
    string FolderName,
    string JudgementImage,
    TestRunStatus Status,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? FailureReason,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    IReadOnlyList<TestFixtureResultDto> Fixtures);

public sealed record TestRunSummaryDto(
    Guid Id,
    Guid SessionId,
    Guid? CompetitorId,
    string RepositoryUrl,
    string? Branch,
    string? CommitSha,
    TestRunStatus Status,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    DateTime CreatedAt,
    int TestsRun,
    int TestsPassed,
    int TestsFailed);
