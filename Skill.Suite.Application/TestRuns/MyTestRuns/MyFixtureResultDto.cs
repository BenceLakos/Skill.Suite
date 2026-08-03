namespace Skill.Suite.Application.TestRuns.MyTestRuns;

public sealed record MyFixtureResultDto(
    Guid Id,
    string Name,
    ScoreBucket Score,
    long? DurationMs,
    DateTime StartedAt,
    DateTime? FinishedAt);
