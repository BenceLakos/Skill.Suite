using Skill.Suite.TestLog.Protocol;

namespace Skill.Suite.TestLog;

/// <summary>
/// Thread-safe JSON-Lines logger that emits test-lifecycle events.
/// Writes to <c>$LOG_DIRECTORY/events.jsonl</c> when the env var is set,
/// otherwise falls back to stdout. One event per line.
/// </summary>
/// <remarks>
/// <para>
/// Every event is a record from <see cref="Protocol"/>, so field names live at their declaration site and
/// cannot drift from the consumer that reads them. The convenience methods here exist because a fixture name
/// and a test name are what a caller actually has; anything else goes through <see cref="Emit"/>.
/// </para>
/// <para>
/// The sink is opened once per process, on first use. <c>LOG_DIRECTORY</c> cannot be retargeted afterwards, and
/// the file is opened with <see cref="FileMode.Create"/>, so the first write of a process truncates it. Any
/// other writer that shares the file must append and must run after the test host has exited — see the
/// write-ordering contract in <c>judging/README.md</c>.
/// </para>
/// </remarks>
public static class TestLogger
{
    private const string EventFileName = "events.jsonl";
    private const string LogDirectoryVariable = "LOG_DIRECTORY";

    /// <summary>
    /// Fallback line emitted when an event cannot be serialized.
    /// </summary>
    /// <remarks>
    /// A hand-written literal on purpose: this is the path taken when serialization itself failed, so it must
    /// not depend on the serializer. It is the one event with no timestamp.
    /// </remarks>
    /// <remarks>
    /// <c>log-error</c> is not a registered event kind and has no record: it exists only for the case where
    /// serialization itself failed, so it must not depend on the serializer or on anything the serializer
    /// configures. Hence a bare literal.
    /// </remarks>
    private const string LogErrorLine = "{\"event\":\"log-error\"}";

    private static readonly object Sync = new();
    private static readonly TextWriter Sink = OpenSink();

