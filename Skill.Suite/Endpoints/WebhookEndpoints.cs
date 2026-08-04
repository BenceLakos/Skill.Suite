using System.Text.Json;
using Mediator;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Application.Webhooks.ProcessGitWebhook;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Endpoints;

public static class WebhookEndpoints
{
    /// <summary>Largest push payload accepted. Gitea's is a few kilobytes; this is a sanity bound.</summary>
    private const int MaxBodyBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        // Still anonymous, because the git host cannot hold an application session. Authentication is the
        // HMAC over the body, checked in the handler against the secret that session's hook was installed
        // with — see ProcessGitWebhookHandler.VerifySignature.
        var group = app.MapGroup("/webhooks").AllowAnonymous();

        group.MapPost("/git", async (
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            // Read the bytes rather than letting the framework bind the body. The signature covers exactly
            // these bytes, and a model-bound object re-serialized for verification would never match.
            var raw = await ReadBodyAsync(http, ct);
            if (raw is null)
                return Results.BadRequest(new { error = $"Payload exceeds {MaxBodyBytes} bytes." });

            GitWebhookRequest? body;
            try
            {
                body = JsonSerializer.Deserialize<GitWebhookRequest>(raw, PayloadOptions);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = $"Payload is not valid JSON: {ex.Message}" });
            }

            if (body is null)
                return Results.BadRequest(new { error = "Payload was empty." });

            // Cheap rejections first, before anything touches the database.
            if (!body.IsBranchPush())
                return MapError(TestRunErrors.NotABranchPush);

            var repositoryUrl = body.ResolveRepositoryUrl();
            if (string.IsNullOrWhiteSpace(repositoryUrl))
                return Results.BadRequest(new { error = "Could not determine repository URL from payload." });

            var command = new ProcessGitWebhookCommand(
                repositoryUrl,
                body.ResolveRepositoryName(),
                body.ResolveOwner(),
                body.ResolveRepositorySlug(),
                body.ResolveBranch(),
                body.ResolveCommitSha(),
                raw,
                SignatureHeader(http));

            var result = await sender.Send(command, ct);
            if (result.IsFailure)
                return MapError(result.Error);

            var dto = result.Value;
            return Results.Accepted($"/api/test-runs/{dto.TestRunId}", dto);
        }).DisableAntiforgery();

        return app;
    }

    /// <summary>
    /// Reads at most <see cref="MaxBodyBytes"/> of the request body, or returns null if there is more.
    /// </summary>
    /// <remarks>
    /// Enforced <i>while</i> copying, not after. Buffering the whole body first and then comparing its length
    /// rejected nothing it had not already allocated — on an endpoint that is anonymous by necessity and reached
    /// before the signature is checked, so any client could make this process allocate up to Kestrel's own
    /// 30&#160;MB default per request. The declared 1&#160;MB bound has to be the real one.
    /// </remarks>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest http, CancellationToken cancellationToken)
    {
        // The advertised length is a fast path only; it is absent on a chunked request and a lie on a hostile
        // one, so the copy below is still bounded.
        if (http.ContentLength > MaxBodyBytes)
            return null;

        // One byte of headroom: reading MaxBodyBytes + 1 is what distinguishes "exactly at the limit" from
        // "over it" without allocating the overage.
        var buffer = new byte[MaxBodyBytes + 1];
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await http.Body.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0) break;
            total += read;
        }

        return total > MaxBodyBytes ? null : buffer[..total];
    }

    /// <summary>
    /// Gitea sends the same HMAC-SHA256 digest twice: bare hex in its own header, and <c>sha256=</c>-prefixed
    /// in the GitHub-compatible one. Either is accepted so a GitHub-style sender also works.
    /// </summary>
    private static string? SignatureHeader(HttpRequest http)
    {
        if (http.Headers.TryGetValue("X-Gitea-Signature", out var gitea) && !string.IsNullOrWhiteSpace(gitea))
            return gitea.ToString();

        return http.Headers.TryGetValue("X-Hub-Signature-256", out var hub) ? hub.ToString() : null;
    }

    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(error),
        ErrorType.Validation => Results.BadRequest(error),
        ErrorType.Conflict => Results.Conflict(error),
        _ => Results.Problem(detail: error.Message, statusCode: 500, title: error.Code),
    };
}
