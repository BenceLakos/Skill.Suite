namespace Skill.Suite.TestLog.Protocol;

/// <summary>Line-coverage measurement for a scoring part.</summary>
/// <param name="Part">Scoring part; null means the <c>overall</c> rollup.</param>
/// <param name="Value">Line coverage: <c>covered / total</c>.</param>
/// <param name="Total">Coverable lines.</param>
/// <param name="Covered">Lines the suite executed.</param>
/// <remarks>
/// <c>covered</c> and <c>total</c> are the shared metric vocabulary rather than <c>lines_covered</c> and
/// <c>lines_total</c>: the <c>coverage</c> discriminator already says the population is lines, so repeating it
/// in every field name was the duplication this contract removes.
/// </remarks>
public sealed record CoverageEvent(
    string? Part,
    double? Value,
    int? Total,
    int? Covered)
    : MetricEvent(Part, Value);
