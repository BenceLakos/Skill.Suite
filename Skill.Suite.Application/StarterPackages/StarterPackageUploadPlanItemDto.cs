namespace Skill.Suite.Application.StarterPackages;

/// <summary>What an upload would do with one of its files.</summary>
/// <param name="RelativePath">
/// Exactly as the request gave it, so the browser can match the answer to the file it describes.
/// </param>
public sealed record StarterPackageUploadPlanItemDto(
    string RelativePath,
    long SizeBytes,
    StarterPackageUploadAction Action);
