using System.Text.Json;
using Skill.Suite.Marker.Model;

namespace Skill.Suite.Marker.Readers;

/// <summary>
/// Reads a Stryker.NET JSON mutation report into per-part mutation outcomes.
/// </summary>
public static class StrykerReader
{
    /// <summary>Reads the report and sums mutant outcomes per part plus overall.</summary>
    /// <param name="path">Report path. A missing path yields empty stats.</param>
    /// <param name="router">Routes a source file path to a part.</param>
    /// <returns>Outcomes keyed by part id, always including <see cref="Map.MarkingMap.OverallPart"/>.</returns>
    /// <remarks>
    /// A malformed report is skipped like a malformed TRX or Cobertura file rather than failing the run.
    /// The original implementation parsed this one outside its try/catch, so a truncated Stryker report
    /// turned a scored submission into a failed run — inconsistent with how the other two readers behave,
    /// and the harsher outcome for the least essential input.
    /// </remarks>
    public static IReadOnlyDictionary<string, MutationStats> Read(string? path, PartRouter router)
    {
        var mutation = new Dictionary<string, MutationStats>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return mutation;

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            Accumulate(document, router, mutation);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return mutation;
        }

        return mutation;
    }

    private static void Accumulate(
        JsonDocument document, PartRouter router, Dictionary<string, MutationStats> mutation)
    {
        if (!document.RootElement.TryGetProperty("files", out var files) ||
            files.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var file in files.EnumerateObject())
        {
            // Stryker keys files by absolute path.
            var part = router.Route(file.Name);

            if (!file.Value.TryGetProperty("mutants", out var mutants) ||
                mutants.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var delta = default(MutationStats);
            foreach (var mutant in mutants.EnumerateArray())
            {
                var status = mutant.TryGetProperty("status", out var statusProperty) &&
                             statusProperty.ValueKind == JsonValueKind.String
                    ? statusProperty.GetString()
                    : null;

                delta = delta.Add(Classify(status));
            }

            if (delta == default) continue;

            if (part is not null)
                mutation[part] = mutation.GetValueOrDefault(part).Add(delta);

            mutation[Map.MarkingMap.OverallPart] =
                mutation.GetValueOrDefault(Map.MarkingMap.OverallPart).Add(delta);
        }
    }

    private static MutationStats Classify(string? status) => status switch
    {
        "Killed" => new MutationStats(1, 0, 0, 0, 0),
        "Survived" => new MutationStats(0, 1, 0, 0, 0),
        "Timeout" => new MutationStats(0, 0, 1, 0, 0),
        "NoCoverage" => new MutationStats(0, 0, 0, 1, 0),
        // CompileError, Ignored, missing or unknown status.
        _ => new MutationStats(0, 0, 0, 0, 1),
    };
}
