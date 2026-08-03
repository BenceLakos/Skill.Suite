using System.Reflection;
using System.Text.Json.Serialization;

namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// The registry of event kinds, read from the polymorphic attributes on <see cref="TestLogEvent"/>.
/// </summary>
/// <remarks>
/// There is no separate list of event-name constants. The <see cref="JsonDerivedTypeAttribute"/> entries are the
/// single declaration: the serializer uses them to write the discriminator, and this type projects the same
/// entries for the reader and for <see cref="TestLogEvent.Event"/>. Adding an event kind is therefore one line
/// in one place.
/// </remarks>
public static class TestLogEventTypes
{
    private static readonly IReadOnlyDictionary<Type, string> Discriminators =
        typeof(TestLogEvent)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .ToDictionary(a => a.DerivedType, a => (string)a.TypeDiscriminator!);

    /// <summary>Every registered event kind, by its record type.</summary>
    public static IReadOnlyDictionary<Type, string> All => Discriminators;

    /// <summary>The wire discriminator for an event record type.</summary>
    /// <param name="type">The record type.</param>
    /// <returns>The discriminator, or an empty string for a type that is not registered.</returns>
    /// <remarks>
    /// An unregistered type is not an error here: <see cref="UnknownEvent"/> is deliberately unregistered and
    /// overrides <see cref="TestLogEvent.Event"/> with the name it actually read.
    /// </remarks>
    public static string Of(Type type) => Discriminators.GetValueOrDefault(type, string.Empty);

    /// <summary>The wire discriminator for an event record type.</summary>
    /// <typeparam name="TEvent">The record type.</typeparam>
    /// <returns>The discriminator.</returns>
    public static string Of<TEvent>() where TEvent : TestLogEvent => Of(typeof(TEvent));
}
