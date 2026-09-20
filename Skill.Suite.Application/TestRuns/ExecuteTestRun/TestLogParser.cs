using Skill.Suite.Domain.TestRuns;
using Skill.Suite.TestLog.Protocol;

namespace Skill.Suite.Application.TestRuns.ExecuteTestRun;

/// <summary>
/// Applies one event emitted by a judgement container to a <see cref="TestRun"/>.
/// </summary>
/// <remarks>
/// <para>
/// Reading and field access come from <see cref="TestLogEventReader"/>, which is compiled in from the judging
/// subtree as shared source. That is what makes this a contract rather than a convention: the producer and this
/// consumer spell every event and field name through the same constants, so a rename cannot land on one side.
/// This class is now only the mapping from protocol records onto the domain.
/// </para>
/// <para>
/// Tolerance is unchanged and deliberate. Unknown event kinds are ignored, malformed lines are dropped, and a
/// wrong-typed field defaults rather than discarding the event — the stream is written by code running inside a
/// competitor's submission and must not be able to fault the worker.
/// </para>
/// </remarks>
public static class TestLogParser
{
    /// <summary>Applies a single event line to the run. Never throws on malformed input.</summary>
    /// <param name="run">The run to apply the event to.</param>
    /// <param name="line">One JSON-Lines event.</param>
    public static void Apply(TestRun run, string line)
    {
        if (!TestLogEventReader.TryRead(line, out var @event))
            return;

        var timestamp = @event.TimestampOr(DateTime.UtcNow);

        switch (@event)
        {
            case StartFixtureEvent e:
                run.StartFixture(Name(e.Fixture), timestamp);
                break;

            case FinishFixtureEvent e:
                run.FinishFixture(
                    Name(e.Fixture), e.TestsRun, e.TestsPassed, e.TestsFailed, e.DurationMs, timestamp);
                break;

            case StartUnitTestEvent e:
                run.StartUnitTest(
                    Name(e.Fixture), Name(e.Test), timestamp, e.Aspect, e.AspectVisible ?? false);
                break;

            case FinishUnitTestEvent e:
                run.FinishUnitTest(
                    Name(e.Fixture), Name(e.Test), MapOutcome(e.Outcome), e.DurationMs, e.Error, timestamp,
                    e.Aspect, e.AspectVisible ?? false);
                break;

            case CallEvent e:
                run.AppendUnitTestEvent(Name(e.Fixture), Name(e.Test), new TestEventRecord(
                    Kind: TestEventKind.Call,
                    Timestamp: timestamp,
                    Target: e.Target,
                    Arguments: e.Arguments?.Select(AsText).ToList(),
                    Returned: AsText(e.Returned),
                    Threw: e.Threw,
                    AssertionKind: null,
                    Expected: null,
                    Actual: null,
                    Passed: null));
                break;

            case AssertionEvent e:
                run.AppendUnitTestEvent(Name(e.Fixture), Name(e.Test), new TestEventRecord(
                    Kind: TestEventKind.Assertion,
                    Timestamp: timestamp,
                    Target: null,
                    Arguments: null,
                    Returned: null,
                    Threw: null,
                    AssertionKind: e.Kind,
                    Expected: AsText(e.Expected),
                    Actual: AsText(e.Actual),
                    Passed: e.Passed));
                break;

            // Fixture-scoped measurements, matched BEFORE the general metric arm. They are the same two event
            // kinds, distinguished only by carrying a `fixture` instead of a `part` — so ordering is what keeps
            // a test class out of the metric-part name space, where it would collide with a scoring part.
            case CoverageEvent { Fixture: { Length: > 0 } fixture } e:
                run.RecordFixtureMetric(
                    fixture, TestFixtureMetric.LineCoverage, e.Event, timestamp, e.RawLine, e.Value);
                break;

            case MutationEvent { Fixture: { Length: > 0 } fixture } e:
                run.RecordFixtureMetric(
                    fixture, TestFixtureMetric.MutationScore, e.Event, timestamp, e.RawLine, e.Value);
                break;

            // One arm for all four metric kinds. Previously this was a four-way fallthrough plus a string
            // comparison to find the one that carried a quality; the shared headline value makes it a type test.
            case MetricEvent e:
                run.RecordMetric(e.PartOrOverall, e.Event, timestamp, e.RawLine);

                // Only the score's value is functionally consumed: it becomes the fixture's quality and drives
                // the competitor-visible bucket. The other three are stored for display.
                if (e is ScoreEvent { Value: { } quality })
                    run.SetFixtureQuality(e.PartOrOverall, quality);
                break;

            // A fatal judge diagnostic. Without this the emit_fatal contract is a no-op end to end: the run
            // shows a failed status and an exit code with nothing saying why, which is exactly the case an
            // expert has to explain to a competitor.
            case MarkerErrorEvent e:
                run.RecordDiagnostic(e.Detail ?? e.RawLine ?? e.Event, timestamp);
                break;

            // UnknownEvent falls through here on purpose: the protocol has always promised that a consumer
            // ignores kinds it does not know, so a newer judge image cannot break an older platform.
            default:
                break;
        }
    }

    private const string UnknownName = "(unknown)";

    private static string Name(string? value) => string.IsNullOrEmpty(value) ? UnknownName : value;

    /// <summary>
    /// Renders an open-typed wire value as the text the domain stores.
    /// </summary>
    /// <remarks>
    /// A JSON string becomes its unquoted content so an error message reads naturally in the UI; anything else
    /// keeps its JSON form, which is what makes a number, a boolean and a nested stream snapshot all legible.
    /// </remarks>
    private static string? AsText(object? value) => value switch
    {
        null => null,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Null } => null,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element => element.GetString(),
        System.Text.Json.JsonElement element => element.GetRawText(),
        string text => text,
        _ => value.ToString(),
    };

    private static TestOutcome MapOutcome(TestLogOutcome outcome) => outcome switch
    {
        TestLogOutcome.Passed => TestOutcome.Passed,
        TestLogOutcome.Failed => TestOutcome.Failed,
        TestLogOutcome.Skipped => TestOutcome.Skipped,
        TestLogOutcome.Errored => TestOutcome.Errored,
        _ => TestOutcome.Unknown,
    };
}
