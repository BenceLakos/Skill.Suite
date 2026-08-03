using System.Text.Json.Serialization;

namespace Skill.Suite.Marker.Map;

/// <summary>
/// One black-box scoring part, and how test classes and source files are routed to it.
/// </summary>
/// <remarks>
/// <para>
/// Routing matches a <b>whole segment</b> of a namespace or path, case-insensitively — so part
/// <c>validator</c> claims <c>Acme.Validator.Tests.FooTests</c> and <c>src/Validator/Foo.cs</c>, but not
/// <c>DisbursementTests</c>. Whole-segment matching is deliberate: substring matching produced
/// false positives in the original implementation.
/// </para>
/// <para>
/// In JSON a part is either a bare string, where the id doubles as the segment to match:
/// <c>"parts": ["validator", "optimizer"]</c>
/// or an object when the segment names differ from the id:
/// <c>{ "id": "aqi", "segments": ["AirWatch.Aqi.Services", "Aqi"] }</c>
/// </para>
/// </remarks>
[JsonConverter(typeof(PartRuleConverter))]
public sealed record PartRule
{
    /// <summary>The part name, used as the <c>part</c> field on emitted events.</summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// Namespace or path segments that route to this part. Dotted values are split, so
    /// <c>"AirWatch.Aqi.Services"</c> contributes three segments. Defaults to <see cref="Id"/>.
    /// </summary>
    public IReadOnlyList<string> Segments { get; init; } = [];

    /// <summary>Builds a rule from the bare-string JSON form.</summary>
    public static PartRule FromId(string id) => new() { Id = id, Segments = [id] };

    /// <summary>Expands dotted segment values and falls back to the id when none were given.</summary>
    public PartRule Normalized()
    {
        var expanded = Segments
            .SelectMany(segment => segment.Split(['.', '/', '\\'], StringSplitOptions.RemoveEmptyEntries))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return this with { Segments = expanded.Length > 0 ? expanded : [Id] };
    }
}
