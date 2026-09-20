using System.Text.Json;
using Skill.Suite.Marker.Model;
using Skill.Suite.TestLog.Protocol;

namespace Skill.Suite.Marker.Readers;

/// <summary>
/// Reads a Stryker.NET JSON mutation report into per-part and per-fixture mutation outcomes.
/// </summary>
public static class StrykerReader
{
    private const string FilesProperty = "files";
    private const string MutantsProperty = "mutants";
    private const string StatusProperty = "status";
    private const string TestFilesProperty = "testFiles";
    private const string TestsProperty = "tests";
    private const string IdProperty = "id";
    private const string NameProperty = "name";
    private const string CoveredByProperty = "coveredBy";
    private const string KilledByProperty = "killedBy";

    private const string KilledStatus = "Killed";
    private const string SurvivedStatus = "Survived";
    private const string TimeoutStatus = "Timeout";
    private const string NoCoverageStatus = "NoCoverage";

    /// <summary>Reads the report and sums mutant outcomes per part plus overall.</summary>
    /// <param name="path">Report path. A missing path yields empty stats.</param>
    /// <param name="router">Routes a source file path to a part.</param>
    /// <returns>Outcomes keyed by part id, always including <see cref="Map.MarkingMap.OverallPart"/>.</returns>
    /// <remarks>
    /// A malformed report is skipped like a malformed TRX or Cobertura file rather than failing the run.
    /// The original implementation parsed this one outside its try/catch, so a truncated Stryker report
    /// turned a scored submission into a failed run — inconsistent with how the other two readers behave,
    /// and the harsher outcome for the least essential input.
    /// </remarks>
    public static IReadOnlyDictionary<string, MutationStats> Read(string? path, PartRouter router)
    {
        var mutation = new Dictionary<string, MutationStats>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return mutation;

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            Accumulate(document, router, mutation);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return mutation;
        }

