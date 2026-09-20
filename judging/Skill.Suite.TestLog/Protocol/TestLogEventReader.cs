using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// The one reader for the event stream.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not <c>JsonSerializer.Deserialize</c>.</b> This stream is adversarial input: it is written by
/// code running inside a competitor's submission, which can put a string where a number belongs.
/// <c>Deserialize&lt;FinishUnitTestEvent&gt;</c> would throw on <c>"duration_ms":"fast"</c> and take the whole
/// event with it — including the outcome, the only part that actually matters. The hand-written mapping below
/// gives field-level tolerance instead: a bad field defaults, the rest of the event survives.
/// </para>
/// <para>
/// Serialization runs the other way, where the input is our own typed record, and there
/// <see cref="TestLogEventWriter"/> uses the serializer because that is the right tool for trusted input.
/// </para>
/// <para>
/// Probes are named with <c>nameof</c> over the record property being populated, and converted to their wire
/// spelling by the same naming policy the writer uses (<see cref="EventJson.Wire"/>). So reader and writer
/// cannot drift on a <i>rename</i> — the compiler carries it — and there is no parallel list of name constants
/// to keep in step. Always name the property of the record being built here, never an identically-named one on
/// a different record, or a later rename will silently stop following.
///
/// They can still drift on an <i>addition</i> — a new record property this mapper forgets to populate — which
/// is precisely what the round-trip tests exist to catch.
/// </para>
/// </remarks>
public static class TestLogEventReader
{
    /// <summary>
    /// The discriminator property, as configured by <see cref="System.Text.Json.Serialization.JsonPolymorphicAttribute"/>
    /// on <see cref="TestLogEvent"/>. Not derived from a record property: the serializer writes it, no record
    /// declares it.
    /// </summary>
    private const string DiscriminatorProperty = "event";

