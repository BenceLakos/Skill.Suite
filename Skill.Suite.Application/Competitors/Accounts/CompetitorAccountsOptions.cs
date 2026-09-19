namespace Skill.Suite.Application.Competitors.Accounts;

public sealed class CompetitorAccountsOptions
{
    public const string SectionName = "CompetitorAccounts";

    /// <summary>
    /// Domain the competitor's git-host e-mail address is composed from, as <c>username@domain</c>.
    /// </summary>
    /// <remarks>
    /// The host requires a unique e-mail per user and never sends anything to it — a competition has no mail
    /// server — so this is an identifier, not an address. The default is deliberately a non-routable name so a
    /// misconfigured host cannot mail a real person. A leading <c>@</c> is tolerated and trimmed.
    /// </remarks>
    public string GiteaEmailDomain { get; set; } = "competitors.local";
}
