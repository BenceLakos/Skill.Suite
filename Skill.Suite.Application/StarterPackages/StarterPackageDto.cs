namespace Skill.Suite.Application.StarterPackages;

/// <summary>One top-level directory of the starter packages volume.</summary>
/// <param name="TemplateFolder">
/// The value a session's <c>TemplateFolder</c> takes to use this package — the container path of its
/// <c>competitor-start</c> folder, whether or not that folder exists yet.
/// </param>
public sealed record StarterPackageDto(
    string Name,
    long SizeBytes,
    DateTimeOffset ModifiedAt,
    bool HasCompetitorStart,
    string TemplateFolder);
