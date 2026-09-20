namespace Skill.Suite.TestLog.Protocol;

/// <summary>Mutation-testing measurement for a scoring part, or for a single test fixture.</summary>
/// <param name="Part">Scoring part; null means the <c>overall</c> rollup.</param>
/// <param name="Value">
/// Kill rate: <c>(killed + timeout) / covered</c>. Note the denominator is <see cref="Covered"/>, not
/// <see cref="Total"/> — a mutant no test reached says nothing about the suite's ability to detect faults, so
/// including it would punish a good suite for untested code the coverage metric already reports.
/// </param>
/// <param name="Total">Mutants generated.</param>
/// <param name="Covered">Mutants at least one test reached: <c>killed + survived + timeout</c>.</param>
/// <param name="Killed">Mutants the suite detected.</param>
/// <param name="Survived">Mutants the suite executed but failed to detect. The interesting number.</param>
/// <param name="Timeout">Mutants that hung the suite. Counted as detected.</param>
/// <param name="NoCoverage">Mutants no test reached.</param>
/// <param name="Other">Mutants with any other disposition, such as a compile error.</param>
/// <param name="Fixture">
/// Test class this measurement belongs to, named exactly as <see cref="StartFixtureEvent.Fixture"/> names it —
/// the class's simple name — so it joins onto a fixture already in the stream. When set, the event reports what
/// <i>that one test class</i> reached and detected on its own, and <see cref="MetricEvent.Part"/> is absent: a fixture is
/// not a part, and the two name spaces must not be mixed. Omitted from the wire when null.
/// </param>
/// <remarks>
/// <para>
/// <see cref="Total"/> is new information rather than a rename: previously only the covered count was on the
/// wire, so no-coverage's share of the whole mutant population could not be computed from the event.
/// </para>
/// <para>
/// For a fixture-scoped event the tallies are attributed per test rather than per mutant status:
/// <see cref="Covered"/> counts the mutants at least one of that fixture's tests exercised, and
/// <see cref="Killed"/> the ones at least one of its tests detected. <see cref="Total"/> is therefore the same
/// as <see cref="Covered"/>, and <see cref="NoCoverage"/> and <see cref="Other"/> are 0 — a mutant nothing in
/// this fixture touched is simply not this fixture's business, and counting it here would make every small
/// fixture look negligent.
/// </para>
/// <para>
/// A fixture-scoped event is additive: the part and <c>overall</c> events keep their exact meaning and remain
/// the only ones a score is computed from. Consumers that do not know the field ignore it.
/// </para>
/// </remarks>
public sealed record MutationEvent(
    string? Part,
    double? Value,
    int? Total,
    int? Covered,
    int? Killed,
    int? Survived,
    int? Timeout,
    int? NoCoverage,
    int? Other,
    string? Fixture = null)
    : MetricEvent(Part, Value);
