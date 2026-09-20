using Skill.Suite.TestLog.Protocol;
using Xunit;

namespace Skill.Suite.TestLog.Tests.Protocol;

/// <summary>
/// Writes every event kind, reads it back, and asserts nothing was lost.
/// </summary>
/// <remarks>
/// This is the primary pin of the contract, and it catches the one failure a golden file cannot: a record
/// property the reader's mapper forgets to populate. Writer and reader cannot drift on a field <i>rename</i>
/// — the writer's names come from the naming policy and the reader derives its probes from the same policy
/// over <c>nameof</c> — but they can drift on an <i>addition</i>, and that is silent: the field simply never
/// comes back.
/// <para>
/// The assertion is write → read → write rather than record equality. Open-typed fields come back as
/// <c>JsonElement</c>, which has no structural equality, so comparing lines is both stricter and honest about
/// what round-tripping actually means: no information lost.
/// </para>
/// </remarks>
public sealed class RoundTripTests
{
    private const string Stamp = "2026-07-31T10:11:12.345Z";

    [Fact]
    public void StartFixture()
    {
        var read = AssertRoundTrip(new StartFixtureEvent("DemoTests"));

        var evt = Assert.IsType<StartFixtureEvent>(read);
        Assert.Equal("DemoTests", evt.Fixture);
        Assert.Equal(Stamp, evt.Timestamp);
    }

    [Fact]
    public void FinishFixture()
    {
        var read = AssertRoundTrip(new FinishFixtureEvent("DemoTests", 4, 3, 1, 23));

        var evt = Assert.IsType<FinishFixtureEvent>(read);
        Assert.Equal(4, evt.TestsRun);
        Assert.Equal(3, evt.TestsPassed);
        Assert.Equal(1, evt.TestsFailed);
        Assert.Equal(23, evt.DurationMs);
    }

    [Fact]
    public void StartUnitTest_Graded()
    {
        var read = AssertRoundTrip(new StartUnitTestEvent("F", "T", "C2.1", true));

        var evt = Assert.IsType<StartUnitTestEvent>(read);
        Assert.Equal("C2.1", evt.Aspect);
        Assert.True(evt.AspectVisible);
    }

    [Fact]
    public void StartUnitTest_Ungraded_CarriesNoAspect()
    {
        var line = Write(new StartUnitTestEvent("F", "T"));

        Assert.DoesNotContain("aspect", line, StringComparison.Ordinal);

        var evt = Assert.IsType<StartUnitTestEvent>(TestLogEventReader.Read(line));
        Assert.Null(evt.Aspect);
        Assert.Null(evt.AspectVisible);
    }

    [Theory]
    [InlineData(TestLogOutcome.Passed)]
    [InlineData(TestLogOutcome.Failed)]
    [InlineData(TestLogOutcome.Skipped)]
    [InlineData(TestLogOutcome.Errored)]
    public void FinishUnitTest_EveryOutcome(TestLogOutcome outcome)
    {
        var read = AssertRoundTrip(new FinishUnitTestEvent("F", "T", outcome, 5, null, "A1.1", false));

        var evt = Assert.IsType<FinishUnitTestEvent>(read);
        Assert.Equal(outcome, evt.Outcome);
        Assert.Equal("A1.1", evt.Aspect);
        Assert.False(evt.AspectVisible);
    }

