namespace Skill.Suite.Marker.Model;

/// <summary>
/// The verdict of one unit test, as reconstructed from the event stream.
/// </summary>
/// <remarks>
/// Mirrors the platform's own outcome enum, including its promotion rules: <see cref="Errored"/> and
/// <see cref="Failed"/> cannot be downgraded by a later <c>finish-unit-test</c> claiming success.
/// </remarks>
public enum UnitOutcome
{
    /// <summary>No verdict was recorded.</summary>
    Unknown = 0,

    /// <summary>The test passed.</summary>
    Passed = 1,

    /// <summary>An assertion failed.</summary>
    Failed = 2,

    /// <summary>The test was skipped and produced no verdict.</summary>
    Skipped = 3,

    /// <summary>The system under test threw.</summary>
    Errored = 4,
}
