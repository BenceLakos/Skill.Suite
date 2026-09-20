namespace Skill.Suite.Application.Tests.Competitors;

using Skill.Suite.Application.Competitors.Accounts;
using Xunit;

/// <summary>
/// How a snapshot of a remote system becomes a per-competitor status.
/// </summary>
/// <remarks>
/// The distinction that matters here is Unknown versus Missing. The obvious reaction to "no account" is to
/// press Provision, so reporting an unreachable system as Missing would have an admin creating accounts that
/// already exist against a server nobody can talk to.
/// <para>
/// One rule covers both systems: an account is a git-host user on one side and a SQL Server login on the
/// other, and nothing else on either. The SQL cases below are kept separate because that server is the one
/// whose account used to mean more than a name in a list.
/// </para>
/// </remarks>
public sealed class AccountStatusEvaluatorTests
{
    private const string Unreachable = "the git host returned 502";

    private const string SqlUnreachable = "the SQL Server is restarting";

    private static IReadOnlySet<string> Names(params string[] names) =>
        names.ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void APresentNameExists()
    {
        var evaluation = AccountStatusEvaluator.Evaluate("c01", Names("c01", "c02"), null);

        Assert.Equal(ExternalAccountStatus.Exists, evaluation.Status);
        Assert.Null(evaluation.Detail);
    }

    [Fact]
    public void AnAbsentNameIsMissing()
    {
        var evaluation = AccountStatusEvaluator.Evaluate("c03", Names("c01", "c02"), null);

        Assert.Equal(ExternalAccountStatus.Missing, evaluation.Status);
        Assert.Null(evaluation.Detail);
    }

    [Fact]
    public void MatchingIgnoresCase() =>
        // Gitea lower-cases account names, so a competitor stored as "C01" must still match "c01".
        Assert.Equal(
            ExternalAccountStatus.Exists,
            AccountStatusEvaluator.Evaluate("C01", Names("c01"), null).Status);

    [Fact]
    public void NoSnapshotIsUnknownWithTheReasonAttached()
    {
        var evaluation = AccountStatusEvaluator.Evaluate("c01", null, Unreachable);

        Assert.Equal(ExternalAccountStatus.Unknown, evaluation.Status);
        Assert.Equal(Unreachable, evaluation.Detail);
    }

    [Fact]
    public void ASqlServerLoginOnItsOwnIsTheWholeAccount()
    {
        // No database of the competitor's is looked for, because there is none to look for: the databases on
        // the server belong to sessions and are created when a session starts.
        var evaluation = AccountStatusEvaluator.Evaluate("c01", Names("sa", "c01"), null);

        Assert.Equal(ExternalAccountStatus.Exists, evaluation.Status);
        Assert.Null(evaluation.Detail);
    }

    [Fact]
    public void ACompetitorWithNoSqlServerLoginIsMissing()
    {
        var evaluation = AccountStatusEvaluator.Evaluate("c01", Names("sa"), null);

        Assert.Equal(ExternalAccountStatus.Missing, evaluation.Status);
        Assert.Null(evaluation.Detail);
    }

    [Fact]
    public void AnUnreadableSqlServerIsUnknownRatherThanMissing()
    {
        // The login list is null because the inventory query failed, not because the server holds no logins.
        // Missing here would have an admin re-provisioning twenty accounts that are already there.
        var evaluation = AccountStatusEvaluator.Evaluate("c01", null, SqlUnreachable);

        Assert.Equal(ExternalAccountStatus.Unknown, evaluation.Status);
        Assert.Equal(SqlUnreachable, evaluation.Detail);
    }
}
