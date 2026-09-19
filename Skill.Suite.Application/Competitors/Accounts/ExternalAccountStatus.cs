namespace Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// Whether a competitor has an account on an external system.
/// </summary>
/// <remarks>
/// <see cref="Unknown"/> is a first-class answer, not an error: the remote system being unreachable must not
/// make the grid claim the account is missing, because the obvious next action on "missing" is to create it.
/// </remarks>
public enum ExternalAccountStatus
{
    Unknown = 0,
    Exists,
    Missing,
}
