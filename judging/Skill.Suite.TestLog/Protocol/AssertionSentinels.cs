namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// Placeholders used as an assertion's <c>expected</c> or <c>actual</c> where there is no real value to report.
/// </summary>
/// <remarks>
/// Display-only: nothing reads these, they exist so a human looking at an assertion row sees why it passed or
/// failed. The angle brackets mark them as not-a-value; a genuine string value would be quoted JSON without
/// them.
/// </remarks>
public static class AssertionSentinels
{
    /// <summary>Expected, for a not-null assertion.</summary>
    public const string NonNull = "<non-null>";

    /// <summary>Expected, for an empty-collection assertion.</summary>
    public const string Empty = "<empty>";

    /// <summary>Expected, for a non-empty-collection assertion.</summary>
    public const string NonEmpty = "<non-empty>";

    /// <summary>Expected, for a single-element assertion.</summary>
    public const string SingleElement = "<single element>";

    /// <summary>Actual, for a throws assertion where nothing was thrown.</summary>
    public const string NoException = "<no exception>";
}
