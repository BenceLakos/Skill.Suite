namespace Skill.Suite.TestLog.Protocol;

/// <summary>Closes a fixture and reports its tallies.</summary>
/// <param name="Fixture">Fixture name, matching the one that opened it.</param>
/// <param name="TestsRun">Tests run.</param>
/// <param name="TestsPassed">Tests passed.</param>
/// <param name="TestsFailed">Tests failed.</param>
/// <param name="DurationMs">Wall-clock duration of the fixture in milliseconds.</param>
/// <remarks>
/// The three tallies are informational: the consumer recomputes them from the unit outcomes, because a judge
/// can miscount — reporting a test as passed when its call threw, for instance.
/// </remarks>
public sealed record FinishFixtureEvent(
    string? Fixture,
    int TestsRun,
    int TestsPassed,
    int TestsFailed,
    long DurationMs)
    : TestLogEvent;
