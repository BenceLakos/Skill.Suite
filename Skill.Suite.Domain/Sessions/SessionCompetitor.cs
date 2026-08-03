using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Sessions;

/// <summary>
/// One competitor's repository in a session: the mapping a push is resolved through.
/// </summary>
/// <remarks>
/// This row is what makes attribution deterministic. Without it the webhook has to guess the competitor from
/// the repository name, which means a competitor called <c>ali</c> can be credited with a push to
/// <c>alice</c>'s repository. Provisioning writes one row per competitor and the webhook reads it — the
/// repository name is data, not a heuristic.
/// </remarks>
public sealed class SessionCompetitor : Entity<Guid>
{
    private SessionCompetitor() { }

    public Guid SessionId { get; private set; }
    public Guid CompetitorId { get; private set; }

    /// <summary>Repository name within the session's organisation — the competitor's username.</summary>
    public string RepositoryName { get; private set; } = string.Empty;

    /// <summary>Clone URL as the git host reports it, for display and for the competitor to copy.</summary>
    public string? RepositoryUrl { get; private set; }

    public SessionProvisionStatus ProvisionStatus { get; private set; }

    /// <summary>Why provisioning failed, surfaced to the admin so the cause is actionable.</summary>
    public string? ProvisionError { get; private set; }

    public DateTime? ProvisionedAt { get; private set; }

    public static SessionCompetitor Create(Guid sessionId, Guid competitorId, string repositoryName) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            CompetitorId = competitorId,
            RepositoryName = repositoryName.Trim(),
            ProvisionStatus = SessionProvisionStatus.Pending,
        };

    public void MarkProvisioned(string repositoryUrl)
    {
        RepositoryUrl = repositoryUrl;
        ProvisionStatus = SessionProvisionStatus.Provisioned;
        ProvisionError = null;
        ProvisionedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        ProvisionStatus = SessionProvisionStatus.Failed;
        // Truncated to the column width: the message comes from a git-host response body, which can be long.
        ProvisionError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        ProvisionedAt = null;
    }

    /// <summary>Reset to Pending so a retry re-attempts this competitor without recreating the row.</summary>
    public void MarkPending()
    {
        ProvisionStatus = SessionProvisionStatus.Pending;
        ProvisionError = null;
        ProvisionedAt = null;
    }

    public const int MaxErrorLength = 1000;
}
