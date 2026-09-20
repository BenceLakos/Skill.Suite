namespace Skill.Suite.TestLog.Protocol;

/// <summary>Line-coverage measurement for a scoring part, or for a single test fixture.</summary>
/// <param name="Part">Scoring part; null means the <c>overall</c> rollup.</param>
/// <param name="Value">Line coverage: <c>covered / total</c>.</param>
/// <param name="Total">Coverable lines.</param>
/// <param name="Covered">Lines the suite executed.</param>
/// <param name="Fixture">
/// Test class this measurement belongs to, named exactly as <see cref="StartFixtureEvent.Fixture"/> names it —
/// the class's simple name — so it joins onto a fixture already in the stream. When set, the event reports
/// what <i>that one test class</i> covered on its own, and <see cref="MetricEvent.Part"/> is absent: a fixture is not a
/// part, and the two name spaces must not be mixed. Omitted from the wire when null, which is the shape every
/// event before this field had.
/// </param>
/// <remarks>
/// <para>
/// <c>covered</c> and <c>total</c> are the shared metric vocabulary rather than <c>lines_covered</c> and
/// <c>lines_total</c>: the <c>coverage</c> discriminator already says the population is lines, so repeating it
/// in every field name was the duplication this contract removes.
/// </para>
/// <para>
/// A fixture-scoped event is additive rather than a replacement: the part and <c>overall</c> events are still
/// emitted, still mean the same thing, and are still the only ones a score is computed from. A consumer that
/// does not know about <see cref="Fixture"/> reads the event as an unscoped one — every probe in
/// <see cref="TestLogEventReader"/> is name-based and ignores what it does not ask for — so an older platform
/// keeps working against a newer judge image.
/// </para>
/// </remarks>
public sealed record CoverageEvent(
    string? Part,
    double? Value,
    int? Total,
    int? Covered,
    string? Fixture = null)
    : MetricEvent(Part, Value);
