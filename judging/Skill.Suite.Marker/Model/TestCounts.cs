namespace Skill.Suite.Marker.Model;

/// <summary>
/// Test verdict tallies for one part.
/// </summary>
/// <param name="Passed">Tests that passed.</param>
/// <param name="Failed">Tests that failed, errored, timed out, or aborted.</param>
/// <param name="Skipped">Tests that never produced a verdict, excluded from <see cref="Total"/>.</param>
/// <remarks>
/// Skipped tests are counted but kept out of the pass-rate denominator: a skip is not a verdict, so it
/// should neither help nor hurt. They are still reported, so an all-skipped suite is visible rather than
/// silently scoring zero.
/// </remarks>
public readonly record struct TestCounts(int Passed, int Failed, int Skipped)
{
    /// <summary>Tests with a real verdict — the pass-rate denominator.</summary>
    public int Total => Passed + Failed;

    /// <summary>Fraction of decided tests that passed; 0 when nothing was decided.</summary>
    public double PassRate => Total == 0 ? 0.0 : (double)Passed / Total;

    /// <summary>Adds another tally to this one.</summary>
    public TestCounts Add(TestCounts other) =>
        new(Passed + other.Passed, Failed + other.Failed, Skipped + other.Skipped);
}
