using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// The frozen protocol wire settings, in a type with no side effects.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately separate from the producer. <c>TestLogger</c> opens its sink from a static field initializer, so
/// merely touching that type — even to read its serializer options — opens
/// <c>$LOG_DIRECTORY/events.jsonl</c> with <see cref="FileMode.Create"/> and truncates it.
/// </para>
/// <para>
/// That is exactly what happened when the marker shared the options through <c>TestLogger</c>: it destroyed the
/// test events it was about to append its own scores to, leaving a file of the right length full of NUL bytes.
/// Anything that needs the wire format but must not touch the sink uses this type instead.
/// </para>
/// <para>
/// <b>Wire names come from the naming policy, not from per-member attributes.</b> A camelCase policy turns
/// <c>TestsRun</c> into <c>testsRun</c> and <c>Event</c> into <c>event</c>, which is every name the protocol
/// needs. Annotating forty members individually to say the same thing added no information and a great deal of
/// noise. The same policy applies to enum values, so a verdict serializes as <c>"passed"</c> rather than
/// <c>"Passed"</c>.
/// </para>
/// <para>
/// Because the policy is the single definition of how a C# name becomes a wire name, the reader derives its
/// probes the same way — see <see cref="TestLogEventReader"/>. That is what keeps reader and writer in step
/// without a parallel list of name constants to maintain.
/// </para>
/// </remarks>
public static class EventJson
{
    /// <summary>
    /// The serializer settings every producer and consumer of the event protocol must use. Treat as read-only.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The wire name for a C# property or enum member name.</summary>
    /// <param name="name">The C# name, normally supplied with <c>nameof</c>.</param>
    /// <returns>The name as it appears in JSON.</returns>
    public static string Wire(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);
}
