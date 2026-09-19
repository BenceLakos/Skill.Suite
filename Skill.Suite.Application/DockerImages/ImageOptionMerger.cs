namespace Skill.Suite.Application.DockerImages;

/// <summary>
/// Combines the images registered in the database with the ones discovered on the registry into the single
/// list an image picker offers.
/// </summary>
/// <remarks>
/// Preconfigured entries are kept ahead of discovered ones before deduplication, so when the same reference
/// arrives from both it survives with the casing an operator typed and the registry's lower-cased rendering
/// of it does not appear as a second, near-identical option.
/// </remarks>
public static class ImageOptionMerger
{
    public static List<string> Merge(IEnumerable<string> preconfigured, IEnumerable<string> discovered) =>
    [
        .. preconfigured
            .Concat(discovered)
            .Select(image => image?.Trim() ?? string.Empty)
            .Where(image => image.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(image => image, StringComparer.OrdinalIgnoreCase),
    ];
}
