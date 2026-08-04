using System.Text.Json.Serialization;

namespace Skill.Suite.Application.Webhooks;

/// <summary>
/// Inbound git-webhook payload. The shape is a deliberate superset so the same endpoint
/// can serve raw pushes and GitHub-style hooks — flat fields take precedence, then nested
/// repository/head_commit objects fill in what's missing.
/// </summary>
public sealed record GitWebhookRequest(
    [property: JsonPropertyName("repository_url")] string? RepositoryUrl,
    [property: JsonPropertyName("clone_url")] string? CloneUrl,
    [property: JsonPropertyName("repository_name")] string? RepositoryName,
    [property: JsonPropertyName("branch")] string? Branch,
    [property: JsonPropertyName("ref")] string? Ref,
    [property: JsonPropertyName("commit")] string? Commit,
    [property: JsonPropertyName("after")] string? After,
    [property: JsonPropertyName("repository")] GitWebhookRepository? Repository,
    [property: JsonPropertyName("head_commit")] GitWebhookCommit? HeadCommit)
{
    public string? ResolveRepositoryUrl() =>
        FirstNonEmpty(
            RepositoryUrl,
            CloneUrl,
            Repository?.CloneUrl,
            Repository?.Url,
            Repository?.HtmlUrl);

    public string? ResolveRepositoryName() =>
        FirstNonEmpty(RepositoryName, Repository?.FullName, Repository?.Name);

    /// <summary>
    /// Owner of the repository — the organisation, which identifies the session.
    /// </summary>
    /// <remarks>
    /// Falls back to the first segment of <c>full_name</c> so a payload that carries only that still
    /// resolves. Untrusted until the signature has been verified; it selects which key to verify with, and a
    /// forged owner selects a key the signature cannot match.
    /// </remarks>
    public string? ResolveOwner()
    {
        var explicitOwner = FirstNonEmpty(Repository?.Owner?.Username, Repository?.Owner?.Login);
        if (!string.IsNullOrWhiteSpace(explicitOwner))
            return explicitOwner;

        var fullName = Repository?.FullName;
        if (string.IsNullOrWhiteSpace(fullName))
            return null;

        var slash = fullName.IndexOf('/');
        return slash > 0 ? fullName[..slash] : null;
    }

    /// <summary>The repository's own name without the owner — the competitor's username.</summary>
    public string? ResolveRepositorySlug()
    {
        if (!string.IsNullOrWhiteSpace(Repository?.Name))
            return Repository.Name;

        var name = ResolveRepositoryName();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var cleaned = name.Trim().TrimEnd('/');
        if (cleaned.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^4];

        return cleaned[(cleaned.LastIndexOf('/') + 1)..];
    }

    /// <summary>
    /// Whether this push targets a branch rather than a tag.
    /// </summary>
    /// <remarks>
    /// A tag push arrives as an ordinary push event with <c>refs/tags/…</c>, and passing that on as a branch
    /// makes the clone fail with a raw git error. The hook's own branch filter normally catches it; this is
    /// the check that does not depend on the hook having been configured correctly.
    /// </remarks>
    public bool IsBranchPush() =>
        string.IsNullOrWhiteSpace(Ref) || Ref.StartsWith("refs/heads/", StringComparison.Ordinal);

    public string? ResolveBranch()
    {
        if (!string.IsNullOrWhiteSpace(Branch))
            return Branch;

        if (string.IsNullOrWhiteSpace(Ref))
            return null;

        const string prefix = "refs/heads/";
        return Ref.StartsWith(prefix, StringComparison.Ordinal) ? Ref[prefix.Length..] : Ref;
    }

    public string? ResolveCommitSha() => FirstNonEmpty(Commit, After, HeadCommit?.Id);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

public sealed record GitWebhookRepository(
    [property: JsonPropertyName("clone_url")] string? CloneUrl,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("html_url")] string? HtmlUrl,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("full_name")] string? FullName,
    [property: JsonPropertyName("owner")] GitWebhookOwner? Owner = null);

/// <summary>
/// Repository owner. Gitea populates <c>username</c> and <c>login</c> identically; GitHub sends only
/// <c>login</c>, so both are read.
/// </summary>
public sealed record GitWebhookOwner(
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("login")] string? Login);

public sealed record GitWebhookCommit(
    [property: JsonPropertyName("id")] string? Id);
