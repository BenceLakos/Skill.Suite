namespace Skill.Suite.Application.Webhooks;

/// <summary>
/// Replaces the scheme/host/port of a repository URL with the configured internal base
/// URL — used right before <c>git clone</c> so the URL resolves from inside the docker
/// network even when the webhook advertised an externally-reachable host.
/// </summary>
public static class RepositoryUrlRewriter
{
    public static string Rewrite(string originalUrl, string? internalBaseUrl)
    {
        if (string.IsNullOrEmpty(originalUrl) || string.IsNullOrWhiteSpace(internalBaseUrl))
            return originalUrl;

        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var original))
            return originalUrl;

        // The path-and-query is everything after the authority; we splice it onto the
        // configured base URL without touching its trailing slash. Userinfo (auth) and
        // fragment from the original URL are intentionally dropped: auth is set later by
        // ProcessGitClient from the configured credential, and webhooks don't carry
        // fragments.
        var trimmedBase = internalBaseUrl.TrimEnd('/');
        return trimmedBase + original.PathAndQuery;
    }
}
