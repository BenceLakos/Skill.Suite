namespace Skill.Suite.Application.Competitors.Accounts;

/// <summary>A status plus the reason behind it, when the status alone does not explain itself.</summary>
internal sealed record AccountStatusEvaluation(ExternalAccountStatus Status, string? Detail);
