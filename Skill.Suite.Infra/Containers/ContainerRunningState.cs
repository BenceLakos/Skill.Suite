namespace Skill.Suite.Infra.Containers;

/// <summary>What the daemon knows about a container with a given name.</summary>
internal enum ContainerRunningState
{
    /// <summary>No container by that name exists.</summary>
    Absent = 0,

    /// <summary>A container by that name exists but is not running, so it is a leftover to be replaced.</summary>
    Stopped = 1,

    Running = 2,
}
