namespace Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// Turns a snapshot of what a remote system contains into a per-competitor status.
/// </summary>
/// <remarks>
/// A null name set means the snapshot could not be taken, which is <see cref="ExternalAccountStatus.Unknown"/>
/// — never <see cref="ExternalAccountStatus.Missing"/>. The distinction is the whole point of this type: the
/// obvious reaction to "missing" is to press Provision, and doing that against a system nobody could reach is
/// how twenty competitors get told their account is broken when the network is.
/// </remarks>
internal static class AccountStatusEvaluator
{
    /// <summary>
    /// Shown when the login was created but its database was not — the state a half-finished provision, or a
    /// manual <c>DROP DATABASE</c>, leaves behind. Re-running Provision repairs it.
    /// </summary>
    internal const string LoginWithoutDatabase = "login exists, database missing";

    public static AccountStatusEvaluation Evaluate(
        string username, IReadOnlySet<string>? names, string? unavailableReason)
    {
        if (names is null)
            return new AccountStatusEvaluation(ExternalAccountStatus.Unknown, unavailableReason);

        return names.Contains(username)
            ? new AccountStatusEvaluation(ExternalAccountStatus.Exists, null)
            : new AccountStatusEvaluation(ExternalAccountStatus.Missing, null);
    }

    /// <summary>
    /// SQL Server needs both halves: a login on its own cannot be connected to, so it is not an account yet.
    /// </summary>
    public static AccountStatusEvaluation EvaluateMsSql(
        string username,
        IReadOnlySet<string>? logins,
        IReadOnlySet<string>? databases,
        string? unavailableReason)
    {
        if (logins is null || databases is null)
            return new AccountStatusEvaluation(ExternalAccountStatus.Unknown, unavailableReason);

        if (logins.Contains(username) && databases.Contains(username))
            return new AccountStatusEvaluation(ExternalAccountStatus.Exists, null);

        return logins.Contains(username)
            ? new AccountStatusEvaluation(ExternalAccountStatus.Missing, LoginWithoutDatabase)
            : new AccountStatusEvaluation(ExternalAccountStatus.Missing, null);
    }
}
