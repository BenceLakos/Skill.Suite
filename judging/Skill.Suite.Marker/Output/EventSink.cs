using Skill.Suite.TestLog.Protocol;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Skill.Suite.TestLog;

namespace Skill.Suite.Marker.Output;

/// <summary>
/// Adds events to an existing <c>events.jsonl</c> without disturbing what is already in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Adds, never truncates.</b> The marker runs after <c>dotnet test</c>, and the platform reads the whole
/// file once the container exits. Erasing it — as the original implementation did by opening with
/// <c>FileMode.Create</c> — leaves a submission that appears to have executed no tests at all.
/// </para>
/// <para>
/// New events are buffered and the file is rewritten once, on flush, rather than appended to in place.
/// That is deliberate and was expensive to learn: on a Docker bind mount, both <c>FileMode.Append</c> and
/// <c>OpenOrCreate</c> + an explicit seek-to-end wrote the new events at the correct offset but left every
/// preceding byte as NUL — a file of exactly the right length with the entire test run silently destroyed.
/// Reading the existing content and writing the whole file back depends on no append or seek semantics at
/// all, so it behaves the same on every filesystem the judge might run on.
/// </para>
/// <para>
/// Serialization reuses <see cref="EventJson.Options"/> so the marker cannot drift from the producer's wire
/// format. It must be that type and never anything reachable through <c>TestLogger</c>: touching that class
/// initializes it, which opens the sink with <c>FileMode.Create</c> and truncates the very file this one is
/// adding to. That is not hypothetical — it happened, and the symptom was a file of exactly the right length
/// containing nothing but NUL bytes.
/// </para>
/// </remarks>
public sealed class EventSink : IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _path;
    private readonly List<string> _pending = [];
    private bool _disposed;

    /// <summary>Prepares to add events to the file at <paramref name="path"/>, creating it if absent.</summary>
    public EventSink(string path) => _path = path;

    /// <summary>Queues one event, stamping it with the current time.</summary>
    /// <param name="event">The event to add.</param>
    /// <remarks>
    /// Takes a protocol record rather than an event name and a payload dictionary. The dictionary form let this
    /// class invent field names the consumer had never heard of, which is precisely the drift the shared
    /// contract removes — now an unparseable event is not constructible.
    /// </remarks>
    public void Write(TestLogEvent @event) =>
        _pending.Add(TestLogEventWriter.ToLine(@event with { Timestamp = Timestamps.Now() }));

    /// <summary>Writes the queued events to disk and clears the queue.</summary>
    public void Flush()
    {
        if (_pending.Count == 0) return;

        var builder = new StringBuilder();

        if (File.Exists(_path))
        {
            var existing = File.ReadAllText(_path, Utf8NoBom);
            builder.Append(existing);

            // A SIGKILLed test host can leave the file ending mid-line; don't glue the first new event onto it.
            if (existing.Length > 0 && !existing.EndsWith('\n'))
                builder.Append('\n');
        }

        foreach (var line in _pending)
            builder.Append(line).Append('\n');

        // Written to a sibling file and moved into place, never over the original.
        //
        // The read-then-overwrite above has a window: between opening the destination for writing and finishing
        // the write, the file is truncated. A SIGKILL from the run's wall clock, or an ENOSPC on a workdir that
        // nothing prunes, lands in that window and destroys the ENTIRE event stream — every test result the
        // submission produced, not just the metrics being appended. The platform then sees an existing but
        // empty events.jsonl, which yields zero results and also suppresses its stdout fallback.
        //
        // A rename within the same directory is atomic on both Linux and macOS, so a reader either sees the old
        // complete file or the new complete file. The temp file is flushed to disk first: without that, a rename
        // can be durable while the data behind it is not, which on a crash gives a file of the right length
        // filled with NULs — the exact symptom this sink already exists to prevent.
        var temp = _path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, Utf8NoBom))
        {
            writer.Write(builder.ToString());
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, _path, overwrite: true);
        _pending.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Flush();
    }

    private static string Timestamp() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
}
