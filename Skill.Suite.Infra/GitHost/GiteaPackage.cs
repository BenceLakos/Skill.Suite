namespace Skill.Suite.Infra.GitHost;

/// <summary>
/// The subset of Gitea's package representation the image pickers depend on.
/// </summary>
/// <remarks>
/// <c>version</c> is the tag for a tagged container package and a <c>sha256:</c> digest for an untagged
/// manifest, so it is not usable as a tag without checking which one arrived.
/// </remarks>
internal sealed record GiteaPackage
{
    public string? Type { get; init; }

    public string? Name { get; init; }

    public string? Version { get; init; }
}
