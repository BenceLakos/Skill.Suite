using System.Text.Json.Serialization;

namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// One line of the event stream. Every event kind derives from this, and consumers pattern-match on the
/// derived records.
/// </summary>
/// <remarks>
/// <para>
/// The event kind is declared once, here, as a polymorphic discriminator. A derived record therefore says
/// nothing about its own name — the attribute list below is the whole registry of event kinds, and
/// System.Text.Json writes the discriminator itself, always as the first property.
/// </para>
/// <para>
/// The discriminator property is <c>event</c> rather than the more usual <c>type</c> because it is also
/// hand-written by <c>judge-lib.sh</c>, which composes JSON in bash — the judge image ships neither <c>jq</c>
/// nor <c>python3</c> — and read by <c>verify.sh</c> and <c>judge-run.sh</c>. Renaming it would buy nothing and
/// break all three.
/// </para>
/// <para>
/// <see cref="UnknownEvent"/> is deliberately absent from the list: its discriminator is whatever an unknown
/// producer wrote, so it cannot be registered. It is produced by the reader and never serialized.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "event")]
[JsonDerivedType(typeof(StartFixtureEvent), "start-fixture")]
[JsonDerivedType(typeof(FinishFixtureEvent), "finish-fixture")]
[JsonDerivedType(typeof(StartUnitTestEvent), "start-unit-test")]
[JsonDerivedType(typeof(FinishUnitTestEvent), "finish-unit-test")]
[JsonDerivedType(typeof(CallEvent), "call")]
[JsonDerivedType(typeof(AssertionEvent), "assertion")]
[JsonDerivedType(typeof(TestSummaryEvent), "test-summary")]
[JsonDerivedType(typeof(CoverageEvent), "coverage")]
[JsonDerivedType(typeof(MutationEvent), "mutation")]
[JsonDerivedType(typeof(ScoreEvent), "score")]
[JsonDerivedType(typeof(MarkerErrorEvent), "marker-error")]
public abstract record TestLogEvent
{
    /// <summary>
    /// The wire timestamp, verbatim, in <see cref="Timestamps.Format"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately a string rather than a <see cref="DateTime"/>: it round-trips exactly, it is the same text
    /// the bash emitter writes, and deciding what an absent or unparseable value means is a consumer policy —
    /// see <see cref="TimestampOr"/>. Null when unset; <c>log-error</c> carries none at all.
    /// </remarks>
    public string? Timestamp { get; init; }

    /// <summary>
    /// The line this event was read from, or null when it was constructed in memory. Never serialized.
    /// </summary>
    /// <remarks>
    /// Carries the raw payload the platform persists for metric events, and the fallback text for a
    /// <c>marker-error</c> that arrived without a <c>detail</c>.
    /// </remarks>
    [JsonIgnore]
    public string? RawLine { get; init; }

    /// <summary>
    /// This event's wire discriminator.
    /// </summary>
    /// <remarks>
    /// Read from the polymorphic registry rather than stored, so there is exactly one place an event kind is
    /// named. Not serialized: the serializer emits the discriminator itself, and a second property of the same
    /// name would be a duplicate key.
    /// </remarks>
    [JsonIgnore]
    public virtual string Event => TestLogEventTypes.Of(GetType());

    /// <summary>The timestamp as a UTC instant, or <paramref name="fallback"/> when absent or unparseable.</summary>
    /// <param name="fallback">Value to use when the timestamp cannot be read.</param>
    /// <returns>The parsed instant, or the fallback.</returns>
    public DateTime TimestampOr(DateTime fallback) =>
        Timestamps.TryParse(Timestamp, out var parsed) ? parsed : fallback;
}
