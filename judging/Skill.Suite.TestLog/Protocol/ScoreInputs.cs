namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// The component ratios a <see cref="ScoreEvent"/> was derived from.
/// </summary>
/// <param name="PassRate">Pass rate, as reported by the matching <see cref="TestSummaryEvent"/>.</param>
/// <param name="LineCoverage">Raw line coverage, as reported by the matching <see cref="CoverageEvent"/>.</param>
/// <param name="ScaledCoverage">Line coverage after the marking map's floor/ceiling ramp.</param>
/// <param name="PassingCoverage">Pass rate multiplied by scaled coverage.</param>
/// <param name="MutationKillRate">Raw kill rate, as reported by the matching <see cref="MutationEvent"/>.</param>
/// <param name="MutationScore">Kill rate after the marking map's floor/ceiling ramp.</param>
/// <param name="Core">The composite core term the quality is mostly built from.</param>
/// <remarks>
/// <para>
/// Nested rather than flattened alongside the score's own <c>value</c>, and this is the one structural decision
/// worth defending. Flattening would recreate exactly the problem the consolidation removes: the pass rate
/// would be <c>value</c> on <c>test-summary</c> and <c>pass_rate</c> on <c>score</c>, one line apart, with
/// nothing saying they are the same number. Nesting states the relationship structurally — the top level is
/// this event's verdict, <c>inputs</c> is the provenance it came from.
/// </para>
/// <para>
/// These keep their descriptive names because here they are siblings that must be told apart, which is the
/// opposite of the situation the shared vocabulary addresses.
/// </para>
/// </remarks>
public sealed record ScoreInputs(
    double? PassRate,
    double? LineCoverage,
    double? ScaledCoverage,
    double? PassingCoverage,
    double? MutationKillRate,
    double? MutationScore,
    double? Core);
