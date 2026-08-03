namespace Skill.Suite.Components.Pages.Sessions.Drafts;

public sealed class DockerImageDraft
{
    public string Image { get; set; } = string.Empty;
    public List<KeyValueDraft> Env { get; set; } = new();
    public List<KeyValueDraft> Labels { get; set; } = new();
    public List<VolumeDraft> Volumes { get; set; } = new();
    public List<PortMappingDraft> PortMappings { get; set; } = new();
}
