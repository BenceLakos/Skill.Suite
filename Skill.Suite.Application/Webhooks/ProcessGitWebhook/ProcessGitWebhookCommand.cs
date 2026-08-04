using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Webhooks.ProcessGitWebhook;

/// <summary>
/// An inbound push, already read off the wire but not yet trusted.
/// </summary>
/// <param name="Owner">Repository owner — the per-session organisation, which selects the session.</param>
/// <param name="RepositorySlug">Repository name without the owner — the competitor's username.</param>
/// <param name="RawBody">
/// The body exactly as received. Carried through rather than re-serialized because the signature covers
/// these bytes: any round-trip through a serializer invalidates it.
/// </param>
/// <param name="Signature">Value of <c>X-Gitea-Signature</c> or <c>X-Hub-Signature-256</c>, if present.</param>
public sealed record ProcessGitWebhookCommand(
    string RepositoryUrl,
    string? RepositoryName,
    string? Owner,
    string? RepositorySlug,
    string? Branch,
    string? CommitSha,
    byte[] RawBody,
    string? Signature) : IRequest<Result<TestRunAcceptedDto>>;
