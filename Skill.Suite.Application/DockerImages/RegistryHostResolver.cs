namespace Skill.Suite.Application.DockerImages;

/// <summary>
/// Works out which registry host an image reference offered in the UI should carry.
/// </summary>
/// <remarks>
/// Gitea serves its container registry on the same host and port as its API, so the registry host is the
/// internal base URL with the scheme and any path removed. Nothing is configured separately for it.
/// </remarks>
public static class RegistryHostResolver
{
    /// <summary>The git host as this process reaches it when configuration does not say.</summary>
    public const string DefaultRegistryHost = "gitea:3000";

    public static string Resolve(string? gitInternalBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(gitInternalBaseUrl))
            return DefaultRegistryHost;

        if (Uri.TryCreate(gitInternalBaseUrl, UriKind.Absolute, out var url) && !string.IsNullOrEmpty(url.Authority))
            return url.Authority.ToLowerInvariant();

        var trimmed = gitInternalBaseUrl.Trim();
        var pathStart = trimmed.IndexOf('/');
        if (pathStart >= 0)
            trimmed = trimmed[..pathStart];

        return string.IsNullOrWhiteSpace(trimmed) ? DefaultRegistryHost : trimmed.ToLowerInvariant();
    }
}
