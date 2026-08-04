using System.Text.Json;

namespace Skill.Suite.Marker.Map;

/// <summary>
/// Loads and validates <c>marking-map.json</c>.
/// </summary>
public static class MarkingMapLoader
{
    /// <summary>Highest event-protocol version this marker understands.</summary>
    public const int SupportedProtocol = 1;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a map from disk, normalizes it, and validates it.</summary>
    /// <param name="path">Path to the map file.</param>
    /// <returns>The validated map.</returns>
    /// <exception cref="MarkingMapException">The file is missing, malformed, or invalid.</exception>
    public static MarkingMap Load(string path)
    {
        if (!File.Exists(path))
            throw new MarkingMapException($"marking map not found: {path}");

        MarkingMap? map;
        try
        {
            map = JsonSerializer.Deserialize<MarkingMap>(File.ReadAllText(path), ReadOptions);
        }
        catch (JsonException ex)
        {
            throw new MarkingMapException($"marking map {path} is not valid JSON: {ex.Message}", ex);
        }

        if (map is null)
            throw new MarkingMapException($"marking map {path} is empty.");

        map = map with { Parts = [.. map.Parts.Select(part => part.Normalized())] };

        var problems = Validate(map);
        if (problems.Count > 0)
            throw new MarkingMapException($"marking map {path} is invalid: {string.Join(" ", problems)}");

        return map;
    }

    private static List<string> Validate(MarkingMap map)
    {
        var problems = new List<string>();

        if (map.Protocol != SupportedProtocol)
        {
            problems.Add(
                $"protocol {map.Protocol} is not supported (this marker understands {SupportedProtocol}).");
        }

        problems.AddRange(map.Scoring.Validate());

        var duplicateParts = map.Parts
            .GroupBy(part => part.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var duplicate in duplicateParts)
            problems.Add($"part '{duplicate}' is declared more than once.");

        foreach (var part in map.Parts.Where(part => string.IsNullOrWhiteSpace(part.Id)))
            problems.Add("a part has an empty id.");

        // "overall" is emitted for every submission, so a declared part of that name would collide with
        // the rollup and double-write the same events.
        if (map.Parts.Any(part => string.Equals(part.Id, MarkingMap.OverallPart, StringComparison.OrdinalIgnoreCase)))
            problems.Add($"'{MarkingMap.OverallPart}' is reserved and must not be declared as a part.");

        var duplicateAspects = map.Aspects
            .GroupBy(aspect => aspect.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var duplicate in duplicateAspects)
            problems.Add($"aspect '{duplicate}' is declared more than once.");

        foreach (var aspect in map.Aspects.Where(aspect => string.IsNullOrWhiteSpace(aspect.Id)))
            problems.Add("an aspect has an empty id.");

        return problems;
    }
}
