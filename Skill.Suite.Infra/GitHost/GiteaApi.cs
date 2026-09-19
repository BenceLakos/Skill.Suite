namespace Skill.Suite.Infra.GitHost;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Skill.Suite.Application.Abstractions;

/// <summary>
/// The HTTP plumbing shared by every client that speaks the Gitea REST API: authentication, serialization
/// and the failure-to-exception mapping.
/// </summary>
/// <remarks>
/// Extracted so a second client can be added without a second copy of rules that were each learned from a
/// production failure — the snake_case policy below and the "only 404 means absent" distinction in
/// <see cref="ExistsAsync"/> in particular. It holds no state beyond the <see cref="HttpClient"/>, whose
/// <c>BaseAddress</c> carries the <c>/api/v1/</c> prefix, so every path here is relative to that.
/// </remarks>
internal sealed class GiteaApi(HttpClient http)
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
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Whether the resource at <paramref name="path"/> exists.
    /// </summary>
    /// <remarks>
    /// Only a success status means "exists" and only 404 means "does not". Anything else — 401 from a rotated
    /// admin token, 403, a 500 from the host — is neither, and treating it as existence was actively misleading:
    /// with a bad credential the organisation and template checks both reported "already exists", nothing was
    /// created, and provisioning then failed per competitor with a message about empty templates while the log
    /// asserted the opposite. Throwing here names the real cause at the point it is first observable.
    /// </remarks>
    public async Task<bool> ExistsAsync(
        string path, BasicCredential credential, CancellationToken cancellationToken)
    {
        using var request = Build(HttpMethod.Get, path, null, credential);
        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await ThrowIfFailedAsync(response, $"check whether '{path}' exists", cancellationToken);
        return true;
    }

    public async Task<T?> GetAsync<T>(
        string path, BasicCredential credential, CancellationToken cancellationToken)
    {
        using var request = Build(HttpMethod.Get, path, null, credential);
        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        await ThrowIfFailedAsync(response, $"GET {path}", cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    public async Task<HttpResponseMessage> SendAsync(
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

    internal static string Escape(string segment) => Uri.EscapeDataString(segment);
}
