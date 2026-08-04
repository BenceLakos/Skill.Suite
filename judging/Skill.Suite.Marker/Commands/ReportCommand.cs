using System.Globalization;
using System.Text;
using Skill.Suite.Marker.Cli;
using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Model;
using Skill.Suite.Marker.Output;
using Skill.Suite.Marker.Readers;

namespace Skill.Suite.Marker.Commands;

/// <summary>
/// The <c>report</c> subcommand: per-aspect yes/no for CIS.
/// </summary>
public static class ReportCommand
{
    /// <summary>CSV header, in the order CIS expects.</summary>
    public const string Header = "aspect_id,result,tests_matched,tests_passed";

    /// <summary>Runs the command.</summary>
    /// <param name="command">The parsed command line.</param>
    /// <param name="map">The validated marking map.</param>
    public static void Run(MarkerCommand command, MarkingMap map)
    {
        var units = EventReplay.Read(command.EventsPath);
        var results = Aggregate(units, map);
        Write(command.OutPath, results);
    }

    /// <summary>Groups units by aspect and produces one row per aspect.</summary>
    /// <param name="units">Replayed unit verdicts.</param>
    /// <param name="map">The marking map, whose declared aspects fix the row set and their order.</param>
    /// <returns>One row per aspect: every declared aspect, then any undeclared ones found in the stream.</returns>
    /// <remarks>
    /// Declared aspects with no matching test still get a row, reported <c>no</c> — otherwise an aspect
    /// whose tests were deleted or never written would silently disappear from the report instead of
    /// showing up as unearned. Aspects found in the stream but absent from the map are appended, so a
    /// stale map is visible rather than quietly dropping results.
    /// </remarks>
    public static IReadOnlyList<AspectResult> Aggregate(IReadOnlyList<UnitRecord> units, MarkingMap map)
    {
        var graded = units
            .Where(unit => !string.IsNullOrWhiteSpace(unit.Aspect))
            .GroupBy(unit => unit.Aspect!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new AspectResult(
                    group.Key,
                    group.Count(),
                    group.Count(unit => unit.Outcome == UnitOutcome.Passed)),
                StringComparer.Ordinal);

        var rows = new List<AspectResult>();
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var aspect in map.Aspects)
        {
            declared.Add(aspect.Id);
            rows.Add(graded.GetValueOrDefault(aspect.Id) ?? new AspectResult(aspect.Id, 0, 0));
        }

        rows.AddRange(graded.Values
            .Where(result => !declared.Contains(result.AspectId))
            .OrderBy(result => result.AspectId, StringComparer.Ordinal));

        return rows;
    }

    private static void Write(string path, IReadOnlyList<AspectResult> results)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var csv = new StringBuilder();
        csv.Append(Header).Append('\n');

        foreach (var result in results)
        {
            csv.Append(Escape(result.AspectId)).Append(',')
                .Append(result.Result).Append(',')
                .Append(result.TestsMatched.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.TestsPassed.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Quotes a field only when it needs it, so ordinary ids stay readable.</summary>
    private static string Escape(string value) =>
        value.AsSpan().IndexOfAny(NeedsQuoting) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    private static readonly System.Buffers.SearchValues<char> NeedsQuoting =
        System.Buffers.SearchValues.Create(",\"\n\r");
}
