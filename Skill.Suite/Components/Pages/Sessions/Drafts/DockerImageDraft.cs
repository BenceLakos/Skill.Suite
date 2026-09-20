namespace Skill.Suite.Components.Pages.Sessions.Drafts;

public sealed class DockerImageDraft
{
    public string Image { get; set; } = string.Empty;

    /// <summary>Hostname the reverse proxy routes to this service, or empty for a port-only service.</summary>
    public string? Domain { get; set; }

    /// <summary>
    /// The port the application listens on inside the container, which the proxy forwards to. Only
    /// meaningful with a <see cref="Domain"/>, and cleared with it.
    /// </summary>
    public int? RoutedPort { get; set; }
    public List<KeyValueDraft> Env { get; set; } = new();
    public List<KeyValueDraft> Labels { get; set; } = new();
    public List<VolumeDraft> Volumes { get; set; } = new();
    public List<PortMappingDraft> PortMappings { get; set; } = new();
}
