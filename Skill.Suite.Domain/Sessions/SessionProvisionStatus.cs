namespace Skill.Suite.Domain.Sessions;

/// <summary>
/// Provisioning state of one competitor's repository within a session.
/// </summary>
/// <remarks>
/// Tracked per competitor rather than per session because provisioning is N independent git-host
/// conversations: one competitor's repository failing must not deny the other twenty theirs, and an expert
/// needs to see which competitor to fix rather than "provisioning failed".
/// </remarks>
public enum SessionProvisionStatus
{
    Pending = 0,
    Provisioned = 1,
    Failed = 2,
}
