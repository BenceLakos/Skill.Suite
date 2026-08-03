namespace Skill.Suite.Domain.TestRuns;

public sealed record TestEventRecord(
    TestEventKind Kind,
    DateTime Timestamp,
    string? Target,
    IReadOnlyList<string?>? Arguments,
    string? Returned,
    string? Threw,
    string? AssertionKind,
    string? Expected,
    string? Actual,
    bool? Passed,
    /// <summary>
    /// Raw JSON payload for <see cref="TestEventKind.Metric"/> events. Always null for
    /// Call / Assertion records — those fully populate the structured fields above.
    /// </summary>
    string? Payload = null,
    /// <summary>
    /// Human-readable detail for <see cref="TestEventKind.Error"/> records. Always null for the other
    /// kinds. Appended last and optional, so existing jsonb rows deserialize with it null and no
    /// migration is needed.
    /// </summary>
    string? Detail = null);
