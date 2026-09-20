using Skill.Suite.Marker.Model;

namespace Skill.Suite.Marker.Readers;

/// <summary>
/// Reads the per-test-class Cobertura reports into line-coverage totals keyed by fixture.
/// </summary>
/// <remarks>
/// <para>
/// The layout is a directory whose <b>immediate subdirectories are named after test classes</b>, each holding
/// that class's own coverage run:
/// </para>
/// <code>
/// fixture-coverage/
///   CalculatorTests/&lt;guid&gt;/coverage.cobertura.xml
///   DescribeTests/&lt;guid&gt;/coverage.cobertura.xml
/// </code>
/// <para>
/// A directory rather than repeated <c>Name=path</c> pairs, because the caller is a shell script in a judge
/// image. Coverlet buries its report under a per-run GUID directory that nothing can predict, so a pair-based
/// flag would force the entrypoint to run a <c>find</c> per class and splice the results into an argument
/// list — quoting, empty matches and word splitting all in the one place where a mistake silently scores every
/// competitor zero. Naming the parent directory once moves that search in here, where it is ordinary code.
/// </para>
/// <para>
/// Parsing is <see cref="CoberturaReader"/>'s, not a second copy of it: the direct-child <c>&lt;line&gt;</c>
/// counting rule that keeps Coverlet's duplicated lines from doubling both totals has to be identical, or a
/// fixture's numbers would not be comparable with the part and <c>overall</c> numbers beside them.
/// </para>
/// </remarks>
public static class FixtureCoverageReader
{
    private const string CoberturaSearchPattern = "*.cobertura.xml";

    /// <summary>Reads every fixture subdirectory under <paramref name="directory"/>.</summary>
    /// <param name="directory">
    /// The parent directory. Null, blank and absent directories yield no results rather than failing: a
    /// session that does not run per-fixture coverage is not in error.
    /// </param>
    /// <returns>Totals keyed by fixture name, holding only fixtures that had a readable report.</returns>
    /// <remarks>
    /// The whole report is summed — every class in it, routed or not — because the question a fixture-scoped
    /// coverage number answers is "how much of the code under test did this one test class reach", which is the
    /// same population the <c>overall</c> rollup measures.
    /// </remarks>
    public static IReadOnlyDictionary<string, CoverageStats> Read(string? directory)
    {
        var coverage = new Dictionary<string, CoverageStats>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return coverage;

        foreach (var fixtureDirectory in Directory.EnumerateDirectories(directory))
        {
            var fixture = Path.GetFileName(fixtureDirectory);
            if (string.IsNullOrWhiteSpace(fixture)) continue;

            string[] reports;
            try
            {
                reports = Directory.GetFiles(fixtureDirectory, CoberturaSearchPattern, SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (reports.Length == 0) continue;

            // An empty router: routing is a part concern, and this reader only ever wants the rollup.
            var totals = CoberturaReader.Read(reports, new PartRouter([]))
                .GetValueOrDefault(Map.MarkingMap.OverallPart);

            // A report with no coverable lines at all is indistinguishable from a run that never happened, and
            // 0/0 on the wire reads as "this fixture covered nothing" rather than "nothing was measured".
            if (totals.LinesTotal == 0) continue;

            coverage[fixture] = coverage.GetValueOrDefault(fixture).Add(totals);
        }

        return coverage;
    }
}
