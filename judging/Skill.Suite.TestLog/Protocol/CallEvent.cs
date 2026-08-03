namespace Skill.Suite.TestLog.Protocol;

/// <summary>One call into the system under test.</summary>
/// <param name="Fixture">Owning fixture name.</param>
/// <param name="Test">Test case name.</param>
/// <param name="Target">Called member, conventionally <c>IInterface.Method</c>.</param>
/// <param name="Arguments">Arguments as logged.</param>
/// <param name="Returned">Return value, when there was one.</param>
/// <param name="Threw">Description of the thrown exception; non-empty marks the unit errored.</param>
/// <remarks>
/// <para>
/// <see cref="Arguments"/> and <see cref="Returned"/> are <c>object?</c> because they legitimately carry
/// numbers, booleans, strings, null, and nested objects — a <see cref="System.IO.Stream"/> argument is
/// snapshotted into a <c>$stream</c> object. Typing them as strings would quote every value.
/// </para>
/// <para>
/// A void call and a call that returned null are indistinguishable on the wire. That is a known limitation of
/// the protocol, carried over deliberately rather than fixed here.
/// </para>
/// </remarks>
public sealed record CallEvent(
    string? Fixture,
    string? Test,
    string? Target,
    IReadOnlyList<object?>? Arguments,
    object? Returned = null,
    string? Threw = null)
    : TestLogEvent;
