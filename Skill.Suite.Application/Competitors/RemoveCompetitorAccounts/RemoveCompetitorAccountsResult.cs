namespace Skill.Suite.Application.Competitors.RemoveCompetitorAccounts;

using Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// What each system did. One side refusing or failing leaves the other's outcome intact and visible.
/// </summary>
public sealed record RemoveCompetitorAccountsResult(
    string Username,
    AccountActionResult Gitea,
    AccountActionResult MsSql);
