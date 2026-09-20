namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// What an idempotent "remove this account" or "remove this grant" call actually did.
/// </summary>
public enum AccountRemoval
{
    Removed,
    AlreadyMissing,
}
