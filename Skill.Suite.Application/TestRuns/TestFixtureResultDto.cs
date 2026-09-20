using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns;

public sealed record TestFixtureResultDto(
    Guid Id,
    string Name,
    TestFixtureKind Kind,
    int TestsRun,
    int TestsPassed,
    int TestsFailed,
    long? DurationMs,
    DateTime StartedAt,
    DateTime? FinishedAt,
    double? Quality,
    double? LineCoverage,
    double? MutationScore,
    IReadOnlyList<UnitTestResultDto> UnitTests);
