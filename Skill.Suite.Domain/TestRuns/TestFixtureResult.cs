using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.TestRuns;

public sealed class TestFixtureResult : Entity<Guid>
{
    private TestFixtureResult() { }

    public Guid TestRunId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this row is a real test class, a scoring part, or a judge diagnostic. Lookups are
    /// kind-scoped so a metric part cannot collide with a test class of the same name.
    /// </summary>
    public TestFixtureKind Kind { get; private set; }
    public int TestsRun { get; private set; }
    public int TestsPassed { get; private set; }
    public int TestsFailed { get; private set; }
    public long? DurationMs { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    /// <summary>
    /// Composite quality score in [0, 1] for the part this fixture represents. Populated
    /// from a <c>score</c> metric event; null for regular test fixtures.
    /// </summary>
    public double? Quality { get; private set; }

    /// <summary>
    /// Line coverage in [0, 1] this fixture achieved on its own. Populated from a fixture-scoped
    /// <c>coverage</c> metric event; null for scoring parts and for any fixture the judge did not measure
    /// individually.
    /// </summary>
    /// <remarks>
    /// A measurement, not a verdict — deliberately separate from <see cref="Quality"/>, which is a composite
    /// the marking map authorises for a scoring part. Nothing computes a mark from this.
    /// </remarks>
    public double? LineCoverage { get; private set; }

    /// <summary>
    /// Mutation kill rate in [0, 1] for the mutants this fixture's tests reached. Populated from a
    /// fixture-scoped <c>mutation</c> metric event; null when it was not measured.
    /// </summary>
    /// <remarks>
    /// The denominator is what this fixture covered, not every mutant in the submission, so a small focused
    /// test class is not scored against code it never claimed to test.
    /// </remarks>
    public double? MutationScore { get; private set; }

    public List<UnitTestResult> UnitTests { get; private set; } = new();

    internal static TestFixtureResult Start(
        Guid testRunId, string name, DateTime startedAt, TestFixtureKind kind = TestFixtureKind.Tests) =>
        new()
        {
            Id = Guid.NewGuid(),
            TestRunId = testRunId,
            // Truncated here rather than trusted: a name longer than the column would throw on save and
            // take the whole run's results with it.
            Name = TestRunLimits.TruncateName(name),
            Kind = kind,
            StartedAt = startedAt,
        };

    internal void Finish(int testsRun, int testsPassed, int testsFailed, long durationMs, DateTime finishedAt)
    {
        // Trust the per-unit outcomes over the judge image's totals. The judge can
        // miscount (e.g. it reports "passed" for a test whose call threw); after
        // UnitTestResult.AppendEvent promotes such tests to Errored, recomputing here
        // keeps fixture rollups consistent with what each test row actually shows.
        // The arguments are kept in the signature only so callers that don't (yet)
        // emit a sensible total don't have to change.
        _ = testsRun;
        _ = testsPassed;
        _ = testsFailed;

        TestsRun = UnitTests.Count;
        TestsPassed = UnitTests.Count(t => t.Outcome == TestOutcome.Passed);
        TestsFailed = UnitTests.Count(t => t.Outcome is TestOutcome.Failed or TestOutcome.Errored);

        DurationMs = durationMs;
        FinishedAt = finishedAt;
    }

    internal UnitTestResult StartUnitTest(
        string name, DateTime startedAt, string? aspect = null, bool aspectCompetitorVisible = false)
    {
        var test = UnitTestResult.Start(Id, name, startedAt, aspect, aspectCompetitorVisible);
        UnitTests.Add(test);
        return test;
    }

    internal UnitTestResult? FindUnitTest(string name) =>
        UnitTests.LastOrDefault(t => t.Name == name);

    internal void SetQuality(double quality) =>
        Quality = Math.Clamp(quality, 0.0, 1.0);

    internal void SetLineCoverage(double coverage) =>
        LineCoverage = Math.Clamp(coverage, 0.0, 1.0);

    internal void SetMutationScore(double mutationScore) =>
        MutationScore = Math.Clamp(mutationScore, 0.0, 1.0);
}
