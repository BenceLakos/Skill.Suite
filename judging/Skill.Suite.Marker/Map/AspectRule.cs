namespace Skill.Suite.Marker.Map;

/// <summary>
/// One marking-scheme aspect the CIS report must have a row for.
/// </summary>
/// <remarks>
/// Declaring the full aspect set is what lets the report distinguish "every test for this aspect passed"
/// from "no test covered this aspect at all" — the latter must still produce a <c>no</c> row rather than
/// silently vanishing. Test cases claim their aspect through the <c>[Aspect]</c> attribute, so nothing
/// here needs to know about test names.
/// </remarks>
public sealed record AspectRule
{
    /// <summary>The aspect id, matching the <c>[Aspect("...")]</c> value on the test cases.</summary>
    public string Id { get; init; } = "";

    /// <summary>Optional human-readable label, for the report only.</summary>
    public string? Label { get; init; }
}
