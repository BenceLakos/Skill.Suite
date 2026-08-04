namespace Skill.Suite.TestLog.Protocol;

/// <summary>Opens a fixture (a test class). Must precede that fixture's unit-test events.</summary>
/// <param name="Fixture">Fixture name, conventionally the test class's simple name.</param>
public sealed record StartFixtureEvent(
    string? Fixture)
    : TestLogEvent;
