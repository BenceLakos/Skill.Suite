namespace Skill.Suite.Domain.Sessions;

public sealed record VolumeMount(string HostPath, string ContainerPath, bool ReadOnly);
