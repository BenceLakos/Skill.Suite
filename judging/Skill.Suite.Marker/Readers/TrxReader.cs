using System.Xml.Linq;
using Skill.Suite.Marker.Model;

namespace Skill.Suite.Marker.Readers;

/// <summary>
/// Reads VSTest <c>.trx</c> result files into per-part verdict tallies.
/// </summary>
public static class TrxReader
{
    private const string TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Outcomes that count as a real failure. Notably includes <c>Timeout</c>, <c>Aborted</c> and
    /// <c>Error</c>: the original implementation dropped these entirely, so a suite whose host crashed on
    /// nine of ten tests reported a pass rate of 1.0. A test that hangs or crashes the runner is a broken
    /// test, not an absent one.
    /// </summary>
    private static readonly string[] FailureOutcomes =
    [
        "Failed", "Timeout", "Aborted", "Error", "NotRunnable",
        "Blocked", "Disconnected", "PassedButRunAborted", "Inconclusive", "Warning",
    ];

    /// <summary>Reads every given TRX file and tallies verdicts per part plus overall.</summary>
    /// <param name="paths">TRX file paths. Missing and unparsable files are skipped.</param>
    /// <param name="router">Routes a test's class name to a part.</param>
    /// <returns>Tallies keyed by part id, always including <see cref="Map.MarkingMap.OverallPart"/>.</returns>
    public static IReadOnlyDictionary<string, TestCounts> Read(IEnumerable<string> paths, PartRouter router)
    {
        var counts = new Dictionary<string, TestCounts>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;

            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException)
            {
                // A truncated TRX must not sink the whole run; the missing data shows up as a lower total.
                continue;
            }

            Accumulate(document, router, counts);
        }

        return counts;
    }

    private static void Accumulate(
        XDocument document, PartRouter router, Dictionary<string, TestCounts> counts)
    {
        var ns = document.Root?.GetDefaultNamespace() ?? XNamespace.None;
        if (ns == XNamespace.None) ns = TrxNamespace;

        // Pass 1: test id -> declaring class, which is what carries the part routing.
        var classNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var unitTest in document.Descendants(ns + "UnitTest"))
        {
            var id = unitTest.Attribute("id")?.Value;
            var className = unitTest.Element(ns + "TestMethod")?.Attribute("className")?.Value;
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(className))
                classNames[id] = className;
        }

        // Pass 2: verdicts.
        foreach (var result in document.Descendants(ns + "UnitTestResult"))
        {
            var outcome = result.Attribute("outcome")?.Value;
            var testId = result.Attribute("testId")?.Value;
            if (string.IsNullOrEmpty(outcome) || string.IsNullOrEmpty(testId)) continue;

            var delta = Classify(outcome);
            var className = classNames.GetValueOrDefault(testId);
            var part = router.Route(className);

            if (part is not null)
                counts[part] = counts.GetValueOrDefault(part).Add(delta);

            counts[Map.MarkingMap.OverallPart] =
                counts.GetValueOrDefault(Map.MarkingMap.OverallPart).Add(delta);
        }
    }

    private static TestCounts Classify(string outcome)
    {
        if (string.Equals(outcome, "Passed", StringComparison.Ordinal))
            return new TestCounts(1, 0, 0);

        return FailureOutcomes.Contains(outcome, StringComparer.Ordinal)
            ? new TestCounts(0, 1, 0)
            // NotExecuted (an xUnit Skip), Pending, InProgress, Completed, or anything unrecognised.
            : new TestCounts(0, 0, 1);
    }
}
