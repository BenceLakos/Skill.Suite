using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Model;
using Skill.Suite.Marker.Scoring;
using Xunit;

namespace Skill.Suite.Marker.Tests;

/// <summary>
/// Checks the quality formula against values computed by hand, and its behaviour at the edges.
/// </summary>
public sealed class ScorerTests
{
    private static readonly Scorer Default = new(new ScoringOptions());

    [Fact]
    public void Quality_MatchesTheHandComputedValue()
    {
        // passRate        = 1/2                        = 0.5
        // coverage        = 269/289 = 93.0796%
        // scaledCoverage  = clamp((93.0796-40)/40)     = 1.0
        // passingCoverage = 0.5 * 1.0                  = 0.5
        // killRate        = 4/5 = 80%
        // mutationScore   = clamp((80-30)/50)          = 1.0
        // core            = sqrt(0.5 * 1.0)            = 0.70710678...
        // quality         = 0.30*1.0 + 0.70*0.7071068  = 0.7949747...
        var score = Default.Score(
            "services",
            new TestCounts(Passed: 1, Failed: 1, Skipped: 1),
            new CoverageStats(269, 289),
            new MutationStats(Killed: 3, Survived: 1, Timeout: 1, NoCoverage: 2, Other: 1));

        Assert.Equal(1.0, score.ScaledCoverage);
        Assert.Equal(0.5, score.PassingCoverage);
        Assert.Equal(1.0, score.MutationScore);
        Assert.Equal(0.7071, score.Core, 4);
        Assert.Equal(0.795, score.Quality, 4);
    }

    [Fact]
    public void Quality_ForTheOverallRollup_MatchesTheHandComputedValue()
    {
        // coverage 269/300 = 89.6667% -> scaled 1.0; passRate 0.5 -> passingCoverage 0.5
        // killRate 5/7 = 71.4286%     -> mutationScore (71.4286-30)/50 = 0.8285714
        // core = sqrt(0.5 * 0.8285714) = 0.6436503
        // quality = 0.3 + 0.7*0.6436503 = 0.7505552
        var score = Default.Score(
            MarkingMap.OverallPart,
            new TestCounts(2, 2, 1),
            new CoverageStats(269, 300),
            new MutationStats(4, 2, 1, 2, 1));

        Assert.Equal(0.8286, score.MutationScore, 4);
        Assert.Equal(0.6437, score.Core, 4);
        Assert.Equal(0.7506, score.Quality, 4);
    }

    [Fact]
    public void Quality_IsZeroWhenNothingWasCovered()
    {
        var score = Default.Score("program", new TestCounts(0, 1, 0), new CoverageStats(0, 11), new MutationStats(1, 1, 0, 0, 0));

        Assert.Equal(0.0, score.ScaledCoverage);
        Assert.Equal(0.0, score.Core);
        Assert.Equal(0.0, score.Quality);
    }

    [Fact]
    public void Quality_IsCappedByTheCoverageWeightWhenNoMutantsAreKilled()
    {
        // core collapses to 0, so the ceiling is the coverage weight alone - by design: high coverage that
        // detects nothing is worth little.
        var score = Default.Score(
            "services", new TestCounts(10, 0, 0), new CoverageStats(100, 100), new MutationStats(0, 10, 0, 0, 0));

        Assert.Equal(1.0, score.ScaledCoverage);
        Assert.Equal(0.0, score.MutationScore);
        Assert.Equal(0.3, score.Quality, 6);
    }

    [Fact]
    public void Quality_IsZeroForAnEmptySuite()
    {
        var score = Default.Score("services", default, default, default);

        Assert.Equal(0.0, score.Quality);
        Assert.Equal(0.0, score.Counts.PassRate);
    }

    [Fact]
    public void EverythingKilledAndFullyCovered_ScoresOne()
    {
        var score = Default.Score(
            "services", new TestCounts(5, 0, 0), new CoverageStats(100, 100), new MutationStats(10, 0, 0, 0, 0));

        Assert.Equal(1.0, score.Quality);
    }

    [Fact]
    public void MapConstants_ChangeTheResult()
    {
        // Same inputs, a gentler coverage ramp and even weights: 60% coverage is 0.5 on the default ramp
        // ((60-40)/40) but 1.0 on a 40..60 ramp, which is exactly the calibration knob sessions need.
        var lenient = new Scorer(new ScoringOptions
        {
            CoverageFloor = 40,
            CoverageCeil = 60,
            MutationFloor = 0,
            MutationCeil = 100,
            CoverageWeight = 0.5,
            CoreWeight = 0.5,
        });

        var counts = new TestCounts(1, 0, 0);
        var coverage = new CoverageStats(60, 100);
        var mutation = new MutationStats(5, 5, 0, 0, 0);

        var strict = Default.Score("p", counts, coverage, mutation);
        var relaxed = lenient.Score("p", counts, coverage, mutation);

        Assert.Equal(0.5, strict.ScaledCoverage, 6);
        Assert.Equal(1.0, relaxed.ScaledCoverage, 6);
        Assert.True(relaxed.Quality > strict.Quality);

        // Hand-check the lenient case: scaled 1.0, passingCoverage 1.0, killRate 50% -> mutationScore 0.5,
        // core = sqrt(1.0*0.5) = 0.7071068, quality = 0.5 + 0.5*0.7071068 = 0.8535534.
        Assert.Equal(0.8536, relaxed.Quality, 4);
    }

    [Fact]
    public void CoverageAboveTheCeiling_DoesNotExceedOne()
    {
        var score = Default.Score("p", new TestCounts(1, 0, 0), new CoverageStats(100, 100), new MutationStats(1, 0, 0, 0, 0));

        Assert.Equal(1.0, score.ScaledCoverage);
        Assert.True(score.Quality <= 1.0);
    }

    [Fact]
    public void NonFiniteInputs_ScoreZeroInsteadOfPoisoningTheJson()
    {
        // Two traps at once: Math.Clamp(NaN, 0, 1) returns NaN rather than clamping, and System.Text.Json
        // throws on NaN - so one divide-by-zero upstream would turn a scored run into a failed one with an
        // opaque serialization error.
        var score = new Scorer(new ScoringOptions { CoverageWeight = double.NaN, CoreWeight = 0.7 })
            .Score("p", new TestCounts(1, 0, 0), new CoverageStats(100, 100), new MutationStats(1, 0, 0, 0, 0));

        Assert.True(double.IsFinite(score.Quality));
        Assert.True(double.IsFinite(score.Core));
    }
}
