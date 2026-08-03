using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns;

public sealed record UnitTestResultDto(
    Guid Id,
    string Name,
    TestOutcome Outcome,
    string? Aspect,
    bool AspectCompetitorVisible,
    long? DurationMs,
    string? Error,
    DateTime StartedAt,
    DateTime? FinishedAt,
    IReadOnlyList<TestEventDto> Events);
