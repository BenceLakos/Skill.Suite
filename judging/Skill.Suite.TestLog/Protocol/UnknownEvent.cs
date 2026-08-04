using System.Text.Json.Serialization;

namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// A well-formed event whose kind this version of the contract does not know.
/// </summary>
/// <param name="Name">The unrecognised discriminator, verbatim.</param>
/// <remarks>
/// <para>
/// Deliberately not registered as a polymorphic derived type: its discriminator is whatever the producer wrote,
/// which is precisely the thing a registry cannot enumerate. It is produced by the reader and never serialized —
/// writing one throws, and <c>TestLogger.Emit</c> degrades that to a <c>log-error</c> line.
/// </para>
/// <para>
/// Distinguishing "unknown kind" from "malformed line" is a deliberate improvement over the previous readers,
/// which silently dropped both. They are different facts about a run: a malformed line means something wrote
/// broken JSON, while an unknown kind usually means a newer producer is talking to an older consumer. Consumers
/// must still ignore these for scoring — the protocol has always promised unknown kinds are safe to add — but
/// they can now be counted and reported instead of vanishing.
/// </para>
/// </remarks>
public sealed record UnknownEvent([property: JsonIgnore] string Name) : TestLogEvent
{
    /// <inheritdoc />
    public override string Event => Name;
}
