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
/// </remarks>
public sealed class AccountStatusEvaluatorTests
{
    private const string Unreachable = "the git host returned 502";

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
    public void SqlServerNeedsBothTheLoginAndTheDatabase()
    {
        var evaluation = AccountStatusEvaluator.EvaluateMsSql("c01", Names("c01"), Names("c01"), null);

        Assert.Equal(ExternalAccountStatus.Exists, evaluation.Status);
        Assert.Null(evaluation.Detail);
    }

    [Fact]
    public void ALoginWithoutItsDatabaseIsMissingAndSaysSo()
    {
        // The state a half-finished provision, or a manual DROP DATABASE, leaves behind. Reporting it as
        // Exists would hide an account the competitor cannot actually connect to.
        var evaluation = AccountStatusEvaluator.EvaluateMsSql("c01", Names("c01"), Names("master"), null);

        Assert.Equal(ExternalAccountStatus.Missing, evaluation.Status);
        Assert.Equal(AccountStatusEvaluator.LoginWithoutDatabase, evaluation.Detail);
    }

    [Fact]
    public void ADatabaseWithoutItsLoginIsPlainlyMissing()
    {
        var evaluation = AccountStatusEvaluator.EvaluateMsSql("c01", Names("sa"), Names("c01"), null);

        Assert.Equal(ExternalAccountStatus.Missing, evaluation.Status);
        Assert.Null(evaluation.Detail);
    }

    [Fact]
    public void SqlServerMatchingIgnoresCase() =>
        Assert.Equal(
            ExternalAccountStatus.Exists,
            AccountStatusEvaluator.EvaluateMsSql("C01", Names("c01"), Names("C01"), null).Status);

    [Fact]
    public void NoSqlServerSnapshotIsUnknown()
    {
        var evaluation = AccountStatusEvaluator.EvaluateMsSql("c01", null, null, Unreachable);

        Assert.Equal(ExternalAccountStatus.Unknown, evaluation.Status);
        Assert.Equal(Unreachable, evaluation.Detail);
    }

    [Fact]
    public void OneHalfOfTheSqlServerSnapshotMissingIsStillUnknown() =>
        // Half an inventory cannot distinguish an absent account from an unread one.
        Assert.Equal(
            ExternalAccountStatus.Unknown,
            AccountStatusEvaluator.EvaluateMsSql("c01", Names("c01"), null, Unreachable).Status);
}
