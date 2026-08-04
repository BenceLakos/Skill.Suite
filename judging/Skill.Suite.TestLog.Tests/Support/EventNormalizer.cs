using System.Text.RegularExpressions;

namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// Replaces the wall-clock parts of an event line so golden comparisons are deterministic.
/// </summary>
internal static partial class EventNormalizer
{
    internal const string TimestampPlaceholder = "<ts>";
    internal const string DurationPlaceholder = "<ms>";

    /// <summary>Normalizes one event line: timestamp and any duration become placeholders.</summary>
    internal static string Normalize(string line) =>
        DurationPattern().Replace(TimestampPattern().Replace(line, $"\"timestamp\":\"{TimestampPlaceholder}\""),
            $"\"durationMs\":\"{DurationPlaceholder}\"");

    /// <summary>Normalizes a whole file's worth of lines, dropping blanks.</summary>
    internal static string[] NormalizeAll(IEnumerable<string> lines) =>
        lines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Normalize(line.TrimEnd('\r')))
            .ToArray();

    [GeneratedRegex("\"timestamp\":\"[^\"]*\"")]
    private static partial Regex TimestampPattern();

    [GeneratedRegex("\"durationMs\":-?[0-9]+")]
    private static partial Regex DurationPattern();
}
