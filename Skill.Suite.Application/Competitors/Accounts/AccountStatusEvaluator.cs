namespace Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// Turns a snapshot of what a remote system contains into a per-competitor status.
/// </summary>
/// <remarks>
/// A null name set means the snapshot could not be taken, which is <see cref="ExternalAccountStatus.Unknown"/>
/// — never <see cref="ExternalAccountStatus.Missing"/>. The distinction is the whole point of this type: the
/// obvious reaction to "missing" is to press Provision, and doing that against a system nobody could reach is
/// how twenty competitors get told their account is broken when the network is.
/// <para>
/// One rule serves both systems, because an account is one name on each: a git-host user, and a SQL Server
/// login. The databases a competitor works in are the sessions' and are reported by the session, not here.
/// </para>
/// </remarks>
internal static class AccountStatusEvaluator
{
    public static AccountStatusEvaluation Evaluate(
        string username, IReadOnlySet<string>? names, string? unavailableReason)
    {
        if (names is null)
            return new AccountStatusEvaluation(ExternalAccountStatus.Unknown, unavailableReason);

        return names.Contains(username)
            ? new AccountStatusEvaluation(ExternalAccountStatus.Exists, null)
            : new AccountStatusEvaluation(ExternalAccountStatus.Missing, null);
    }
}
