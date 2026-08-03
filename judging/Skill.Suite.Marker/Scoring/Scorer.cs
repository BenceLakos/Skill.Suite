using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Model;

namespace Skill.Suite.Marker.Scoring;

/// <summary>
/// Turns raw tallies into a quality score for one part.
/// </summary>
/// <param name="options">The ramps and weights, from the marking map.</param>
public sealed class Scorer(ScoringOptions options)
{
    /// <summary>Number of decimal places every emitted metric is rounded to.</summary>
    /// <remarks>
    /// <see cref="Math.Round(double, int)"/> is banker's rounding. Kept as-is for protocol stability, so
    /// tests must avoid midpoint fixtures or they will disagree with hand-computed expectations.
    /// </remarks>
    public const int Digits = 4;

    /// <summary>Scores one part.</summary>
    /// <param name="part">Part id.</param>
    /// <param name="counts">Verdict tallies.</param>
    /// <param name="coverage">Line-coverage totals.</param>
    /// <param name="mutation">Mutation outcomes.</param>
    /// <returns>The score with all intermediate terms.</returns>
    public PartScore Score(string part, TestCounts counts, CoverageStats coverage, MutationStats mutation)
    {
        var passRate = Sanitize(counts.PassRate);

        var scaledCoverage = Ramp(coverage.Rate * 100.0, options.CoverageFloor, options.CoverageCeil);
        var passingCoverage = Sanitize(passRate * scaledCoverage);
        var mutationScore = Ramp(mutation.KillRate * 100.0, options.MutationFloor, options.MutationCeil);

        // Geometric mean: a suite that covers everything but detects nothing, or detects everything in a
        // sliver of the code, scores near zero. Both terms have to be real.
        var core = Sanitize(Math.Sqrt(passingCoverage * mutationScore));
        var quality = Math.Clamp(
            Sanitize(options.CoverageWeight * scaledCoverage + options.CoreWeight * core), 0.0, 1.0);

        return new PartScore(
            part, counts, coverage, mutation,
            Round(scaledCoverage), Round(passingCoverage), Round(mutationScore),
            Round(core), Round(quality));
    }

    /// <summary>Rounds a metric to the emitted precision.</summary>
    public static double Round(double value) => Math.Round(Sanitize(value), Digits);

    private static double Ramp(double percentage, double floor, double ceiling)
    {
        var span = ceiling - floor;
        // The map validator rejects a non-positive span, so this only guards a caller that bypassed it.
        if (span <= 0) return percentage >= ceiling ? 1.0 : 0.0;

        return Math.Clamp(Sanitize((percentage - floor) / span), 0.0, 1.0);
    }

    /// <summary>
    /// Replaces NaN and the infinities with 0.
    /// </summary>
    /// <remarks>
    /// Two traps in one: <c>Math.Clamp(double.NaN, 0, 1)</c> returns NaN rather than clamping, and
    /// <c>System.Text.Json</c> throws on NaN — so a single divide-by-zero anywhere upstream would turn a
    /// scored submission into a failed run with an unhelpful serialization error.
    /// </remarks>
    private static double Sanitize(double value) => double.IsFinite(value) ? value : 0.0;
}
