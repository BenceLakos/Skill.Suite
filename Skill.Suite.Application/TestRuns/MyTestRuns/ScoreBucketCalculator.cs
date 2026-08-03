using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.MyTestRuns;

internal static class ScoreBucketCalculator
{
    public static ScoreBucket ForFixture(TestFixtureResult fixture)
    {
        // Metric fixtures (per-part: one per declared scoring part, plus "overall") carry an explicit
        // composite quality from the judge's score event. That is the black-box path.
        if (fixture.Quality is { } quality)
            return BucketFor(quality * 100.0);

        // Diagnostics carry no score at all, and metric parts without a quality have nothing to show.
        if (fixture.Kind is not TestFixtureKind.Tests)
            return ScoreBucket.None;

        // Otherwise roll up aspects. The unit of grading is the aspect, not the test: an aspect is
        // satisfied only when every test claiming it passes, which is the same rule skill-marker applies
        // to the CIS report - so the competitor's bar and the marking row can never disagree.
        //
        // Only aspects the suite marked CompetitorVisible count. That flag is the dial for how much a
        // submission leaks: a handful of visible aspects gives a responsive signal, marking them all makes
        // an individual test flip almost never move the displayed bucket.
        var visibleAspects = fixture.UnitTests
            .Where(t => t.AspectCompetitorVisible && !string.IsNullOrWhiteSpace(t.Aspect))
            .GroupBy(t => t.Aspect!, StringComparer.Ordinal)
            .ToList();

        if (visibleAspects.Count == 0)
            return ScoreBucket.None;

        var satisfied = visibleAspects.Count(aspect => aspect.All(t => t.Outcome == TestOutcome.Passed));

        return BucketFor((double)satisfied / visibleAspects.Count * 100.0);
    }

    private static ScoreBucket BucketFor(double percentage) => percentage switch
    {
        < 25 => ScoreBucket.VeryLow,
        < 50 => ScoreBucket.Low,
        < 75 => ScoreBucket.High,
        _ => ScoreBucket.VeryHigh,
    };
}
