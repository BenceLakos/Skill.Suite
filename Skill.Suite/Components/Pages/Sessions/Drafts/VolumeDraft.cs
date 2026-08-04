namespace Skill.Suite.Components.Pages.Sessions.Drafts;

public sealed class VolumeDraft
{
    public string HostPath { get; set; } = string.Empty;
    public string ContainerPath { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
}
