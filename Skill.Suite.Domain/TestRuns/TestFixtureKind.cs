namespace Skill.Suite.Domain.TestRuns;

/// <summary>
/// What a fixture row represents. The parser partitions three different things into the same table, and
/// they must not be confused with one another.
/// </summary>
/// <remarks>
/// Without this, a metric <c>part</c> that happens to share a name with a real test class would attach its
/// events to that class and overwrite its quality — and a judge diagnostic would be indistinguishable from
/// a test fixture in the competitor's own view.
/// </remarks>
public enum TestFixtureKind
{
    /// <summary>A real test class from the judged suite.</summary>
    Tests = 0,

    /// <summary>A scoring part carrying aggregate metrics (test-summary, coverage, mutation, score).</summary>
    Metrics = 1,

    /// <summary>Judge diagnostics: why a run failed, rather than how a submission scored.</summary>
    Diagnostics = 2,
}
