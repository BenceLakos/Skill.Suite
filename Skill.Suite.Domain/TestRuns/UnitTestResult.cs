using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.TestRuns;

public sealed class UnitTestResult : Entity<Guid>
{
    private UnitTestResult() { }

    public Guid FixtureId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TestOutcome Outcome { get; private set; }
    public long? DurationMs { get; private set; }
    public string? Error { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    /// <summary>
    /// The marking-scheme aspect this test case belongs to, declared by the suite through its
    /// <c>[Aspect]</c> attribute. Null for an ungraded test.
    /// </summary>
    /// <remarks>
    /// Many tests map to one aspect, never the reverse: an aspect is satisfied only when every test
    /// claiming it passes. This replaces the older test-name prefix convention entirely.
    /// </remarks>
    public string? Aspect { get; private set; }

    /// <summary>
    /// Whether this test's aspect contributes to the score the competitor sees for their own submission.
    /// Grading is unaffected either way; this only controls how much feedback a submission leaks.
    /// </summary>
    public bool AspectCompetitorVisible { get; private set; }

    public List<TestEventRecord> Events { get; private set; } = new();

    internal static UnitTestResult Start(
        Guid fixtureId, string name, DateTime startedAt,
        string? aspect = null, bool aspectCompetitorVisible = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            FixtureId = fixtureId,
            Name = TestRunLimits.TruncateName(name),
            Outcome = TestOutcome.Unknown,
            StartedAt = startedAt,
            Aspect = TestRunLimits.TruncateAspect(aspect),
            AspectCompetitorVisible = aspectCompetitorVisible,
        };

    /// <summary>
    /// Records the aspect if it has not been seen yet. The producer emits it on both
    /// <c>start-unit-test</c> and <c>finish-unit-test</c>, and either may be the first to arrive.
    /// </summary>
    internal void SetAspect(string? aspect, bool aspectCompetitorVisible)
    {
        if (string.IsNullOrWhiteSpace(aspect)) return;

        Aspect = TestRunLimits.TruncateAspect(aspect);
        // Visibility ORs: if any event for this test says the aspect is visible, treat it as visible.
        AspectCompetitorVisible = AspectCompetitorVisible || aspectCompetitorVisible;
    }

    internal void Finish(TestOutcome outcome, long durationMs, string? error, DateTime finishedAt)
    {
        // A previously-observed throw or failing assertion is concrete evidence the
        // test broke. Never let a softer finish-unit-test outcome (e.g. "passed", which
        // some judge images emit when they swallow exceptions) overwrite that.
        if (!IsBroken(Outcome))
            Outcome = outcome;

        DurationMs = durationMs;
        Error = error ?? Error;
        FinishedAt = finishedAt;
    }

    internal void AppendEvent(TestEventRecord record)
    {
        Events = [.. Events, record];

        // Promote the outcome eagerly the moment we see hard evidence of a failure, so
        // the test cannot end up "passed" just because the judge image didn't bother
        // emitting a matching finish-unit-test or got the outcome string wrong.
        switch (record)
        {
            case { Kind: TestEventKind.Call, Threw: { Length: > 0 } threw }:
                Outcome = TestOutcome.Errored;
                Error ??= threw;
                break;

            case { Kind: TestEventKind.Assertion, Passed: false } when Outcome != TestOutcome.Errored:
                Outcome = TestOutcome.Failed;
                break;
        }
    }

    private static bool IsBroken(TestOutcome outcome) =>
        outcome is TestOutcome.Errored or TestOutcome.Failed;
}
