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

    /// <summary>
    /// This competitor's stable position within the session, assigned once when they are enrolled.
    /// </summary>
    /// <remarks>
    /// Exists so a per-competitor service container can be given host ports of its own: the configured host
    /// port is a base and this is added to it, which is the only way N copies of one service can publish
    /// ports at all.
    /// <para>
    /// Persisted rather than computed from the competitor's position in a list, and never reused, because
    /// both things that would renumber a competitor are things that must not. A restart has to hand every
    /// competitor the port they have already written down, and enrolling or removing somebody a day later
    /// must leave everybody else's alone — a position in a sorted list gives neither.
    /// </para>
    /// </remarks>
    public int Ordinal { get; private set; }

    public SessionProvisionStatus ProvisionStatus { get; private set; }

    /// <summary>Why provisioning failed, surfaced to the admin so the cause is actionable.</summary>
    public string? ProvisionError { get; private set; }

    public DateTime? ProvisionedAt { get; private set; }

    public static SessionCompetitor Create(
        Guid sessionId, Guid competitorId, string repositoryName, int ordinal) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            CompetitorId = competitorId,
            RepositoryName = repositoryName.Trim(),
            Ordinal = ordinal,
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