    private static TextWriter OpenSink()
    {
        var dir = Environment.GetEnvironmentVariable(LogDirectoryVariable);
        if (string.IsNullOrWhiteSpace(dir)) return Console.Out;

        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, EventFileName);
            // FileMode.Create truncates so each test-run starts fresh.
            // FileShare.Read lets a marker tail the file while tests run.
            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream) { AutoFlush = true };
            // Close the file cleanly on graceful shutdown.
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { writer.Flush(); writer.Dispose(); } catch { /* best-effort */ }
            };
            return writer;
        }
        catch
        {
            // Anything goes wrong creating the file → don't break tests, fall back.
            return Console.Out;
        }
    }

    /// <summary>Emits any protocol event, stamping it with the current time.</summary>
    /// <param name="event">The event to write.</param>
    /// <remarks>
    /// The general entry point. Metric events — <see cref="TestSummaryEvent"/>, <see cref="CoverageEvent"/>,
    /// <see cref="MutationEvent"/>, <see cref="ScoreEvent"/> — are emitted this way; there is no untyped
    /// payload dictionary any more, so an event kind the consumer cannot parse is no longer constructible.
    /// </remarks>
    public static void Emit(TestLogEvent @event)
    {
        string line;
        try
        {
            line = TestLogEventWriter.ToLine(@event with { Timestamp = Timestamps.Now() });
        }
        catch
        {
            // Serialization should not break tests. Fall back to a minimal record.
            line = LogErrorLine;
        }
        EmitRaw(line);
    }

    /// <summary>Opens a fixture (a test class). Must precede that fixture's unit-test events.</summary>
    /// <param name="fixture">Fixture name, conventionally the test class's simple name.</param>
    public static void StartFixture(string fixture) =>
        Emit(new StartFixtureEvent(fixture));

    /// <summary>Closes a fixture.</summary>
    /// <param name="fixture">Fixture name, matching the one passed to <see cref="StartFixture"/>.</param>
    /// <param name="testsRun">Tests run. Informational: the consumer recomputes it from unit outcomes.</param>
    /// <param name="testsPassed">Tests passed. Informational, as above.</param>
    /// <param name="testsFailed">Tests failed. Informational, as above.</param>
    /// <param name="durationMs">Wall-clock duration of the fixture in milliseconds.</param>
    public static void FinishFixture(string fixture, int testsRun, int testsPassed, int testsFailed, long durationMs) =>
        Emit(new FinishFixtureEvent(fixture, testsRun, testsPassed, testsFailed, durationMs));

    /// <summary>
    /// Opens a unit test. This event is mandatory: the consumer drops <c>call</c>, <c>assertion</c>
    /// and <c>finish-unit-test</c> events for a test it has never seen started.
    /// </summary>
    /// <param name="fixture">Owning fixture name.</param>
    /// <param name="test">Test case name.</param>
    /// <param name="aspect">Marking-scheme aspect id, or <see langword="null"/> for an ungraded test.</param>
    /// <param name="aspectCompetitorVisible">Whether the aspect feeds the competitor-visible score.</param>
    public static void StartUnitTest(
        string fixture, string test, string? aspect = null, bool aspectCompetitorVisible = false) =>
        Emit(new StartUnitTestEvent(fixture, test, Annotated(aspect), Visibility(aspect, aspectCompetitorVisible)));

    /// <summary>Closes a unit test and records its verdict.</summary>
    /// <param name="fixture">Owning fixture name.</param>
    /// <param name="test">Test case name.</param>
    /// <param name="outcome">The verdict.</param>
    /// <param name="durationMs">Wall-clock duration of the test in milliseconds.</param>
    /// <param name="error">Failure detail, or <see langword="null"/> when the test passed.</param>
    /// <param name="aspect">Marking-scheme aspect id, or <see langword="null"/> for an ungraded test.</param>
    /// <param name="aspectCompetitorVisible">Whether the aspect feeds the competitor-visible score.</param>
    /// <remarks>
    /// The outcome is advisory. A preceding <c>call</c> carrying <c>threw</c>, or an <c>assertion</c>
    /// that did not pass, already fixed the verdict and cannot be downgraded by this event.
    /// </remarks>
    public static void FinishUnitTest(
        string fixture, string test, TestLogOutcome outcome, long durationMs, string? error,
        string? aspect = null, bool aspectCompetitorVisible = false) =>
        Emit(new FinishUnitTestEvent(
            fixture, test, outcome, durationMs, error,
            Annotated(aspect), Visibility(aspect, aspectCompetitorVisible)));

    /// <summary>Records one call into the system under test.</summary>
    /// <param name="fixture">Owning fixture name.</param>
    /// <param name="test">Test case name.</param>
    /// <param name="target">Called member, conventionally <c>IInterface.Method</c>.</param>
    /// <param name="arguments">Arguments as logged (streams are snapshotted by the caller).</param>
    /// <param name="returned">Return value, when there was one.</param>
    /// <param name="hasReturned">Whether <paramref name="returned"/> is meaningful (false for <c>void</c>).</param>
    /// <param name="threw">Description of the thrown exception; non-empty marks the test errored.</param>
    public static void Call(
        string fixture, string test, string target,
        IReadOnlyList<object?> arguments,
        object? returned = null,
        bool hasReturned = false,
        string? threw = null) =>
        Emit(new CallEvent(fixture, test, target, arguments, hasReturned ? returned : null, threw));

    /// <summary>Records one assertion.</summary>
    /// <param name="fixture">Owning fixture name.</param>
    /// <param name="test">Test case name.</param>
    /// <param name="kind">Assertion kind, conventionally from <see cref="AssertionKinds"/>.</param>
    /// <param name="expected">Expected value, or a sentinel from <see cref="AssertionSentinels"/>.</param>
    /// <param name="actual">Actual value.</param>
    /// <param name="passed">Whether the assertion held; <see langword="false"/> marks the test failed.</param>
    public static void Assertion(
        string fixture, string test, string kind,
        object? expected, object? actual, bool passed) =>
        Emit(new AssertionEvent(fixture, test, kind, expected, actual, passed));

    /// <summary>
    /// Emits a fatal diagnostic — a build failure, a timeout, a missing folder. Call this before any
    /// non-zero exit so the run explains itself instead of showing only a failed status.
    /// </summary>
    /// <param name="detail">One-line human-readable cause.</param>
    public static void MarkerError(string detail) =>
        Emit(new MarkerErrorEvent(detail));

    /// <summary>Blank aspect ids are treated as no annotation at all, so the fields stay off the wire.</summary>
    private static string? Annotated(string? aspect) =>
        string.IsNullOrWhiteSpace(aspect) ? null : aspect;

    /// <summary>Visibility is only meaningful alongside an aspect; without one it is omitted rather than false.</summary>
    private static bool? Visibility(string? aspect, bool competitorVisible) =>
        string.IsNullOrWhiteSpace(aspect) ? null : competitorVisible;

    private static void EmitRaw(string line)
    {
        lock (Sync)
        {
            Sink.WriteLine(line);
        }
    }
}
