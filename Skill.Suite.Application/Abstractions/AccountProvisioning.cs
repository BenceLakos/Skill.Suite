namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// What an idempotent "ensure this account exists" call actually did.
/// </summary>
/// <remarks>
/// Provisioning is restartable by design, so "it was already there" is a success — but the admin pressing the
/// button still wants to know which of the two happened, because only one of them is a change to the world.
/// </remarks>
public enum AccountProvisioning
{
    Created,
    AlreadyExisted,
}
