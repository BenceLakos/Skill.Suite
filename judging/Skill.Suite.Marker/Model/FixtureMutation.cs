namespace Skill.Suite.Marker.Model;

/// <summary>
/// Per-fixture mutation outcomes, together with whether the report could support them at all.
/// </summary>
/// <param name="ByFixture">Outcomes keyed by fixture name. Empty when <paramref name="PerTest"/> is false.</param>
/// <param name="PerTest">
/// Whether the report carried the per-test data this attribution needs — a <c>testFiles</c> section naming the
/// tests, and <c>coveredBy</c> on the mutants. Stryker only writes those with <c>coverage-analysis</c> on.
/// </param>
/// <remarks>
/// The flag exists so an absent capability cannot be mistaken for a measured zero. Without it, a report from an
/// older Stryker — or one configured with coverage analysis off — would attribute nothing to any fixture, and
/// every test class in the submission would be published as having killed no mutants at all.
/// </remarks>
public sealed record FixtureMutation(IReadOnlyDictionary<string, MutationStats> ByFixture, bool PerTest)
{
    /// <summary>The result for a report that cannot be attributed per test.</summary>
    public static FixtureMutation Unavailable { get; } =
        new(new Dictionary<string, MutationStats>(StringComparer.Ordinal), PerTest: false);
}
