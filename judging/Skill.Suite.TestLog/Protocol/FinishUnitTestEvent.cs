namespace Skill.Suite.TestLog.Protocol;

/// <summary>Closes a unit test and reports its verdict.</summary>
/// <param name="Fixture">Owning fixture name.</param>
/// <param name="Test">Test case name.</param>
/// <param name="Outcome">The verdict.</param>
/// <param name="DurationMs">Wall-clock duration of the test in milliseconds.</param>
/// <param name="Error">Failure detail, or null when the test passed.</param>
/// <param name="Aspect">Marking-scheme aspect id, or null for an ungraded test.</param>
/// <param name="AspectVisible">Whether the aspect feeds the competitor-visible score.</param>
/// <remarks>
/// The verdict is advisory. A preceding <c>call</c> carrying <c>threw</c>, or an <c>assertion</c> that did not
/// pass, has already fixed the outcome and this event cannot soften it.
/// </remarks>
public sealed record FinishUnitTestEvent(
    string? Fixture,
    string? Test,
    TestLogOutcome Outcome,
    long DurationMs,
    string? Error,
    string? Aspect = null,
    bool? AspectVisible = null)
    : TestLogEvent;
