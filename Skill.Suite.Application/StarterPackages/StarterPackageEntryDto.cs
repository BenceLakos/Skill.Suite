namespace Skill.Suite.Application.StarterPackages;

/// <summary>One folder or file inside a starter package.</summary>
/// <param name="RelativePath">Path from the starter packages root, always separated by <c>/</c>.</param>
public sealed record StarterPackageEntryDto(
    string Name,
    string RelativePath,
    bool IsDirectory,
    long SizeBytes,
    DateTimeOffset ModifiedAt);
