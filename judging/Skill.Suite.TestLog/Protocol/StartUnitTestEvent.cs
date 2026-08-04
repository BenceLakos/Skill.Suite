namespace Skill.Suite.TestLog.Protocol;

/// <summary>Opens a unit test.</summary>
/// <param name="Fixture">Owning fixture name.</param>
/// <param name="Test">Test case name.</param>
/// <param name="Aspect">Marking-scheme aspect id, or null for an ungraded test.</param>
/// <param name="AspectVisible">Whether the aspect feeds the competitor-visible score.</param>
/// <remarks>
/// Mandatory: the consumer drops <c>call</c>, <c>assertion</c> and <c>finish-unit-test</c> events for a test it
/// never saw start.
/// </remarks>
public sealed record StartUnitTestEvent(
    string? Fixture,
    string? Test,
    string? Aspect = null,
    bool? AspectVisible = null)
    : TestLogEvent;
