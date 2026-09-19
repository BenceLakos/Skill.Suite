namespace Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// Composes the synthetic e-mail address the git host insists every user has.
/// </summary>
internal static class CompetitorEmail
{
    private const char Separator = '@';

    public static string For(string username, string domain) =>
        $"{username.Trim().ToLowerInvariant()}{Separator}{domain.Trim().TrimStart(Separator).ToLowerInvariant()}";
}
