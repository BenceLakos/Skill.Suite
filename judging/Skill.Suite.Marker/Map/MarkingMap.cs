namespace Skill.Suite.Marker.Map;

/// <summary>
/// The per-session marking configuration: scoring parts, scoring constants, and the aspect set.
/// </summary>
/// <remarks>
/// Everything session-specific lives here rather than in the marker, which is what makes one tool usable
/// across every session. The original implementation hard-coded its two part names and four ramp
/// constants; a new session could not be added without editing and republishing the tool.
/// </remarks>
public sealed record MarkingMap
{
    /// <summary>Event-protocol version this map targets. Only <c>1</c> is understood.</summary>
    public int Protocol { get; init; } = 1;

    /// <summary>Black-box scoring parts. Empty means score <c>overall</c> only.</summary>
    public IReadOnlyList<PartRule> Parts { get; init; } = [];

    /// <summary>Scoring constants. Omitted members fall back to the documented defaults.</summary>
    public ScoringOptions Scoring { get; init; } = new();

    /// <summary>Aspects the CIS report must contain a row for. Used by <c>report</c> only.</summary>
    public IReadOnlyList<AspectRule> Aspects { get; init; } = [];

    /// <summary>The reserved part name every submission is scored under, in addition to any declared part.</summary>
    public const string OverallPart = "overall";

    /// <summary>Part ids to score: each declared part, then <see cref="OverallPart"/>.</summary>
    public IReadOnlyList<string> ScoredParts() =>
        [.. Parts.Select(part => part.Id), OverallPart];
}