    /// <summary>Reads one line.</summary>
    /// <param name="line">A single JSON-Lines line. Null, blank and surrounding whitespace are tolerated.</param>
    /// <param name="event">
    /// The parsed event on success — an <see cref="UnknownEvent"/> when the discriminator is one this version does
    /// not know.
    /// </param>
    /// <returns>
    /// <see langword="false"/> only when the line is not a usable event at all: blank, not JSON, not a JSON
    /// object, or carrying no string <c>event</c> field. Never throws.
    /// </returns>
    public static bool TryRead(string? line, [NotNullWhen(true)] out TestLogEvent? @event)
    {
        @event = null;
        if (string.IsNullOrWhiteSpace(line)) return false;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            var name = ReadString(root, DiscriminatorProperty);
            if (string.IsNullOrEmpty(name)) return false;

            @event = Map(name!, root) with { Timestamp = ReadString(root, nameof(TestLogEvent.Timestamp)), RawLine = line };
            return true;
        }
    }

    /// <summary>Reads one line, returning null where <see cref="TryRead"/> would return false.</summary>
    /// <param name="line">A single JSON-Lines line.</param>
    /// <returns>The parsed event, or null.</returns>
    public static TestLogEvent? Read(string? line) => TryRead(line, out var @event) ? @event : null;

    /// <summary>Streams a whole events file, skipping lines that are not usable events.</summary>
    /// <param name="path">Path to the events file.</param>
    /// <returns>The events, in file order.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <remarks>
    /// Skipping a corrupt line rather than failing matches what both consumers already did: one bad line in a
    /// competitor's log must not discard the rest of their run.
    /// </remarks>
    public static IEnumerable<TestLogEvent> ReadFile(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (TryRead(line, out var @event)) yield return @event;
        }
    }

    /// <summary>
    /// Factories keyed by wire discriminator, taken from the polymorphic registry so the names live in exactly
    /// one place — the attributes on <see cref="TestLogEvent"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<JsonElement, TestLogEvent>> Factories =
        new Dictionary<string, Func<JsonElement, TestLogEvent>>(StringComparer.Ordinal)
        {
            [TestLogEventTypes.Of<StartFixtureEvent>()] = root => new StartFixtureEvent(
                ReadString(root, nameof(StartFixtureEvent.Fixture))),

            [TestLogEventTypes.Of<FinishFixtureEvent>()] = root => new FinishFixtureEvent(
                ReadString(root, nameof(FinishFixtureEvent.Fixture)),
                ReadInt(root, nameof(FinishFixtureEvent.TestsRun)),
                ReadInt(root, nameof(FinishFixtureEvent.TestsPassed)),
                ReadInt(root, nameof(FinishFixtureEvent.TestsFailed)),
                ReadLong(root, nameof(FinishFixtureEvent.DurationMs))),

            [TestLogEventTypes.Of<StartUnitTestEvent>()] = root => new StartUnitTestEvent(
                ReadString(root, nameof(StartUnitTestEvent.Fixture)),
                ReadString(root, nameof(StartUnitTestEvent.Test)),
                ReadString(root, nameof(StartUnitTestEvent.Aspect)),
                ReadBool(root, nameof(StartUnitTestEvent.AspectVisible))),

            [TestLogEventTypes.Of<FinishUnitTestEvent>()] = root => new FinishUnitTestEvent(
                ReadString(root, nameof(FinishUnitTestEvent.Fixture)),
                ReadString(root, nameof(FinishUnitTestEvent.Test)),
                Outcomes.Parse(ReadString(root, nameof(FinishUnitTestEvent.Outcome))),
                ReadLong(root, nameof(FinishUnitTestEvent.DurationMs)),
                ReadString(root, nameof(FinishUnitTestEvent.Error)),
                ReadString(root, nameof(FinishUnitTestEvent.Aspect)),
                ReadBool(root, nameof(FinishUnitTestEvent.AspectVisible))),

            [TestLogEventTypes.Of<CallEvent>()] = root => new CallEvent(
                ReadString(root, nameof(CallEvent.Fixture)),
                ReadString(root, nameof(CallEvent.Test)),
                ReadString(root, nameof(CallEvent.Target)),
                ReadArray(root, nameof(CallEvent.Arguments)),
                ReadValue(root, nameof(CallEvent.Returned)),
                ReadString(root, nameof(CallEvent.Threw))),

            [TestLogEventTypes.Of<AssertionEvent>()] = root => new AssertionEvent(
                ReadString(root, nameof(AssertionEvent.Fixture)),
                ReadString(root, nameof(AssertionEvent.Test)),
                ReadString(root, nameof(AssertionEvent.Kind)),
                ReadValue(root, nameof(AssertionEvent.Expected)),
                ReadValue(root, nameof(AssertionEvent.Actual)),
                ReadBool(root, nameof(AssertionEvent.Passed)) ?? false),

            [TestLogEventTypes.Of<TestSummaryEvent>()] = root => new TestSummaryEvent(
                ReadString(root, nameof(TestSummaryEvent.Part)),
                ReadDouble(root, nameof(TestSummaryEvent.Value)),
                ReadNullableInt(root, nameof(TestSummaryEvent.Total)),
                ReadNullableInt(root, nameof(TestSummaryEvent.Passed)),
                ReadNullableInt(root, nameof(TestSummaryEvent.Failed)),
                ReadNullableInt(root, nameof(TestSummaryEvent.Skipped)),
                ReadStringArray(root, nameof(TestSummaryEvent.Warnings))),

            [TestLogEventTypes.Of<CoverageEvent>()] = root => new CoverageEvent(
                ReadString(root, nameof(CoverageEvent.Part)),
                ReadDouble(root, nameof(CoverageEvent.Value)),
                ReadNullableInt(root, nameof(CoverageEvent.Total)),
                ReadNullableInt(root, nameof(CoverageEvent.Covered)),
                ReadString(root, nameof(CoverageEvent.Fixture))),

            [TestLogEventTypes.Of<MutationEvent>()] = root => new MutationEvent(
                ReadString(root, nameof(MutationEvent.Part)),
                ReadDouble(root, nameof(MutationEvent.Value)),
                ReadNullableInt(root, nameof(MutationEvent.Total)),
                ReadNullableInt(root, nameof(MutationEvent.Covered)),
                ReadNullableInt(root, nameof(MutationEvent.Killed)),
                ReadNullableInt(root, nameof(MutationEvent.Survived)),
                ReadNullableInt(root, nameof(MutationEvent.Timeout)),
                ReadNullableInt(root, nameof(MutationEvent.NoCoverage)),
                ReadNullableInt(root, nameof(MutationEvent.Other)),
                ReadString(root, nameof(MutationEvent.Fixture))),

            [TestLogEventTypes.Of<ScoreEvent>()] = root => new ScoreEvent(
                ReadString(root, nameof(ScoreEvent.Part)),
                ReadDouble(root, nameof(ScoreEvent.Value)),
                ReadScoreInputs(root)),

            [TestLogEventTypes.Of<MarkerErrorEvent>()] = root => new MarkerErrorEvent(
                ReadString(root, nameof(MarkerErrorEvent.Detail))),
        };

    private static TestLogEvent Map(string name, JsonElement root) =>
        Factories.TryGetValue(name, out var factory) ? factory(root) : new UnknownEvent(name);

    private static ScoreInputs? ReadScoreInputs(JsonElement root)
    {
        // EventJson.Wire, like every other probe here. Reading a nested object is the one place that does not go
        // through a tolerant helper, so the conversion has to be spelled out — without it this looks for
        // "Inputs" and silently drops the whole object.
        if (!root.TryGetProperty(EventJson.Wire(nameof(ScoreEvent.Inputs)), out var inputs)
            || inputs.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ScoreInputs(
            ReadDouble(inputs, nameof(ScoreInputs.PassRate)),
            ReadDouble(inputs, nameof(ScoreInputs.LineCoverage)),
            ReadDouble(inputs, nameof(ScoreInputs.ScaledCoverage)),
            ReadDouble(inputs, nameof(ScoreInputs.PassingCoverage)),
            ReadDouble(inputs, nameof(ScoreInputs.MutationKillRate)),
            ReadDouble(inputs, nameof(ScoreInputs.MutationScore)),
            ReadDouble(inputs, nameof(ScoreInputs.Core)));
    }

    // ---- Tolerant primitives ----
    // Each takes the C# property name and converts it with EventJson.Wire, so the wire spelling is derived
    // from the same naming policy the writer serializes with rather than restated here.
    // Every one of these returns a default rather than throwing on a missing or wrong-typed field. That is the
    // behaviour the previous consumers had, and losing it would mean one bad field discards a whole event.

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(EventJson.Wire(name), out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int ReadInt(JsonElement root, string name) => ReadNullableInt(root, name) ?? 0;

    private static int? ReadNullableInt(JsonElement root, string name) =>
        root.TryGetProperty(EventJson.Wire(name), out var property) && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt32(out var value)
            ? value
            : null;

    private static long ReadLong(JsonElement root, string name) =>
        root.TryGetProperty(EventJson.Wire(name), out var property) && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt64(out var value)
            ? value
            : 0L;

    private static double? ReadDouble(JsonElement root, string name) =>
        root.TryGetProperty(EventJson.Wire(name), out var property) && property.ValueKind == JsonValueKind.Number
        && property.TryGetDouble(out var value)
            ? value
            : null;

    private static bool? ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(EventJson.Wire(name), out var property)
            ? property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    /// <summary>
    /// Reads an open-typed value. The result is a cloned <see cref="JsonElement"/>, never a live one.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonElement.Clone"/> is mandatory: the <see cref="JsonDocument"/> is disposed the moment
    /// <see cref="TryRead"/> returns, and a non-cloned element would throw <see cref="ObjectDisposedException"/>
    /// the first time a real submission logged a non-string expected value — after every test had passed,
    /// because a test that reads the field inside the same statement never notices.
    /// </remarks>
    private static object? ReadValue(JsonElement root, string name) =>
        root.TryGetProperty(EventJson.Wire(name), out var property) && property.ValueKind != JsonValueKind.Null
            ? property.Clone()
            : null;

    private static IReadOnlyList<object?>? ReadArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(EventJson.Wire(name), out var property) || property.ValueKind != JsonValueKind.Array)
            return null;

        var items = new List<object?>();
        foreach (var element in property.EnumerateArray())
            items.Add(element.ValueKind == JsonValueKind.Null ? null : element.Clone());

        return items;
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(EventJson.Wire(name), out var property) || property.ValueKind != JsonValueKind.Array)
            return null;

        var items = new List<string>();
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String) items.Add(element.GetString()!);
        }

        return items;
    }
}
