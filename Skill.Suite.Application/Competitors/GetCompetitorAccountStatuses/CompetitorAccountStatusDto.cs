namespace Skill.Suite.Application.Competitors.GetCompetitorAccountStatuses;

using Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// One competitor's account status on each external system, as the grid shows it.
/// </summary>
/// <param name="GiteaDetail">Why the status is what it is, when it needs saying. Already trimmed for display.</param>
public sealed record CompetitorAccountStatusDto(
    Guid CompetitorId,
    string Username,
    ExternalAccountStatus Gitea,
    string? GiteaDetail,
    ExternalAccountStatus MsSql,
    string? MsSqlDetail);
