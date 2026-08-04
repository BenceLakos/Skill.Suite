using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.Webhooks;

/// <summary>
/// Returned by the webhook endpoint after a submission is accepted. The run starts
/// asynchronously — clients should poll <c>/api/test-runs/{TestRunId}</c> for progress.
/// </summary>
public sealed record TestRunAcceptedDto(
    Guid TestRunId,
    Guid SessionId,
    Guid? CompetitorId,
    string RepositoryUrl,
    string? Branch,
    string? CommitSha,
    string FolderName,
    TestRunStatus Status,
    DateTime CreatedAt,
    bool SupersededPreviousRun);
