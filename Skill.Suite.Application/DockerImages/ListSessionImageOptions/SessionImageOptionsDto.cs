namespace Skill.Suite.Application.DockerImages.ListSessionImageOptions;

/// <summary>
/// The image references to offer, and why the list may be shorter than expected.
/// </summary>
/// <param name="Images">Every reference to offer, deduplicated and ordered.</param>
/// <param name="RegistryWarning">
/// <see langword="null"/> when the registry was listed. Otherwise a sentence for the operator explaining why
/// only the registered images are on offer — the list itself is still usable, which is the point of carrying
/// the reason alongside it rather than failing the query.
/// </param>
public sealed record SessionImageOptionsDto(IReadOnlyList<string> Images, string? RegistryWarning);