        return mutation;
    }

    /// <summary>
    /// Attributes the same report's mutants to the individual test classes that reached and detected them.
    /// </summary>
    /// <param name="path">Report path. A missing path yields <see cref="FixtureMutation.Unavailable"/>.</param>
    /// <returns>Per-fixture outcomes, or <see cref="FixtureMutation.Unavailable"/> when the report cannot support them.</returns>
    /// <remarks>
    /// <para>
    /// One report, read a second way — Stryker runs once and already knows which test touched which mutant.
    /// That attribution lives in <c>testFiles[*].tests[]</c>, which maps a test id to its name, and in each
    /// mutant's <c>coveredBy</c> and <c>killedBy</c> id lists. Stryker writes both only when coverage analysis
    /// is on; when it is off the report is still perfectly valid for the rollup and carries nothing here, which
    /// is exactly the case <see cref="FixtureMutation.PerTest"/> exists to distinguish from a measured zero.
    /// </para>
    /// <para>
    /// A mutant contributes to a fixture once, however many of its tests were involved, and only when that
    /// fixture actually reached it. So a fixture's totals describe the mutants it is responsible for, and a
    /// small, focused test class is not punished for the code it deliberately does not touch.
    /// </para>
    /// <para>
    /// <b>Bail must be off for these numbers to mean anything.</b> With bail on, Stryker stops a mutant's test
    /// run at the first killing test, so <c>killedBy</c> names that one test and every other fixture that would
    /// also have caught it is recorded as having let it survive.
    /// </para>
    /// </remarks>
    public static FixtureMutation ReadByFixture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return FixtureMutation.Unavailable;

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            return AttributeToFixtures(document);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return FixtureMutation.Unavailable;
        }
    }

    private static void Accumulate(
        JsonDocument document, PartRouter router, Dictionary<string, MutationStats> mutation)
    {
        if (!document.RootElement.TryGetProperty(FilesProperty, out var files) ||
            files.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var file in files.EnumerateObject())
        {
            // Stryker keys files by absolute path.
            var part = router.Route(file.Name);

            if (!file.Value.TryGetProperty(MutantsProperty, out var mutants) ||
                mutants.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var delta = default(MutationStats);
            foreach (var mutant in mutants.EnumerateArray())
                delta = delta.Add(Classify(Status(mutant)));

            if (delta == default) continue;

            if (part is not null)
                mutation[part] = mutation.GetValueOrDefault(part).Add(delta);

            mutation[Map.MarkingMap.OverallPart] =
                mutation.GetValueOrDefault(Map.MarkingMap.OverallPart).Add(delta);
        }
    }

    private static FixtureMutation AttributeToFixtures(JsonDocument document)
    {
        var fixtureByTestId = FixturesByTestId(document.RootElement);
        if (fixtureByTestId.Count == 0) return FixtureMutation.Unavailable;

        if (!document.RootElement.TryGetProperty(FilesProperty, out var files) ||
            files.ValueKind != JsonValueKind.Object)
        {
            return FixtureMutation.Unavailable;
        }

        var byFixture = new Dictionary<string, MutationStats>(StringComparer.Ordinal);
        var sawCoverage = false;

        foreach (var file in files.EnumerateObject())
        {
            if (!file.Value.TryGetProperty(MutantsProperty, out var mutants) ||
                mutants.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var mutant in mutants.EnumerateArray())
            {
                var covered = FixturesOf(mutant, CoveredByProperty, fixtureByTestId);
                var killed = FixturesOf(mutant, KilledByProperty, fixtureByTestId);

                if (covered.Count > 0) sawCoverage = true;

                // killedBy without coveredBy should not happen, but a fixture that demonstrably detected a
                // mutant must never be recorded as having missed it.
                covered.UnionWith(killed);
                if (covered.Count == 0) continue;

                var status = Status(mutant);

                foreach (var fixture in covered)
                {
                    var delta = killed.Contains(fixture)
                        ? new MutationStats(1, 0, 0, 0, 0)
                        // A timeout is a detection the runner could not attribute to a test, so it is credited
                        // to every fixture that reached the mutant - the same rule MutationStats applies to the
                        // rollup, where a timeout counts towards the kill rate.
                        : status == TimeoutStatus
                            ? new MutationStats(0, 0, 1, 0, 0)
                            : new MutationStats(0, 1, 0, 0, 0);

                    byFixture[fixture] = byFixture.GetValueOrDefault(fixture).Add(delta);
                }
            }
        }

        // A report whose mutants were all NoCoverage is legitimately empty; one where no mutant carries a
        // coveredBy array at all is a report that cannot answer this question, and saying so beats publishing
        // a page of zeroes.
        return sawCoverage ? new FixtureMutation(byFixture, PerTest: true) : FixtureMutation.Unavailable;
    }

    /// <summary>Maps Stryker's test ids onto fixture names.</summary>
    /// <remarks>
    /// The test's own name is authoritative: it is the vstest fully-qualified name, whose second-to-last
    /// segment is the declaring class. The file it was found in is the fallback for a runner that reports a
    /// bare display name, on the one-class-per-file convention every session template follows.
    /// </remarks>
    private static Dictionary<string, string> FixturesByTestId(JsonElement root)
    {
        var fixtures = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!root.TryGetProperty(TestFilesProperty, out var testFiles) ||
            testFiles.ValueKind != JsonValueKind.Object)
        {
            return fixtures;
        }

        foreach (var testFile in testFiles.EnumerateObject())
        {
            var fromFileName = FixtureNames.FromTypeName(
                Path.GetFileNameWithoutExtension(testFile.Name.AsSpan()).ToString());

            if (!testFile.Value.TryGetProperty(TestsProperty, out var tests) ||
                tests.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var test in tests.EnumerateArray())
            {
                var id = String(test, IdProperty);
                if (id is null) continue;

                var fixture = FixtureNames.FromTestName(String(test, NameProperty)) ?? fromFileName;
                if (fixture is not null) fixtures[id] = fixture;
            }
        }

        return fixtures;
    }

    private static HashSet<string> FixturesOf(
        JsonElement mutant, string property, IReadOnlyDictionary<string, string> fixtureByTestId)
    {
        var fixtures = new HashSet<string>(StringComparer.Ordinal);

        if (!mutant.TryGetProperty(property, out var ids) || ids.ValueKind != JsonValueKind.Array)
            return fixtures;

        foreach (var id in ids.EnumerateArray())
        {
            var key = id.ValueKind switch
            {
                JsonValueKind.String => id.GetString(),
                // Some emitters write numeric ids; the map is keyed by their textual form either way.
                JsonValueKind.Number => id.GetRawText(),
                _ => null,
            };

            if (key is not null && fixtureByTestId.TryGetValue(key, out var fixture))
                fixtures.Add(fixture);
        }

        return fixtures;
    }

    private static string? Status(JsonElement mutant) => String(mutant, StatusProperty);

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static MutationStats Classify(string? status) => status switch
    {
        KilledStatus => new MutationStats(1, 0, 0, 0, 0),
        SurvivedStatus => new MutationStats(0, 1, 0, 0, 0),
        TimeoutStatus => new MutationStats(0, 0, 1, 0, 0),
        NoCoverageStatus => new MutationStats(0, 0, 0, 1, 0),
        // CompileError, Ignored, missing or unknown status.
        _ => new MutationStats(0, 0, 0, 0, 1),
    };
}
