namespace Skill.Suite.TestLog.Protocol;

/// <summary>
/// Wire spellings for <see cref="TestLogOutcome"/>, and the tolerant parse both readers share.
/// </summary>
public static class Outcomes
{
    /// <summary>The test passed.</summary>
    public const string Passed = "passed";

    /// <summary>An assertion failed.</summary>
    public const string Failed = "failed";

    /// <summary>The test did not run.</summary>
    public const string Skipped = "skipped";

    /// <summary>The test threw rather than asserting.</summary>
    public const string Errored = "errored";

    /// <summary>
    /// Maps a wire value to a verdict, tolerating case and the short forms.
    /// </summary>
    /// <param name="outcome">The wire value, which may be null or anything at all.</param>
    /// <returns>The verdict, or <see cref="TestLogOutcome.Unknown"/> when unrecognised.</returns>
    /// <remarks>
    /// Never throws. The stream is written by code running inside a competitor's submission, so an unexpected
    /// value must degrade to <c>Unknown</c> rather than fault the consumer. The short forms are accepted because
    /// the original consumer accepted them; nothing in this repository emits them.
    /// </remarks>
    public static TestLogOutcome Parse(string? outcome) => outcome?.ToLowerInvariant() switch
    {
        Passed or "pass" => TestLogOutcome.Passed,
        Failed or "fail" => TestLogOutcome.Failed,
        Skipped or "skip" => TestLogOutcome.Skipped,
        Errored or "error" => TestLogOutcome.Errored,
        _ => TestLogOutcome.Unknown,
    };

    /// <summary>The wire spelling of a verdict.</summary>
    /// <param name="outcome">The verdict.</param>
    /// <returns>The wire value.</returns>
    public static string ToWire(TestLogOutcome outcome) => outcome switch
    {
        TestLogOutcome.Passed => Passed,
        TestLogOutcome.Failed => Failed,
        TestLogOutcome.Skipped => Skipped,
        TestLogOutcome.Errored => Errored,
        _ => "unknown",
    };
}
