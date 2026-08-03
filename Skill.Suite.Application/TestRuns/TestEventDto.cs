using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns;

public sealed record TestEventDto(
    TestEventKind Kind,
    DateTime Timestamp,
    string? Target,
    IReadOnlyList<string?>? Arguments,
    string? Returned,
    string? Threw,
    string? AssertionKind,
    string? Expected,
    string? Actual,
    bool? Passed,
    string? Payload,
    string? Detail);
