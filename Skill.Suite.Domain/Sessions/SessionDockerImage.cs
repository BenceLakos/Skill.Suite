namespace Skill.Suite.Domain.Sessions;

public sealed record SessionDockerImage(
    string Image,
    Dictionary<string, string> Env,
    Dictionary<string, string> Labels,
    List<VolumeMount> Volumes,
    List<PortMapping> PortMappings);
