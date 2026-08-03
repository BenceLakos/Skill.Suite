namespace Skill.Suite.TestLog.Protocol;

/// <summary>One assertion.</summary>
/// <param name="Fixture">Owning fixture name.</param>
/// <param name="Test">Test case name.</param>
/// <param name="Kind">Assertion label, conventionally from <see cref="AssertionKinds"/>.</param>
/// <param name="Expected">Expected value, or a sentinel from <see cref="AssertionSentinels"/>.</param>
/// <param name="Actual">Actual value.</param>
/// <param name="Passed">Whether the assertion held. False marks the unit failed.</param>
/// <remarks>
/// Emitted <i>after</i> the underlying assertion runs — <see cref="Passed"/> cannot be known any earlier.
/// <see cref="Expected"/> and <see cref="Actual"/> stay <c>object?</c> for the same reason as on
/// <see cref="CallEvent"/>: they carry whatever type the assertion compared.
/// </remarks>
public sealed record AssertionEvent(
    string? Fixture,
    string? Test,
    string? Kind,
    object? Expected,
    object? Actual,
    bool Passed)
    : TestLogEvent;
