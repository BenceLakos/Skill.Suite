using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
using Skill.Suite.Marker.Model;

namespace Skill.Suite.Marker.Readers;

/// <summary>
/// Reads Cobertura coverage reports into per-part line-coverage totals.
/// </summary>
public static class CoberturaReader
{
    /// <summary>Reads every given report and sums coverage per part plus overall.</summary>
    /// <param name="paths">Cobertura file paths. Missing and unparsable files are skipped.</param>
    /// <param name="router">Routes a source file path to a part.</param>
    /// <returns>Totals keyed by part id, always including <see cref="Map.MarkingMap.OverallPart"/>.</returns>
    /// <remarks>
    /// <para>
    /// Reads <c>class/lines/line</c> as direct children rather than descending recursively. Coverlet emits
    /// every line twice — once under each method and once at class level — so a recursive read doubles both
    /// numerator and denominator. The rate survives that, but the reported line counts are then nonsense;
    /// with the direct read they equal the report's own <c>lines-valid</c> / <c>lines-covered</c>
    /// attributes, which is the acceptance oracle in the tests.
    /// </para>
    /// <para>
    /// Byte-identical reports are counted <b>once</b>, whatever path they arrived under. A caller globbing
    /// <c>TestResults</c> for <c>coverage.cobertura.xml</c> finds the same report twice — coverlet's own
    /// output under a run GUID, and the copy VSTest attaches to the TRX under <c>&lt;run&gt;/In/&lt;host&gt;/</c>
    /// — which doubled both totals and left them disagreeing with the report's own <c>lines-valid</c> /
    /// <c>lines-covered</c>. The rate hid it, so the bug reached the platform's stored counts. Deduplicating
    /// here rather than only in the caller's <c>find</c> keeps that true for any session author who globs by
    /// hand. Two <i>different</i> reports still sum, so genuinely multi-project runs are unaffected.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<string, CoverageStats> Read(IEnumerable<string> paths, PartRouter router)
    {
        var coverage = new Dictionary<string, CoverageStats>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;

            if (ContentHash(path) is { } hash && !seen.Add(hash)) continue;

            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException)
            {
                continue;
            }

            Accumulate(document, router, coverage);
        }

        return coverage;
    }

    /// <summary>
    /// Hex SHA-256 of the file's bytes, or null when it cannot be read — an unreadable file is left to
    /// <see cref="XDocument.Load(string)"/> to reject, exactly as before, rather than being deduplicated
    /// against anything.
    /// </summary>
    private static string? ContentHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Accumulate(
        XDocument document, PartRouter router, Dictionary<string, CoverageStats> coverage)
    {
        // Coverlet's Cobertura has no default namespace, but honour one if some other tool emits it.
        var ns = document.Root?.GetDefaultNamespace() ?? XNamespace.None;

        foreach (var element in document.Descendants(ns + "class"))
        {
            var fileName = element.Attribute("filename")?.Value;
            var part = router.Route(fileName);

            var covered = 0;
            var total = 0;

            foreach (var line in element.Element(ns + "lines")?.Elements(ns + "line") ?? [])
            {
                total++;
                var hits = line.Attribute("hits")?.Value;
                if (int.TryParse(hits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0)
                    covered++;
            }

            if (total == 0) continue;

            var delta = new CoverageStats(covered, total);

            if (part is not null)
                coverage[part] = coverage.GetValueOrDefault(part).Add(delta);

            coverage[Map.MarkingMap.OverallPart] =
                coverage.GetValueOrDefault(Map.MarkingMap.OverallPart).Add(delta);
        }
    }
}
