using System.Text.Json;

namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// Serializes an event record to its wire line.
/// </summary>
public static class TestLogEventWriter
{
    /// <summary>Serializes one event to a single JSON line, with no trailing newline.</summary>
    /// <param name="event">The event to write.</param>
    /// <returns>The wire line.</returns>
    /// <remarks>
    /// <para>
    /// <b>Serialized as <see cref="TestLogEvent"/>, deliberately.</b> The polymorphic configuration lives on the
    /// base, so the discriminator is only written when the serializer is told the static type is the base.
    /// Passing the runtime type instead — <c>Serialize(@event, @event.GetType(), …)</c> — produces a line with
    /// the payload but no <c>event</c> field, which every consumer then discards as unusable.
    /// </para>
    /// <para>
    /// That is the exact inverse of what this method needed before the hierarchy became polymorphic, so it is
    /// worth reading twice. The round-trip tests cover both halves.
    /// </para>
    /// </remarks>
    public static string ToLine(TestLogEvent @event) =>
        JsonSerializer.Serialize(@event, EventJson.Options);
}
