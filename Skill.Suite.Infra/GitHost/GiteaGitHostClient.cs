using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.GitHost;

/// <summary>
/// <see cref="IGitHostClient"/> over the Gitea REST API (verified against Gitea 1.26).
/// </summary>
/// <remarks>
/// Two behaviours of that API are load-bearing and neither is obvious from its documentation, so both are
/// guarded here rather than left to the caller:
/// <list type="number">
/// <item>
/// <b>Generating from a template silently produces an empty repository</b> when the template's content is
/// not yet visible to the server — which is exactly the case moments after the template has been pushed.
/// The response is a 201 describing a normal repository. Hence
/// <see cref="WaitForRepositoryContentAsync"/> before generating, and a verification pass after.
/// </item>
/// <item>
/// <c>git_content</c> defaults to <see langword="false"/>, and omitting it fails the whole call with
/// "must select at least one template item" rather than copying nothing. That one at least fails loudly, but
/// it is why the flag is always sent.
/// </item>
/// </list>
/// Every operation is idempotent because provisioning walks N competitors over a network and has to be
/// restartable: an existing organisation, repository or hook is a success.
/// </remarks>
internal sealed class GiteaGitHostClient(HttpClient http, ILogger<GiteaGitHostClient> logger)
    : IGitHostClient
{
    private readonly GiteaApi api = new(http);

    /// <summary>How long to wait for a freshly pushed repository to report content.</summary>
    private static readonly TimeSpan ContentPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Users per page when listing accounts.</summary>
    private const int UserPageSize = 50;

    /// <summary>
    /// Hard stop on paging, so a host that keeps returning full pages cannot spin the status query forever.
    /// </summary>
    private const int MaxUserPages = 40;

    public async Task EnsureOrganizationAsync(
        EnsureOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (await api.ExistsAsync($"orgs/{GiteaApi.Escape(request.Name)}", request.Credential, cancellationToken))
        {
            logger.LogInformation("Gitea organisation {Org} already exists", request.Name);
            return;
        }

        var body = new Dictionary<string, object?>
        {
            ["username"] = request.Name,
            ["description"] = request.Description,
            // Competitor repositories must not be world-readable: one competitor reading another's
            // submission is the whole competition compromised.
            ["visibility"] = "private",
        };

        await api.SendAsync(HttpMethod.Post, "orgs", body, request.Credential,
            $"create organisation '{request.Name}'", cancellationToken);

        logger.LogInformation("Created Gitea organisation {Org}", request.Name);
    }

    public async Task EnsureTemplateRepositoryAsync(
        EnsureRepositoryRequest request, CancellationToken cancellationToken)
    {
        var (owner, name) = (request.Repository.Owner, request.Repository.Name);

        if (await api.ExistsAsync($"repos/{GiteaApi.Escape(owner)}/{GiteaApi.Escape(name)}", request.Credential, cancellationToken))
        {
            logger.LogInformation("Template repository {Owner}/{Name} already exists", owner, name);
            return;
        }

        var body = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["description"] = request.Description,
            ["private"] = true,
            ["template"] = true,
            // No auto_init: the starter package is pushed as the first commit, and an auto-created README
            // would have to be merged with it.
            ["auto_init"] = false,
        };

        await api.SendAsync(HttpMethod.Post, $"orgs/{GiteaApi.Escape(owner)}/repos", body, request.Credential,
            $"create template repository '{owner}/{name}'", cancellationToken);

        logger.LogInformation("Created template repository {Owner}/{Name}", owner, name);
    }

    public async Task<string> GenerateRepositoryFromTemplateAsync(
        GenerateRepositoryRequest request, CancellationToken cancellationToken)
    {
        var (owner, name) = (request.Target.Owner, request.Target.Name);
        var path = $"repos/{GiteaApi.Escape(request.Template.Owner)}/{GiteaApi.Escape(request.Template.Name)}/generate";

        var existing = await GetRepositoryAsync(request.Target, request.Credential, cancellationToken);
        if (existing is null)
        {
            var body = new Dictionary<string, object?>
            {
                ["owner"] = owner,
                ["name"] = name,
                // Mandatory. Without it Gitea rejects the request outright rather than copying an empty repo.
                ["git_content"] = true,
                // Without this the repository record's default branch stays blank, which breaks the
                // content API and shows the repository as uninitialised in the web UI.
                ["default_branch"] = request.DefaultBranch,
                ["private"] = true,
            };

            using var response = await api.SendAsync(HttpMethod.Post, path, body, request.Credential,
                $"generate repository '{owner}/{name}' from template", cancellationToken);

            existing = await response.Content.ReadFromJsonAsync<GiteaRepository>(GiteaApi.Json, cancellationToken);
            logger.LogInformation("Generated repository {Owner}/{Name} from template", owner, name);
        }
        else
        {
            logger.LogInformation("Repository {Owner}/{Name} already exists; not regenerating", owner, name);
        }

        // The response says nothing reliable about whether the copy landed, so ask again. A repository that
        // never reports content is a failed provision for this competitor, not a warning.
        var populated = await WaitForRepositoryContentAsync(
            request.Target, request.Credential, TimeSpan.FromSeconds(30), cancellationToken);

        if (!populated)
        {
            throw new GitHostException(
                $"Repository '{owner}/{name}' was created but never reported any content. The template " +
                $"'{request.Template.Owner}/{request.Template.Name}' was probably still empty when it was " +
                "copied; re-run Start to retry this competitor.");
        }

        var cloneUrl = existing?.CloneUrl;
        if (!string.IsNullOrWhiteSpace(cloneUrl))
            return cloneUrl;

        var refreshed = await GetRepositoryAsync(request.Target, request.Credential, cancellationToken);
        return refreshed?.CloneUrl
            ?? throw new GitHostException($"Repository '{owner}/{name}' reported no clone URL.");
    }

    public async Task EnsureOrganizationWebhookAsync(
        EnsureWebhookRequest request, CancellationToken cancellationToken)
    {
        var path = $"orgs/{GiteaApi.Escape(request.Organization)}/hooks";

        // Delete before creating rather than editing in place: the secret is regenerated on every Start, so
        // a hook left from an earlier attempt would keep delivering with a key nothing verifies against.
        var existing = await api.GetAsync<List<GiteaHook>>(path, request.Credential, cancellationToken) ?? [];
        foreach (var hook in existing.Where(h => h.Config is not null
                                                 && h.Config.TryGetValue("url", out var url)
                                                 && string.Equals(url, request.TargetUrl, StringComparison.OrdinalIgnoreCase)))
        {
            await api.SendAsync(HttpMethod.Delete, $"{path}/{hook.Id}", null, request.Credential,
                $"remove the previous webhook {hook.Id}", cancellationToken);
            logger.LogInformation("Removed stale webhook {HookId} on {Org}", hook.Id, request.Organization);
        }

        var body = new Dictionary<string, object?>
        {
            ["type"] = "gitea",
            ["active"] = true,
            ["events"] = new[] { "push" },
            // A tag push would otherwise arrive as a push event and be cloned as a branch that does not exist.
            ["branch_filter"] = request.BranchFilter,
            ["config"] = new Dictionary<string, string>
            {
                ["url"] = request.TargetUrl,
                ["content_type"] = "json",
                ["secret"] = request.Secret,
            },
        };

        await api.SendAsync(HttpMethod.Post, path, body, request.Credential,
            $"create the push webhook on '{request.Organization}'", cancellationToken);

        logger.LogInformation("Installed push webhook on {Org} -> {Url}",
            request.Organization, request.TargetUrl);
    }

    /// <summary>
    /// Polls until the repository reports content. An empty repository is indistinguishable from a
    /// not-yet-copied one, so this is the only way to tell a finished generate from a silently failed one.
    /// </summary>
    public async Task<bool> WaitForRepositoryContentAsync(
        RepositoryReference repository,
        BasicCredential credential,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            var repo = await GetRepositoryAsync(repository, credential, cancellationToken);
            if (repo is { Empty: false })
                return true;

            if (DateTime.UtcNow >= deadline)
            {
                logger.LogWarning("Repository {Owner}/{Name} still reports empty after {Timeout}s",
                    repository.Owner, repository.Name, timeout.TotalSeconds);
                return false;
            }

            await Task.Delay(ContentPollInterval, cancellationToken);
        }
    }

    public async Task<AccountProvisioning> EnsureUserAsync(
        EnsureGitHostUserRequest request, CancellationToken cancellationToken)
    {
        if (await api.ExistsAsync($"users/{GiteaApi.Escape(request.Username)}", request.Credential, cancellationToken))
        {
            // The existing account's password is NOT reset. A competitor may already be pushing with it, and
            // silently rotating a credential mid-competition costs them the time it takes to work out why.
            logger.LogInformation("Gitea user {Username} already exists", request.Username);
            return AccountProvisioning.AlreadyExisted;
        }

        var body = new Dictionary<string, object?>
        {
            ["username"] = request.Username,
            // Required and required to be unique, though nothing is ever sent to it: a competition has no mail
            // server, so this is an identifier wearing an address's shape.
            ["email"] = request.Email,
            ["password"] = request.Password,
            // The password is handed to the competitor on paper. Forcing a change at first login turns the
            // first five minutes of a competition into a support queue.
            ["must_change_password"] = false,
            ["send_notify"] = false,
            ["visibility"] = "private",
        };

        await api.SendAsync(HttpMethod.Post, "admin/users", body, request.Credential,
            $"create the user '{request.Username}'", cancellationToken);

        logger.LogInformation("Created Gitea user {Username}", request.Username);
        return AccountProvisioning.Created;
    }

    public async Task<IReadOnlyCollection<string>> ListUsernamesAsync(
        BasicCredential credential, CancellationToken cancellationToken)
    {
        var usernames = new List<string>();

        // Paged rather than one big limit: Gitea caps the page size server-side, so a large limit silently
        // returns one page and every competitor past it is reported as having no account.
        for (var page = 1; page <= MaxUserPages; page++)
        {
            var batch = await api.GetAsync<List<GiteaUser>>(
                $"admin/users?limit={UserPageSize}&page={page}", credential, cancellationToken) ?? [];

            usernames.AddRange(batch
                .Select(u => u.Login)
                .Where(login => !string.IsNullOrWhiteSpace(login))!);

            if (batch.Count < UserPageSize)
                return usernames;
        }

        logger.LogWarning(
            "Stopped listing Gitea users after {Pages} pages; the status column may be incomplete",
            MaxUserPages);

        return usernames;
    }

    public async Task<bool> HasRepositoriesAsync(
        string username, BasicCredential credential, CancellationToken cancellationToken)
    {
        var repositories = await api.GetAsync<List<GiteaRepository>>(
            $"users/{GiteaApi.Escape(username)}/repos?limit=1", credential, cancellationToken);

        return repositories is { Count: > 0 };
    }

    public async Task<AccountRemoval> DeleteUserAsync(
        string username, BasicCredential credential, CancellationToken cancellationToken)
    {
        if (!await api.ExistsAsync($"users/{GiteaApi.Escape(username)}", credential, cancellationToken))
        {
            logger.LogInformation("Gitea user {Username} is already gone", username);
            return AccountRemoval.AlreadyMissing;
        }

        // No ?purge=true. Purge deletes the user's repositories along with them, and a competitor's submission
        // history is the evidence a marking dispute is settled from. The caller refuses to delete a user that
        // still owns any, so there is nothing left to purge by the time this runs.
        await api.SendAsync(HttpMethod.Delete, $"admin/users/{GiteaApi.Escape(username)}", null, credential,
            $"delete the user '{username}'", cancellationToken);

        logger.LogInformation("Deleted Gitea user {Username}", username);
        return AccountRemoval.Removed;
    }

    internal async Task<GiteaRepository?> GetRepositoryAsync(
        RepositoryReference repository, BasicCredential credential, CancellationToken cancellationToken) =>
        await api.GetAsync<GiteaRepository>(
            $"repos/{GiteaApi.Escape(repository.Owner)}/{GiteaApi.Escape(repository.Name)}", credential, cancellationToken);
}
