using Skill.Suite.TestLog.Protocol;
using Skill.Suite.TestLog.Tests.Support;
using Xunit;

namespace Skill.Suite.TestLog.Tests;

/// <summary>
/// Producer-level behaviour: what <see cref="TestLogger"/> decides, as opposed to what the wire format is.
/// </summary>
/// <remarks>
/// The wire shape itself is pinned by <c>Protocol/RoundTripTests</c> — write, read back, assert nothing was
/// lost — which is a stronger check than the exact-string assertions this file used to carry, and it does not
/// have to be rewritten every time a field is added. What is left here is the handful of choices the producer
/// makes on top of the records: when a field is omitted entirely, what a timestamp looks like, and that
/// nothing it does can throw.
/// </remarks>
public sealed class TestLoggerProtocolTests : EventFixture
{
    [Fact]
    public void EachEventIsExactlyOneLine()
    {
        TestLogger.StartFixture("DemoTests");
        TestLogger.StartUnitTest("DemoTests", "T");
        TestLogger.FinishFixture("DemoTests", 1, 1, 0, 5);

        Assert.Equal(3, EventCapture.Lines().Length);
    }

    [Fact]
    public void UnannotatedTest_OmitsBothAspectFields()
    {
        // A producer decision, not a serialization one: an ungraded test must carry no aspect key at all rather
        // than a null one, so a consumer cannot mistake "not graded" for "graded as null".
        TestLogger.StartUnitTest("F", "T");
        TestLogger.FinishUnitTest("F", "T", TestLogOutcome.Passed, 1, null);

        foreach (var line in NormalizedLines())
        {
            Assert.DoesNotContain("aspect", line, StringComparison.Ordinal);
            Assert.DoesNotContain("aspectVisible", line, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankAspect_CountsAsUnannotated(string aspect)
    {
        TestLogger.StartUnitTest("F", "T", aspect, aspectCompetitorVisible: true);

        Assert.DoesNotContain("aspect", NormalizedLine(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnnotatedTest_CarriesTheAspectAndItsVisibility()
    {
        TestLogger.StartUnitTest("F", "T", "C2.1", aspectCompetitorVisible: true);

        var evt = Assert.IsType<StartUnitTestEvent>(TestLogEventReader.Read(EventCapture.SingleLine()));
        Assert.Equal("C2.1", evt.Aspect);
        Assert.True(evt.AspectVisible);
    }

    [Fact]
    public void GradedButHiddenAspect_StillCarriesVisibilityExplicitly()
    {
        // False must be on the wire rather than omitted: "graded, not shown" and "not graded" are different.
        TestLogger.StartUnitTest("F", "T", "C2.1");

        var evt = Assert.IsType<StartUnitTestEvent>(TestLogEventReader.Read(EventCapture.SingleLine()));
        Assert.Equal("C2.1", evt.Aspect);
        Assert.False(evt.AspectVisible);
    }

    [Fact]
    public void VoidCall_AndACallReturningNull_AreIndistinguishable()
    {
        // A known limitation of the protocol, carried over deliberately. Anything that needs to tell them apart
        // has to look at the declared signature, not the log.
        TestLogger.Call("F", "T", "IJob.Run", []);
        var voidCall = NormalizedLine();

        EventCapture.Reset();
        TestLogger.Call("F", "T", "IJob.Run", [], returned: null, hasReturned: true);

        Assert.Equal(voidCall, NormalizedLine());
    }

    [Fact]
    public void Timestamp_UsesTheProtocolFormat()
    {
        TestLogger.StartFixture("F");

        var evt = TestLogEventReader.Read(EventCapture.SingleLine())!;
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", evt.Timestamp!);
        Assert.True(Timestamps.TryParse(evt.Timestamp, out _));
    }

    [Fact]
    public void EveryEventIsStamped()
    {
        ScriptedRun.Emit();

        foreach (var line in EventCapture.Lines())
        {
            Assert.NotNull(TestLogEventReader.Read(line)!.Timestamp);
        }
    }

    [Fact]
    public void Emit_EscapesWithTheRelaxedEncoderSoDiagnosticsStayReadable()
    {
        TestLogger.MarkerError("""expected "x" & got <y>""");

        var line = EventCapture.SingleLine();

        // UnsafeRelaxedJsonEscaping: quotes are escaped because they must be, but &, < and > are left alone so a
        // compiler error in a diagnostic is still readable by a human.
        Assert.Contains("""\"x\" & got <y>""", line, StringComparison.Ordinal);
        Assert.Equal("""expected "x" & got <y>""", Assert.IsType<MarkerErrorEvent>(TestLogEventReader.Read(line)).Detail);
    }

    [Fact]
    public void MetricEvents_CanOnlyBeAKindTheConsumerUnderstands()
    {
        // There used to be a runtime allow-list of metric event names here, because the name was a string and a
        // typo would silently vanish (the consumer ignores unknown kinds). With typed records the allow-list is
        // the type system, so this asserts the set rather than the guard.
        MetricEvent[] metrics =
        [
            new TestSummaryEvent(null, 1, 1, 1, 0, 0),
            new CoverageEvent(null, 1, 1, 1),
            new MutationEvent(null, 1, 1, 1, 1, 0, 0, 0, 0),
            new ScoreEvent(null, 1),
        ];

        foreach (var metric in metrics) TestLogger.Emit(metric);

        var kinds = EventCapture.Lines().Select(line => TestLogEventReader.Read(line)!.Event).ToArray();
        Assert.Equal(
            [
                TestLogEventTypes.Of<TestSummaryEvent>(),
                TestLogEventTypes.Of<CoverageEvent>(),
                TestLogEventTypes.Of<MutationEvent>(),
                TestLogEventTypes.Of<ScoreEvent>(),
            ],
            kinds);
    }

    [Fact]
    public void ScoreValue_IsANumberNotAString()
    {
        // Only a JSON number is lifted onto the fixture; a quoted value is ignored and the competitor sees no
        // score at all.
        TestLogger.Emit(new ScoreEvent(MetricEvent.OverallPart, 0.83));

        Assert.Contains("\"value\":0.83", EventCapture.SingleLine(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryEmittedLineIsReadableByTheSharedReader()
    {
        // The end-to-end producer/consumer contract in one assertion: everything the producer can emit, the
        // reader recognises. An UnknownEvent here would mean the two have drifted.
        ScriptedRun.Emit();

        foreach (var line in EventCapture.Lines())
        {
            Assert.True(TestLogEventReader.TryRead(line, out var @event), $"unreadable line: {line}");
            Assert.IsNotType<UnknownEvent>(@event);
        }
    }

    [Fact]
    public void TheWireFormatIsNotIndented()
    {
        // One event per line is the whole format; indentation would break every consumer.
        Assert.False(EventJson.Options.WriteIndented);
    }
}
