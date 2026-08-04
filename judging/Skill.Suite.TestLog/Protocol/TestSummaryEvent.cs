namespace Skill.Suite.TestLog.Protocol;

/// <summary>Aggregate test tallies for a scoring part.</summary>
/// <param name="Part">Scoring part; null means the <c>overall</c> rollup.</param>
/// <param name="Value">Pass rate: <c>passed / total</c>.</param>
/// <param name="Total">
/// Tests with a verdict. Note this is <c>passed + failed</c> and <b>excludes skipped</b>, so
/// <c>total != passed + failed + skipped</c> — surprising, but it is what makes the pass rate meaningful.
/// </param>
/// <param name="Passed">Tests that passed.</param>
/// <param name="Failed">Tests that failed, including those that errored or timed out.</param>
/// <param name="Skipped">Tests that did not run. Reported so an all-skipped suite is visible rather than silent.</param>
/// <param name="Warnings">
/// Free-text warnings about missing inputs, attached only to the rollup. A warning on its own part would
/// become a junk fixture in the UI, which is why there is no separate warning event.
/// </param>
public sealed record TestSummaryEvent(
    string? Part,
    double? Value,
    int? Total,
    int? Passed,
    int? Failed,
    int? Skipped,
    IReadOnlyList<string>? Warnings = null)
    : MetricEvent(Part, Value);
