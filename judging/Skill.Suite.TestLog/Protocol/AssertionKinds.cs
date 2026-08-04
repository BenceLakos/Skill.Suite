namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// The assertion labels the xUnit harness emits.
/// </summary>
/// <remarks>
/// Constants rather than an enum, on purpose. The wire field is an open string: the consumer stores it verbatim
/// for display and never branches on it, and a third-party emitter may use a label of its own. Narrowing it to
/// an enum would need an <c>Unknown</c> member plus a parallel raw string to avoid replacing someone else's
/// label with <c>Unknown</c> in the UI — two fields for one wire value. Constants remove every literal at a
/// fraction of that machinery.
/// <para>
/// Contrast <see cref="TestLogOutcome"/>, which is a closed four-value set the consumer really does branch on,
/// and is therefore an enum.
/// </para>
/// </remarks>
public static class AssertionKinds
{
    /// <summary>Equality, including the tolerance and precision overloads.</summary>
    public const string Equal = "equal";

    /// <summary>Inequality.</summary>
    public const string NotEqual = "not-equal";

    /// <summary>A condition expected to hold.</summary>
    public const string True = "true";

    /// <summary>A condition expected not to hold.</summary>
    public const string False = "false";

    /// <summary>A value expected to be null.</summary>
    public const string Null = "null";

    /// <summary>A value expected to be non-null.</summary>
    public const string NotNull = "not-null";

    /// <summary>Reference identity.</summary>
    public const string Same = "same";

    /// <summary>Reference non-identity.</summary>
    public const string NotSame = "not-same";

    /// <summary>An empty collection.</summary>
    public const string Empty = "empty";

    /// <summary>A non-empty collection.</summary>
    public const string NotEmpty = "not-empty";

    /// <summary>A collection with exactly one element.</summary>
    public const string Single = "single";

    /// <summary>Containment, of an element in a collection or a substring in a string.</summary>
    public const string Contains = "contains";

    /// <summary>An action expected to throw.</summary>
    public const string Throws = "throws";
}
