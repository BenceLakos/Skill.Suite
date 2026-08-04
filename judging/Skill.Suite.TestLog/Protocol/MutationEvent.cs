namespace Skill.Suite.TestLog.Protocol;

/// <summary>Mutation-testing measurement for a scoring part.</summary>
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
/// <remarks>
/// <see cref="Total"/> is new information rather than a rename: previously only the covered count was on the
/// wire, so no-coverage's share of the whole mutant population could not be computed from the event.
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
    int? Other)
    : MetricEvent(Part, Value);
