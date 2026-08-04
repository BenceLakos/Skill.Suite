using Skill.Suite.TestLog.Protocol;
using Xunit;

namespace Skill.Suite.TestLog.Tests.Protocol;

/// <summary>
/// Pins the reader's tolerance of hostile input.
/// </summary>
/// <remarks>
/// This file exists to make one specific future decision conscious rather than accidental: replacing the
/// hand-written mapping with <c>JsonSerializer.Deserialize&lt;T&gt;</c>. That looks like a simplification and is
/// a regression — the stream is written by code inside a competitor's submission, and a single wrong-typed field
/// would throw and discard the whole event, including the verdict, which is the only part that matters.
/// </remarks>
public sealed class ReaderToleranceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("""{"event":123}""")]
    [InlineData("""{"no-event":true}""")]
    public void UnusableLines_AreRejectedWithoutThrowing(string? line)
    {
        Assert.False(TestLogEventReader.TryRead(line, out var @event));
        Assert.Null(@event);
        Assert.Null(TestLogEventReader.Read(line));
    }

    [Fact]
    public void UnknownKind_IsReadRatherThanRejected()
    {
        // Distinguishing "unknown kind" from "malformed" is the point: the first means a newer producer is
        // talking to an older consumer, the second means something wrote broken JSON.
        Assert.True(TestLogEventReader.TryRead("""{"event":"invented-later","extra":1}""", out var @event));

        var unknown = Assert.IsType<UnknownEvent>(@event);
        Assert.Equal("invented-later", unknown.Event);
    }

    [Fact]
    public void WrongTypedField_DefaultsWithoutLosingTheRestOfTheEvent()
    {
        // The whole reason the reader is hand-written. The outcome must survive a garbage duration.
        var line = """{"event":"finish-unit-test","fixture":"F","test":"T","outcome":"failed","duration_ms":"fast","error":"boom"}""";

        var evt = Assert.IsType<FinishUnitTestEvent>(TestLogEventReader.Read(line));

        Assert.Equal(TestLogOutcome.Failed, evt.Outcome);
        Assert.Equal("boom", evt.Error);
        Assert.Equal(0, evt.DurationMs);
    }

    [Fact]
    public void MissingFields_Default()
    {
        var evt = Assert.IsType<FinishFixtureEvent>(TestLogEventReader.Read("""{"event":"finish-fixture"}"""));

        Assert.Null(evt.Fixture);
        Assert.Equal(0, evt.TestsRun);
        Assert.Equal(0, evt.DurationMs);
    }

    [Fact]
    public void ExplicitJsonNull_ReadsAsNull()
    {
        // Older streams wrote nulls explicitly; newer ones omit them. Both must read identically.
        var withNull = """{"event":"call","fixture":"F","test":"T","target":"I.M","arguments":[],"returned":null,"threw":null}""";
        var without = """{"event":"call","fixture":"F","test":"T","target":"I.M","arguments":[]}""";

        var a = Assert.IsType<CallEvent>(TestLogEventReader.Read(withNull));
        var b = Assert.IsType<CallEvent>(TestLogEventReader.Read(without));

        Assert.Null(a.Returned);
        Assert.Null(a.Threw);
        Assert.Null(b.Returned);
        Assert.Null(b.Threw);
    }

    [Theory]
    [InlineData("passed", TestLogOutcome.Passed)]
    [InlineData("PASSED", TestLogOutcome.Passed)]
    [InlineData("pass", TestLogOutcome.Passed)]
    [InlineData("failed", TestLogOutcome.Failed)]
    [InlineData("fail", TestLogOutcome.Failed)]
    [InlineData("skipped", TestLogOutcome.Skipped)]
    [InlineData("skip", TestLogOutcome.Skipped)]
    [InlineData("errored", TestLogOutcome.Errored)]
    [InlineData("error", TestLogOutcome.Errored)]
    [InlineData("nonsense", TestLogOutcome.Unknown)]
    [InlineData(null, TestLogOutcome.Unknown)]
    public void OutcomeParsing_ToleratesCaseAndShortForms(string? wire, TestLogOutcome expected)
    {
        Assert.Equal(expected, Outcomes.Parse(wire));
    }

    [Fact]
    public void UnparseableTimestamp_FallsBackWithoutThrowing()
    {
        var evt = TestLogEventReader.Read("""{"event":"start-fixture","fixture":"F","timestamp":"yesterday"}""")!;
        var fallback = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("yesterday", evt.Timestamp);
        Assert.Equal(fallback, evt.TimestampOr(fallback));
    }

    [Fact]
    public void MissingTimestamp_FallsBack()
    {
        var evt = TestLogEventReader.Read("""{"event":"start-fixture","fixture":"F"}""")!;
        var fallback = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Null(evt.Timestamp);
        Assert.Equal(fallback, evt.TimestampOr(fallback));
    }

    [Fact]
    public void OpenTypedValues_SurviveTheDocumentBeingDisposed()
    {
        // JsonElement is a view over a JsonDocument that TryRead disposes before returning, so the reader must
        // Clone(). Without it this throws ObjectDisposedException the first time a real submission logs a
        // non-string expected value - and only then, because a test that reads the field inside the same
        // statement as the parse never notices.
        var line = """{"event":"assertion","fixture":"F","test":"T","kind":"equal","expected":{"a":[1,2]},"actual":4,"passed":false}""";

        var evt = Assert.IsType<AssertionEvent>(TestLogEventReader.Read(line));

        Assert.NotNull(evt.Expected);
        Assert.Contains("\"a\"", evt.Expected!.ToString(), StringComparison.Ordinal);
        Assert.Equal("4", evt.Actual!.ToString());
        Assert.Contains("\"a\"", TestLogEventWriter.ToLine(evt), StringComparison.Ordinal);
    }

    [Fact]
    public void ArgumentElements_SurviveTheDocumentBeingDisposed()
    {
        var line = """{"event":"call","fixture":"F","test":"T","target":"I.M","arguments":[{"$stream":true,"length":3}]}""";

        var evt = Assert.IsType<CallEvent>(TestLogEventReader.Read(line));

        Assert.Contains("$stream", Assert.Single(evt.Arguments!)!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MetricPart_DefaultsToOverallWhenAbsentOrBlank()
    {
        var absent = Assert.IsAssignableFrom<MetricEvent>(
            TestLogEventReader.Read("""{"event":"score","value":0.5}"""));
        var blank = Assert.IsAssignableFrom<MetricEvent>(
            TestLogEventReader.Read("""{"event":"score","part":"   ","value":0.5}"""));

        Assert.Equal(MetricEvent.OverallPart, absent.PartOrOverall);
        Assert.Equal(MetricEvent.OverallPart, blank.PartOrOverall);
    }

    [Fact]
    public void MetricValue_AsAStringIsIgnoredRatherThanCoerced()
    {
        // The platform only lifts a numeric value. Coercing "0.5" here would make a broken producer look fine
        // in tests and fail in production, so the reader reports it as absent.
        var evt = Assert.IsType<ScoreEvent>(TestLogEventReader.Read("""{"event":"score","value":"0.5"}"""));

        Assert.Null(evt.Value);
    }

    [Fact]
    public void ScoreInputs_MissingOrWrongTypedIsNull()
    {
        Assert.Null(Assert.IsType<ScoreEvent>(
            TestLogEventReader.Read("""{"event":"score","value":0.5}""")).Inputs);

        Assert.Null(Assert.IsType<ScoreEvent>(
            TestLogEventReader.Read("""{"event":"score","value":0.5,"inputs":"nope"}""")).Inputs);
    }

    [Fact]
    public void RawLine_IsCarriedButNeverSerialized()
    {
        const string line = """{"event":"marker-error","detail":"boom"}""";

        var evt = TestLogEventReader.Read(line)!;

        Assert.Equal(line, evt.RawLine);
        Assert.DoesNotContain("RawLine", TestLogEventWriter.ToLine(evt), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WritingThroughTheBaseTypeStillEmitsThePayload()
    {
        // System.Text.Json binds to the declared type, so a writer that took TestLogEvent and called
        // Serialize(evt, options) would emit the envelope only - valid JSON, consumer still creates the
        // fixture, payload silently gone. This is the highest-blast-radius mistake available here.
        TestLogEvent asBase = new StartFixtureEvent("DemoTests");

        var line = TestLogEventWriter.ToLine(asBase);

        Assert.Contains("\"fixture\":\"DemoTests\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFile_SkipsUnusableLinesAndKeepsOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"protocol-{Guid.NewGuid():N}.jsonl");
        File.WriteAllLines(path,
        [
            """{"event":"start-fixture","fixture":"F"}""",
            "",
            "garbage",
            """{"event":"start-unit-test","fixture":"F","test":"T"}""",
        ]);

        try
        {
            var events = TestLogEventReader.ReadFile(path).ToList();

            Assert.Collection(events,
                first => Assert.IsType<StartFixtureEvent>(first),
                second => Assert.IsType<StartUnitTestEvent>(second));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFile_OnAMissingFileThrows()
    {
        // Distinct from a corrupt line: a missing file is a caller error, not hostile input.
        Assert.Throws<FileNotFoundException>(
            () => TestLogEventReader.ReadFile(Path.Combine(Path.GetTempPath(), "absent.jsonl")).ToList());
    }
}