    [Fact]
    public void FinishUnitTest_OutcomeIsLowercaseOnTheWire()
    {
        // The enum converter is configured with the camelCase policy. Without it this would be "Passed" and
        // every consumer's comparison would silently fail.
        var line = Write(new FinishUnitTestEvent("F", "T", TestLogOutcome.Passed, 1, null));

        Assert.Contains("\"outcome\":\"passed\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Call_WithScalarArgumentsAndReturn()
    {
        var read = AssertRoundTrip(new CallEvent("F", "T", "ICalc.Add", [2, 3], 5));

        var evt = Assert.IsType<CallEvent>(read);
        Assert.Equal("ICalc.Add", evt.Target);
        Assert.Equal(2, evt.Arguments!.Count);
        Assert.Null(evt.Threw);
    }

    [Fact]
    public void Call_WithMixedArgumentTypesIncludingNullAndNestedObject()
    {
        // Exactly what CallLoggingProxy produces for a Stream argument, alongside a null and a scalar.
        var snapshot = new Dictionary<string, object?>
        {
            ["$stream"] = true,
            ["length"] = 12,
            ["content"] = "hello world!",
        };

        var read = AssertRoundTrip(
            new CallEvent("F", "T", "IReader.Read", [null, 7, "text", snapshot], null, "IOException"));

        var evt = Assert.IsType<CallEvent>(read);
        Assert.Equal(4, evt.Arguments!.Count);
        Assert.Null(evt.Arguments[0]);
        Assert.Equal("IOException", evt.Threw);
    }

    [Fact]
    public void Call_ThatThrew()
    {
        var read = AssertRoundTrip(new CallEvent("F", "T", "ICalc.Div", [1, 0], null, "DivideByZeroException"));

        Assert.Equal("DivideByZeroException", Assert.IsType<CallEvent>(read).Threw);
    }

    [Theory]
    [InlineData(42)]
    [InlineData("text")]
    [InlineData(true)]
    [InlineData(0.5)]
    public void Assertion_ExpectedAndActualKeepTheirJsonType(object value)
    {
        // These must stay open-typed: the goldens carry numbers, bools, strings and sentinels here, and typing
        // them as string would quote every one.
        var read = AssertRoundTrip(new AssertionEvent("F", "T", AssertionKinds.Equal, value, value, true));

        Assert.True(Assert.IsType<AssertionEvent>(read).Passed);
    }

    [Fact]
    public void Assertion_Failed()
    {
        var read = AssertRoundTrip(
            new AssertionEvent("F", "T", AssertionKinds.NotNull, AssertionSentinels.NonNull, null, false));

        var evt = Assert.IsType<AssertionEvent>(read);
        Assert.False(evt.Passed);
        Assert.Equal(AssertionKinds.NotNull, evt.Kind);
    }

    [Fact]
    public void TestSummary()
    {
        var read = AssertRoundTrip(new TestSummaryEvent("overall", 0.5, 4, 2, 2, 0));

        var evt = Assert.IsType<TestSummaryEvent>(read);
        Assert.Equal(0.5, evt.Value);
        Assert.Equal(4, evt.Total);
        Assert.Null(evt.Warnings);
    }

    [Fact]
    public void TestSummary_WithWarnings()
    {
        var read = AssertRoundTrip(
            new TestSummaryEvent(null, 0, 0, 0, 0, 0, ["no-trx: nothing to read"]));

        var evt = Assert.IsType<TestSummaryEvent>(read);
        Assert.Equal("no-trx: nothing to read", Assert.Single(evt.Warnings!));

        // No part on the wire means the reserved rollup.
        Assert.Null(evt.Part);
        Assert.Equal(MetricEvent.OverallPart, evt.PartOrOverall);
    }

    [Fact]
    public void Coverage()
    {
        var read = AssertRoundTrip(new CoverageEvent("services", 0.9308, 289, 269));

        var evt = Assert.IsType<CoverageEvent>(read);
        Assert.Equal(269, evt.Covered);
        Assert.Equal(289, evt.Total);
        Assert.Equal("services", evt.PartOrOverall);
    }

    [Fact]
    public void Coverage_ForAFixture_CarriesTheFixtureAndNoPart()
    {
        var read = AssertRoundTrip(new CoverageEvent(null, 0.4, 100, 40, "WidgetTests"));

        var evt = Assert.IsType<CoverageEvent>(read);
        Assert.Equal("WidgetTests", evt.Fixture);
        Assert.Null(evt.Part);
    }

    [Fact]
    public void Coverage_ForAPart_CarriesNoFixtureField()
    {
        // Absent rather than null on the wire, so a part-scoped event is byte-identical to what every judge
        // image emitted before fixture scoping existed.
        var line = Write(new CoverageEvent("services", 0.9308, 289, 269));

        Assert.DoesNotContain("fixture", line, StringComparison.Ordinal);
        Assert.Null(Assert.IsType<CoverageEvent>(TestLogEventReader.Read(line)).Fixture);
    }

    [Fact]
    public void Mutation()
    {
        var read = AssertRoundTrip(new MutationEvent("services", 0.7143, 100, 85, 55, 25, 5, 15, 0));

        var evt = Assert.IsType<MutationEvent>(read);
        Assert.Equal(55, evt.Killed);
        Assert.Equal(25, evt.Survived);
        Assert.Equal(85, evt.Covered);
        Assert.Equal(100, evt.Total);
    }

    [Fact]
    public void Mutation_ForAFixture_CarriesTheFixtureAndNoPart()
    {
        var read = AssertRoundTrip(new MutationEvent(null, 0.75, 8, 8, 5, 2, 1, 0, 0, "WidgetTests"));

        var evt = Assert.IsType<MutationEvent>(read);
        Assert.Equal("WidgetTests", evt.Fixture);
        Assert.Null(evt.Part);
        Assert.Equal(evt.Covered, evt.Total);
    }

    [Fact]
    public void AFixtureScopedEventIsStillReadableWhenTheFieldIsNotKnown()
    {
        // The backward-compatibility promise, exercised the only way it can be from inside this version: an
        // unrecognised property is ignored rather than discarding the event. That is what lets a judge image
        // emitting fixture-scoped metrics run against a platform that has never heard of them.
        const string line = """
            {"event":"coverage","part":null,"value":0.4,"total":100,"covered":40,"invented-later":"x"}
            """;

        var evt = Assert.IsType<CoverageEvent>(TestLogEventReader.Read(line));
        Assert.Equal(0.4, evt.Value);
        Assert.Equal(40, evt.Covered);
    }

    [Fact]
    public void Score_WithInputs()
    {
        var read = AssertRoundTrip(new ScoreEvent(
            "services", 0.795,
            new ScoreInputs(0.9524, 0.9308, 0.8612, 0.8202, 0.7143, 0.681, 0.7473)));

        var evt = Assert.IsType<ScoreEvent>(read);
        Assert.Equal(0.795, evt.Value);
        Assert.Equal(0.681, evt.Inputs!.MutationScore);
    }

    [Fact]
    public void Score_WithoutInputs()
    {
        var read = AssertRoundTrip(new ScoreEvent("overall", 0.5));

        Assert.Null(Assert.IsType<ScoreEvent>(read).Inputs);
    }

    [Fact]
    public void MarkerError()
    {
        var read = AssertRoundTrip(new MarkerErrorEvent("restore failed: NU1101"));

        Assert.Equal("restore failed: NU1101", Assert.IsType<MarkerErrorEvent>(read).Detail);
    }

    [Fact]
    public void EveryMetricEventSharesTheHeadlineValueName()
    {
        // The consolidation, asserted directly: four kinds, one name for the number each exists to report.
        MetricEvent[] metrics =
        [
            new TestSummaryEvent("p", 0.25, 4, 1, 3, 0),
            new CoverageEvent("p", 0.5, 10, 5),
            new MutationEvent("p", 0.75, 8, 4, 3, 1, 0, 4, 0),
            new ScoreEvent("p", 0.9),
        ];

        foreach (var metric in metrics)
        {
            var line = Write(metric);
            Assert.Contains("\"value\":", line, StringComparison.Ordinal);

            var read = Assert.IsAssignableFrom<MetricEvent>(TestLogEventReader.Read(line));
            Assert.Equal(metric.Value, read.Value);
        }
    }

    [Fact]
    public void TheDiscriminatorIsAlwaysTheFirstProperty()
    {
        // Guaranteed by polymorphic serialization rather than by an ordering attribute: System.Text.Json writes
        // the type discriminator before anything else so a streaming reader can dispatch without buffering.
        // Nothing here reads by position - the reader is name-based - but a discriminator that moved would be a
        // sign the polymorphic configuration had been lost.
        foreach (TestLogEvent @event in new TestLogEvent[]
                 {
                     new StartFixtureEvent("DemoTests"),
                     new CallEvent("F", "T", "I.M", []),
                     new CoverageEvent("services", 0.5, 10, 5),
                     new MarkerErrorEvent("boom"),
                 })
        {
            Assert.StartsWith($"{{\"event\":\"{@event.Event}\"", Write(@event), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryRegisteredKindRoundTripsToItsOwnRecord()
    {
        // The registry is the only declaration of an event kind, so this asserts it is complete and consistent:
        // every registered discriminator must come back as exactly the type it was registered for.
        foreach (var (type, discriminator) in TestLogEventTypes.All)
        {
            var line = $"{{\"event\":\"{discriminator}\"}}";

            Assert.True(TestLogEventReader.TryRead(line, out var read), $"unreadable: {line}");
            Assert.IsType(type, read);
            Assert.Equal(discriminator, read.Event);
        }
    }

    private static string Write(TestLogEvent @event) =>
        TestLogEventWriter.ToLine(@event with { Timestamp = Stamp });

    private static TestLogEvent AssertRoundTrip(TestLogEvent original)
    {
        var line = Write(original);

        Assert.True(TestLogEventReader.TryRead(line, out var read), $"could not read back: {line}");
        Assert.Equal(original.GetType(), read.GetType());
        Assert.Equal(original.Event, read.Event);

        // Write → read → write must be stable. Any field the mapper drops shows up here as a shorter line.
        Assert.Equal(line, TestLogEventWriter.ToLine(read));

        return read;
    }
}
