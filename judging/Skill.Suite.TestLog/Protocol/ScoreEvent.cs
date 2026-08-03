namespace Skill.Suite.TestLog.Protocol;

/// <summary>The composite quality for a scoring part.</summary>
/// <param name="Part">Scoring part; null means the <c>overall</c> rollup.</param>
/// <param name="Value">
/// The composite quality in 0..1. <b>The one metric value the platform functionally consumes</b> — it is lifted
/// onto the fixture and drives the competitor-visible score bucket. Must be a JSON number; a quoted string is
/// ignored and the competitor sees no score at all.
/// </param>
/// <param name="Inputs">The component ratios this was derived from.</param>
/// <remarks>
/// Deliberately does not repeat the test tallies. They duplicate the <see cref="TestSummaryEvent"/> emitted for
/// the same part by the same loop, one line earlier, and nothing read them.
/// </remarks>
public sealed record ScoreEvent(
    string? Part,
    double? Value,
    ScoreInputs? Inputs = null)
    : MetricEvent(Part, Value);
