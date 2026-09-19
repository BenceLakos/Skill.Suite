namespace Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// What one side of a provision or remove request did. Reported per system, because the two are independent.
/// </summary>
public enum AccountActionOutcome
{
    Created,
    AlreadyExists,
    Removed,
    AlreadyMissing,

    /// <summary>Deliberately not attempted — no credential, or the account is still in use.</summary>
    Skipped,

    /// <summary>Attempted and the remote system refused or was unreachable.</summary>
    Failed,
}
