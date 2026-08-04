using Skill.Suite.Application.TestRuns.ExecuteTestRun;
using Skill.Suite.Application.TestRuns.MyTestRuns;
using Skill.Suite.Domain.TestRuns;
using Xunit;

namespace Skill.Suite.Application.Tests.TestRuns;

/// <summary>
/// Replays the judging layer's golden event streams through the real parser.
/// </summary>
/// <remarks>
/// This is the seam that keeps producer and consumer honest. The goldens are produced by
/// <c>Skill.Suite.TestLog</c> (net9.0) and consumed here (net10.0), so they cannot reference each other -
/// the shared file is the contract. A change on either side that breaks the other fails here rather than in
/// production against a competitor's submission.
/// </remarks>
public sealed class GoldenEventReplayTests
{
    [Fact]
    public void ProtocolV1Golden_ParsesIntoTheExpectedRun()
    {
        var run = Replay("protocol-v1.jsonl");

        var fixture = Assert.Single(run.Fixtures, f => f.Kind == TestFixtureKind.Tests);
        Assert.Equal("DemoTests", fixture.Name);
        Assert.Equal(4, fixture.TestsRun);
        Assert.Equal(2, fixture.TestsPassed);
        Assert.Equal(2, fixture.TestsFailed);

        Assert.Equal(TestOutcome.Passed, Unit(fixture, "Compute_TwoPlusTwo_ReturnsFour").Outcome);
        Assert.Equal(TestOutcome.Failed, Unit(fixture, "Compute_Overflow_Saturates").Outcome);
        Assert.Equal(TestOutcome.Errored, Unit(fixture, "Compute_NullInput_Throws").Outcome);
        Assert.Equal(TestOutcome.Passed, Unit(fixture, "Diagnostics_Smoke_DoesNotThrow").Outcome);
    }

    [Fact]
    public void ProtocolV1Golden_CarriesAspectsThrough()
    {
        var fixture = Replay("protocol-v1.jsonl").Fixtures.Single(f => f.Kind == TestFixtureKind.Tests);

        Assert.Equal("A1.1", Unit(fixture, "Compute_TwoPlusTwo_ReturnsFour").Aspect);
        Assert.True(Unit(fixture, "Compute_TwoPlusTwo_ReturnsFour").AspectCompetitorVisible);

        // Graded but hidden.
        Assert.Equal("A1.2", Unit(fixture, "Compute_Overflow_Saturates").Aspect);
        Assert.False(Unit(fixture, "Compute_Overflow_Saturates").AspectCompetitorVisible);

        // Ungraded.
        Assert.Null(Unit(fixture, "Diagnostics_Smoke_DoesNotThrow").Aspect);
    }

    [Fact]
    public void ProtocolV1Golden_LiftsQualityFromTheScoreEvent()
    {
        var run = Replay("protocol-v1.jsonl");

        var metrics = run.Fixtures.Single(f => f.Kind == TestFixtureKind.Metrics);
        Assert.Equal("overall", metrics.Name);
        Assert.Equal(0.5, metrics.Quality);
        Assert.Equal(ScoreBucket.High, ScoreBucketCalculator.ForFixture(metrics));
    }

    [Fact]
    public void ProtocolV1Golden_RollsUpTheVisibleAspect()
    {
        var fixture = Replay("protocol-v1.jsonl").Fixtures.Single(f => f.Kind == TestFixtureKind.Tests);

        // Visible aspects are A1.1 (passed) and A1.3 (errored) - A1.2 is hidden. One of two satisfied.
        Assert.Equal(ScoreBucket.High, ScoreBucketCalculator.ForFixture(fixture));
    }

    [Fact]
    public void ProtocolV1Golden_RecordsCallAndAssertionEvents()
    {
        var fixture = Replay("protocol-v1.jsonl").Fixtures.Single(f => f.Kind == TestFixtureKind.Tests);
        var passing = Unit(fixture, "Compute_TwoPlusTwo_ReturnsFour");

        Assert.Contains(passing.Events, e => e.Kind == TestEventKind.Call && e.Target == "ICalculator.Compute");
        Assert.Contains(passing.Events, e => e.Kind == TestEventKind.Assertion && e.Passed == true);
    }

    [Fact]
    public void SmokeImageGolden_ProducesExactlyWhatItsReadmePromises()
    {
        var run = Replay("expected-events.jsonl");

        var checks = run.Fixtures.Single(f => f.Name == "SmokeChecks");
        var errors = run.Fixtures.Single(f => f.Name == "SmokeErrors");

        Assert.Equal(2, checks.TestsRun);
        Assert.Equal(1, checks.TestsPassed);
        Assert.Equal(1, checks.TestsFailed);
        Assert.Equal(1, errors.TestsRun);
        Assert.Equal(TestOutcome.Errored, errors.UnitTests.Single().Outcome);

        // 1 of 2 visible aspects satisfied, and 0 of 1.
        Assert.Equal(ScoreBucket.High, ScoreBucketCalculator.ForFixture(checks));
        Assert.Equal(ScoreBucket.VeryLow, ScoreBucketCalculator.ForFixture(errors));

        var metrics = run.Fixtures.Single(f => f.Kind == TestFixtureKind.Metrics);
        Assert.Equal(0.5, metrics.Quality);
    }

    [Fact]
    public void EveryGoldenLineIsUnderstood()
    {
        // Guards against a producer-side addition landing that the parser silently drops. Counting the
        // fixtures and units a replay produces is the cheapest proxy for "nothing was ignored".
        var run = Replay("protocol-v1.jsonl");

        Assert.Equal(2, run.Fixtures.Count);
        Assert.Equal(4, run.Fixtures.Single(f => f.Kind == TestFixtureKind.Tests).UnitTests.Count);
    }

    private static UnitTestResult Unit(TestFixtureResult fixture, string name) =>
        fixture.UnitTests.Single(t => t.Name == name);

    private static TestRun Replay(string goldenName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Golden", goldenName);
        Assert.True(File.Exists(path), $"golden stream missing: {path}. Build the judging solution first.");

        var run = TestRun.Create(
            Guid.NewGuid(), Guid.NewGuid(), "https://example.test/r.git", "r", "main", "abc", "f", "img:1");

        foreach (var line in File.ReadAllLines(path))
            TestLogParser.Apply(run, line);

        return run;
    }
}
