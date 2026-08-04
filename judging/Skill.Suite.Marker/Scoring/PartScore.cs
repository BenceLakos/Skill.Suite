using Skill.Suite.Marker.Model;

namespace Skill.Suite.Marker.Scoring;

/// <summary>
/// The computed score for one part, with every intermediate term kept for transparency.
/// </summary>
/// <param name="Part">Part id.</param>
/// <param name="Counts">Verdict tallies.</param>
/// <param name="Coverage">Line-coverage totals.</param>
/// <param name="Mutation">Mutation outcomes.</param>
/// <param name="ScaledCoverage">Coverage mapped onto the configured ramp, 0..1.</param>
/// <param name="PassingCoverage">Coverage discounted by the pass rate, 0..1.</param>
/// <param name="MutationScore">Kill rate mapped onto the configured ramp, 0..1.</param>
/// <param name="Core">Geometric mean of passing coverage and mutation score.</param>
/// <param name="Quality">Final weighted quality, 0..1. This is what reaches the UI.</param>
/// <remarks>
/// The intermediate terms are emitted alongside the quality so a disputed score can be re-derived from
/// the event stream without re-running the judge.
/// </remarks>
public sealed record PartScore(
    string Part,
    TestCounts Counts,
    CoverageStats Coverage,
    MutationStats Mutation,
    double ScaledCoverage,
    double PassingCoverage,
    double MutationScore,
    double Core,
    double Quality);
