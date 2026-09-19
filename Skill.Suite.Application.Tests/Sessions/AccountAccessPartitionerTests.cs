namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.StartSession;
using Skill.Suite.Domain.Competitors;
using Xunit;

/// <summary>
/// Which competitors a provisioning stage is allowed to act on, given the accounts a system actually holds.
/// </summary>
/// <remarks>
/// The bug this covers: Start used to provision every competitor row in the database, whether or not they
/// had an account on the git host. Those competitors got a private repository they could not sign in to
/// see, while the session reported itself fully provisioned — the failure only surfaced once the
/// competition had started and they had nowhere to push. The same partition now guards the session
/// database, where the equivalent is a grant against a SQL login that does not exist.
/// </remarks>
public sealed class AccountAccessPartitionerTests
{
    private static Competitor Named(string username) =>
        Competitor.Create(username, $"{username} name", [], "10.0.0.1", "HU");

    [Fact]
    public void OnlyAccountHoldersAreProvisionable()
    {
        var partition = AccountAccessPartitioner.Partition(
            [Named("c01"), Named("c02"), Named("c03")],
            ["c01", "c03"]);

        Assert.Equal(["c01", "c03"], partition.Provisionable.Select(c => c.Username));
    }

    [Fact]
    public void CompetitorsWithoutAnAccountAreReportedAsSkipped()
    {
        var partition = AccountAccessPartitioner.Partition(
            [Named("c01"), Named("c02"), Named("c03")],
            ["c01", "c03"]);

        Assert.Equal(["c02"], partition.Skipped);
    }

    [Fact]
    public void MatchingIgnoresCase()
    {
        // Gitea lower-cases account names, so a competitor stored as "C01" holds the account the host
        // reports as "c01" and must not be skipped.
        var partition = AccountAccessPartitioner.Partition([Named("C01")], ["c01"]);

        Assert.Single(partition.Provisionable);
        Assert.Empty(partition.Skipped);
    }

    [Fact]
    public void EveryCompetitorIsSkippedWhenTheHostHasNoMatchingAccount()
    {
        // The handler turns this into a validation failure rather than starting a session in which nobody
        // can push.
        var partition = AccountAccessPartitioner.Partition(
            [Named("c01"), Named("c02")],
            ["admin", "judge"]);

        Assert.Empty(partition.Provisionable);
        Assert.Equal(["c01", "c02"], partition.Skipped);
    }

    [Fact]
    public void HostAccountsWithoutACompetitorAreIgnored()
    {
        // Administrators, service accounts and experts all live on the same host; none of them gets a
        // competition repository.
        var partition = AccountAccessPartitioner.Partition(
            [Named("c01")],
            ["admin", "expert-1", "c01"]);

        Assert.Equal(["c01"], partition.Provisionable.Select(c => c.Username));
        Assert.Empty(partition.Skipped);
    }

    [Fact]
    public void TheInputOrderIsPreservedInBothHalves()
    {
        // Start reads competitors ordered by username, and the skipped list is shown to the admin verbatim.
        var partition = AccountAccessPartitioner.Partition(
            [Named("c03"), Named("c01"), Named("c02")],
            ["c02"]);

        Assert.Equal(["c02"], partition.Provisionable.Select(c => c.Username));
        Assert.Equal(["c03", "c01"], partition.Skipped);
    }

    [Fact]
    public void AnEmptyAccountListSkipsEveryone()
    {
        // The SQL Server side of Start tolerates this — the session database is still created, and nobody is
        // granted anything — so the partitioner must answer it rather than treat it as a missing snapshot.
        var partition = AccountAccessPartitioner.Partition([Named("c01"), Named("c02")], []);

        Assert.Empty(partition.Provisionable);
        Assert.Equal(["c01", "c02"], partition.Skipped);
    }
}
