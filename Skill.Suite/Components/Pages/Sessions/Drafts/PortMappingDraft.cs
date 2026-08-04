using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Components.Pages.Sessions.Drafts;

public sealed class PortMappingDraft
{
    public int HostPort { get; set; } = 8080;
    public int ContainerPort { get; set; } = 8080;
    public PortProtocol Protocol { get; set; } = PortProtocol.Tcp;
}
