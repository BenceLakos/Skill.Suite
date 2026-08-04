namespace Skill.Suite.Domain.Sessions;

public sealed record PortMapping(int HostPort, int ContainerPort, PortProtocol Protocol);
