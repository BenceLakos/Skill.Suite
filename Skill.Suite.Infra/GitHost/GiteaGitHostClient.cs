using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    /// <summary>
    /// Gitea's API is snake_case throughout, so the naming policy has to be too.
    /// </summary>
    /// <remarks>
    /// This is not cosmetic. Under the Web defaults' camelCase, <c>clone_url</c>, <c>full_name</c> and
    /// <c>default_branch</c> all deserialize to null while single-word members like <c>empty</c> keep working
    /// — so the content check passed and the clone URL silently vanished, failing every competitor with
    /// "reported no clone URL" after their repository had already been created.
    /// <para>
    /// Request bodies are built as dictionaries with literal snake_case keys. Those are unaffected: a
    /// property naming policy does not rewrite dictionary keys, <see cref="JsonSerializerOptions.DictionaryKeyPolicy"/>
    /// does, and it is deliberately left unset.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>How long to wait for a freshly pushed repository to report content.</summary>
    private static readonly TimeSpan ContentPollInterval = TimeSpan.FromMilliseconds(500);

    public async Task EnsureOrganizationAsync(
        EnsureOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (await ExistsAsync($"orgs/{Escape(request.Name)}", request.Credential, cancellationToken))
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

        await SendAsync(HttpMethod.Post, "orgs", body, request.Credential,
            $"create organisation '{request.Name}'", cancellationToken);

        logger.LogInformation("Created Gitea organisation {Org}", request.Name);
    }

    public async Task EnsureTemplateRepositoryAsync(
        EnsureRepositoryRequest request, CancellationToken cancellationToken)
    {
        var (owner, name) = (request.Repository.Owner, request.Repository.Name);

        if (await ExistsAsync($"repos/{Escape(owner)}/{Escape(name)}", request.Credential, cancellationToken))
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

        await SendAsync(HttpMethod.Post, $"orgs/{Escape(owner)}/repos", body, request.Credential,
            $"create template repository '{owner}/{name}'", cancellationToken);

        logger.LogInformation("Created template repository {Owner}/{Name}", owner, name);
    }

    public async Task<string> GenerateRepositoryFromTemplateAsync(
        GenerateRepositoryRequest request, CancellationToken cancellationToken)
    {
        var (owner, name) = (request.Target.Owner, request.Target.Name);
        var path = $"repos/{Escape(request.Template.Owner)}/{Escape(request.Template.Name)}/generate";

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

            using var response = await SendAsync(HttpMethod.Post, path, body, request.Credential,
                $"generate repository '{owner}/{name}' from template", cancellationToken);

            existing = await response.Content.ReadFromJsonAsync<GiteaRepository>(Json, cancellationToken);
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
        var path = $"orgs/{Escape(request.Organization)}/hooks";

        // Delete before creating rather than editing in place: the secret is regenerated on every Start, so
        // a hook left from an earlier attempt would keep delivering with a key nothing verifies against.
        var existing = await GetAsync<List<GiteaHook>>(path, request.Credential, cancellationToken) ?? [];
        foreach (var hook in existing.Where(h => h.Config is not null
                                                 && h.Config.TryGetValue("url", out var url)
                                                 && string.Equals(url, request.TargetUrl, StringComparison.OrdinalIgnoreCase)))
        {
            await SendAsync(HttpMethod.Delete, $"{path}/{hook.Id}", null, request.Credential,
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

        await SendAsync(HttpMethod.Post, path, body, request.Credential,
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

    internal async Task<GiteaRepository?> GetRepositoryAsync(
        RepositoryReference repository, BasicCredential credential, CancellationToken cancellationToken) =>
        await GetAsync<GiteaRepository>(
            $"repos/{Escape(repository.Owner)}/{Escape(repository.Name)}", credential, cancellationToken);

    // ------------------------------------------------------------------ plumbing

    private async Task<bool> ExistsAsync(
        string path, BasicCredential credential, CancellationToken cancellationToken)
    {
        using var request = Build(HttpMethod.Get, path, null, credential);
        using var response = await http.SendAsync(request, cancellationToken);

        return response.StatusCode != HttpStatusCode.NotFound;
    }

    private async Task<T?> GetAsync<T>(
        string path, BasicCredential credential, CancellationToken cancellationToken)
    {
        using var request = Build(HttpMethod.Get, path, null, credential);
        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        await ThrowIfFailedAsync(response, $"GET {path}", cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        Dictionary<string, object?>? body,
        BasicCredential credential,
        string what,
        CancellationToken cancellationToken)
    {
        using var request = Build(method, path, body, credential);
        var response = await http.SendAsync(request, cancellationToken);

        await ThrowIfFailedAsync(response, what, cancellationToken);
        return response;
    }

    private static HttpRequestMessage Build(
        HttpMethod method, string path, Dictionary<string, object?>? body, BasicCredential credential)
    {
        var request = new HttpRequestMessage(method, path);

        // Basic auth with the admin's token as the password is what the Gitea API accepts for both a real
        // password and a personal access token, so one credential shape covers either.
        var raw = $"{credential.Username}:{credential.Secret}";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));

        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        return request;
    }

    private static async Task ThrowIfFailedAsync(
        HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 500) detail = detail[..500];

        throw new GitHostException(
            $"Could not {what}: the git host returned {(int)response.StatusCode} " +
            $"{response.ReasonPhrase}. {detail}");
    }

    private static string Escape(string segment) => Uri.EscapeDataString(segment);
}
