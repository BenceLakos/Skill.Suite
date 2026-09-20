namespace Skill.Suite.Infra.Sql;

using System.Text.RegularExpressions;

/// <summary>
/// Cuts a script into the batches <c>SqlCommand</c> can send, at the <c>GO</c> lines it cannot.
/// </summary>
/// <remarks>
/// <c>GO</c> is not T-SQL. It is a separator <c>sqlcmd</c> and Management Studio act on themselves, and a
/// script exported from either is full of them — sent as written it fails with a syntax error on the first
/// one. It also matters for what the batches contain: <c>CREATE PROCEDURE</c>, <c>CREATE VIEW</c> and
/// <c>CREATE SCHEMA</c> have to be the only statement in theirs, which is exactly what the author separated
/// them for.
/// <para>
/// Deliberately naive: a line is a separator when the line itself is <c>GO</c>, whatever came before it. A
/// <c>GO</c> alone on a line inside a string literal or a block comment is therefore split on, and the two
/// halves fail to parse. Recognising it would mean lexing T-SQL — strings, brackets, nested block comments —
/// to serve a case no seed script has, and a wrong lexer would corrupt scripts that are fine today instead of
/// refusing one that is odd. <c>sqlcmd</c> draws the line in the same place.
/// </para>
/// </remarks>
internal static class SqlBatchSplitter
{
    /// <summary>
    /// A line that is nothing but <c>GO</c>, optionally followed by the count of times to repeat the batch.
    /// </summary>
    /// <remarks>
    /// Case-insensitive and tolerant of surrounding whitespace, because an exported script's separators are
    /// indented as often as not. The count is what <c>GO 5</c> means to <c>sqlcmd</c>: run what came before
    /// five times — used by seed scripts that insert a block of sample rows repeatedly.
    /// </remarks>
    private static readonly Regex SeparatorLine = new(
        @"^\s*GO(?:\s+(\d+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>A count of zero or a missing one both mean the batch runs once.</summary>
    private const int DefaultRepeatCount = 1;

    /// <summary>
    /// The batches to execute in order, with everything that would send nothing dropped.
    /// </summary>
    /// <remarks>
    /// Whitespace-only batches are left out rather than sent: a script that ends with a <c>GO</c>, or has two
    /// in a row, would otherwise produce an empty command whose only effect is a round trip.
    /// </remarks>
    public static IReadOnlyList<string> Split(string script)
    {
        var batches = new List<string>();

        if (string.IsNullOrWhiteSpace(script))
            return batches;

        // The lines are kept exactly as they arrived, carriage returns and all, and rejoined with the newline
        // that separated them. What reaches the server is the author's script minus its separators.
        var current = new List<string>();

        foreach (var line in script.Split('\n'))
        {
            var separator = SeparatorLine.Match(line.TrimEnd('\r'));

            if (!separator.Success)
            {
                current.Add(line);
                continue;
            }

            Flush(batches, current, RepeatCount(separator));
            current.Clear();
        }

        Flush(batches, current, DefaultRepeatCount);

        return batches;
    }

    private static void Flush(List<string> batches, List<string> lines, int repeat)
    {
        var batch = string.Join('\n', lines).Trim();

        if (batch.Length == 0)
            return;

        for (var i = 0; i < repeat; i++)
            batches.Add(batch);
    }

    private static int RepeatCount(Match separator) =>
        separator.Groups[1].Success
        && int.TryParse(separator.Groups[1].ValueSpan, out var count)
        && count > 0
            ? count
            : DefaultRepeatCount;
}
