namespace Skill.Suite.Application.Competitors.ProvisionCompetitorAccounts;

using Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// What each system did. Reported separately because the two are provisioned independently and one of them
/// failing says nothing about the other.
/// </summary>
public sealed record ProvisionCompetitorAccountsResult(
    string Username,
    AccountActionResult Gitea,
    AccountActionResult MsSql);
