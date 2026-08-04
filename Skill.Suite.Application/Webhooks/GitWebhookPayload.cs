namespace Skill.Suite.Application.Webhooks;

/// <summary>
/// Normalized git-webhook payload. We accept a small superset of fields so the same
/// endpoint can serve native git pushes and GitHub-style hooks. The endpoint extracts
/// these from the incoming JSON before handing off to the command.
/// </summary>
public sealed record GitWebhookPayload(
    string RepositoryUrl,
    string? RepositoryName,
    string? Branch,
    string? CommitSha);
