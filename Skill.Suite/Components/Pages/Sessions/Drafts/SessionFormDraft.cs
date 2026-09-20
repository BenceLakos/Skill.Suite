using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Components.Pages.Sessions.Drafts;

public sealed class SessionFormDraft
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartsAt { get; set; } = DateTime.UtcNow.Date;
    public DateTime EndsAt { get; set; } = DateTime.UtcNow.Date.AddDays(7);
    public SessionStatus Status { get; set; } = SessionStatus.Draft;

    public string? TemplateFolder { get; set; }
    public string? JudgementImage { get; set; }

    public string? DatabaseName { get; set; }
    public bool DatabaseReadAccess { get; set; }
    public bool DatabaseWriteAccess { get; set; }

    /// <summary>Path relative to the starter packages volume root, as the picker offers it.</summary>
    public string? DatabaseSeedScript { get; set; }

    public Guid? GitCredentialId { get; set; }
    public Guid? JudgementImagePullCredentialId { get; set; }

    public List<DockerImageDraft> DockerImages { get; set; } = new();
}
