namespace Skill.Suite.Application.Sessions.StartSession;

using Skill.Suite.Domain.Competitors;

/// <summary>
/// Splits the competitor list against a snapshot of the account names an external system actually holds.
/// </summary>
/// <remarks>
/// Used for both external systems a session provisions against, because the failure mode is the same on each.
/// A competitor with no account on the git host cannot clone, push to, or even see a repository created for
/// them; a competitor with no SQL login cannot be granted anything on the session database, and asking the
/// server to grant it fails per competitor for a reason that has nothing to do with the session. Either way
/// the work is pointless, and doing it anyway reports a session as fully provisioned when it is not.
/// </remarks>
internal static class AccountAccessPartitioner
{
    /// <summary>
    /// Matches each competitor against <paramref name="accountNames"/>, the external system's own account list.
    /// </summary>
    /// <remarks>
    /// Case-insensitive, for the same reason the competitor account grid is: Gitea lower-cases account names
    /// and SQL Server logins compare case-insensitively under the usual collation, so a competitor stored as
    /// "C01" would otherwise never match the "c01" the system reports and would be skipped while holding a
    /// perfectly good account.
    /// </remarks>
    public static AccountAccessPartition Partition(
        IEnumerable<Competitor> competitors, IEnumerable<string> accountNames)
    {
        var accounts = accountNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var provisionable = new List<Competitor>();
        var skipped = new List<string>();

        foreach (var competitor in competitors)
        {
            if (accounts.Contains(competitor.Username))
                provisionable.Add(competitor);
            else
                skipped.Add(competitor.Username);
        }

        return new AccountAccessPartition(provisionable, skipped);
    }
}
