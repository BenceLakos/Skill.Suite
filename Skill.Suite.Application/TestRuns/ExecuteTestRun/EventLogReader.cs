namespace Skill.Suite.Application.TestRuns.ExecuteTestRun;

/// <summary>
/// Reads a judgement container's JSON-lines event log into memory under a hard size bound.
/// </summary>
/// <remarks>
/// <para>
/// The size of that file is chosen by the competitor, not by the judge: the xUnit harness writes it from inside
/// their own test process, which is why <c>LOG_DIRECTORY</c> has to be writable by the account their tests run
/// as. Reading it with <c>File.ReadAllLinesAsync</c> therefore let a loop in a submission allocate an
/// arbitrarily large <c>string[]</c> inside the application process and take down every other competitor's
/// in-flight run along with the UI — the same attack the container runner already caps stderr against.
/// </para>
/// <para>
/// Truncation keeps the head and drops the tail. Events are written in stream order, so the head is the part
/// that describes what ran; a truncated run stays reportable, which is the point — refusing to read an
/// oversized log would let a competitor discard their own results just as effectively.
/// </para>
/// </remarks>
public static class EventLogReader
{
    /// <summary>Most lines ingested from one event log.</summary>
    /// <remarks>
    /// Far above any real suite: a session's hidden suite is tens to low hundreds of tests, each emitting a
    /// handful of events. Reaching this means a producer is looping.
    /// </remarks>
    public const int MaxLines = 200_000;

    /// <summary>Most characters ingested from one event log, across all lines.</summary>
    /// <remarks>
    /// Bounds the total allocation independently of the line count, so a single enormous line — or a few of
    /// them — cannot get past <see cref="MaxLines"/>.
    /// </remarks>
    public const int MaxCharacters = 64 * 1024 * 1024;

    /// <summary>Reads the log at <paramref name="path"/>, stopping at the limits.</summary>
    /// <param name="path">Path to the event log.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lines read, and whether anything was dropped.</returns>
    public static async Task<EventLogContent> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var characters = 0L;
        var truncated = false;

        using var reader = new StreamReader(path);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            // Tested before the line is kept, so an over-long line is dropped rather than admitted.
            if (lines.Count >= MaxLines || characters + line.Length > MaxCharacters)
            {
                truncated = true;
                break;
            }

            characters += line.Length;
            lines.Add(line);
        }

        return new EventLogContent(lines, truncated);
    }
}
