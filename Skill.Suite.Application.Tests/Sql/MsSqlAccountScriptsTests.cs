namespace Skill.Suite.Application.Tests.Sql;

using Skill.Suite.Infra.Sql;
using Xunit;

/// <summary>
/// Properties of the competitor-account T-SQL that are decisions rather than formatting.
/// </summary>
public sealed class MsSqlAccountScriptsTests
{
    /// <summary>A name that would break out of a bracketed identifier if it were not escaped.</summary>
    private const string HostileName = "c01]; DROP DATABASE master --";

    [Fact]
    public void CreateLoginTurnsThePasswordPolicyOff() =>
        // Competitor passwords are generated passphrases. With the policy on, the server rejects exactly the
        // passwords this platform hands out — and expiry and lockout would apply to a competition login.
        Assert.Contains(
            "CHECK_POLICY = OFF",
            MsSqlAccountScripts.CreateLogin("c01", "correct horse battery staple"),
            StringComparison.Ordinal);

    [Fact]
    public void CreateLoginEscapesThePassword() =>
        Assert.Contains(
            "'it''s a passphrase'",
            MsSqlAccountScripts.CreateLogin("c01", "it's a passphrase"),
            StringComparison.Ordinal);

    [Theory]
    [InlineData("c01")]
    [InlineData(HostileName)]
    public void NoScriptContainsABatchSeparator(string name)
    {
        // GO is a client-side separator sqlcmd understands and SqlCommand does not: sending it is a syntax
        // error. Each statement is executed as its own command instead.
        foreach (var script in ScriptsFor(name))
            Assert.DoesNotContain("GO", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DroppingADatabaseDoesNotEvictWhoeverIsConnected()
    {
        // SET SINGLE_USER WITH ROLLBACK IMMEDIATE would kill live connections and drop the database anyway,
        // defeating the active-connection check that makes removal refuse while a competitor is working.
        var script = MsSqlAccountScripts.DropDatabase("c01");

        Assert.DoesNotContain("SINGLE_USER", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ROLLBACK", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryStatementEscapesTheNameTheSameWay()
    {
        // One statement forgetting the escaping is all it takes, and the drop statements are the ones where
        // that is unrecoverable.
        var quoted = TSql.QuoteName(HostileName);

        foreach (var script in ScriptsFor(HostileName))
        {
            Assert.Contains(quoted, script, StringComparison.Ordinal);
            Assert.DoesNotContain($"[{HostileName}]", script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OwnershipIsGrantedOnTheDatabaseToTheLoginOfTheSameName() =>
        Assert.Equal(
            "ALTER AUTHORIZATION ON DATABASE::[c01] TO [c01];",
            MsSqlAccountScripts.GrantDatabaseOwnership("c01"));

    [Theory]
    [InlineData(MsSqlAccountScripts.LoginExists)]
    [InlineData(MsSqlAccountScripts.DatabaseExists)]
    [InlineData(MsSqlAccountScripts.ActiveConnectionCount)]
    public void TheLookupScriptsAreParameterised(string script) =>
        // These take a name the caller did not validate, and unlike the DDL above they can be parameterised —
        // so they must be.
        Assert.Contains(MsSqlAccountScripts.NameParameter, script, StringComparison.Ordinal);

    [Fact]
    public void TheConnectionCountIgnoresTheConnectionAskingTheQuestion() =>
        // The admin connection is itself a session; counting it would never let the count reach zero.
        Assert.Contains("@@SPID", MsSqlAccountScripts.ActiveConnectionCount, StringComparison.Ordinal);

    [Fact]
    public void TheInventoryReadsBothLoginsAndDatabases()
    {
        Assert.Contains("sys.server_principals", MsSqlAccountScripts.Inventory, StringComparison.Ordinal);
        Assert.Contains("sys.databases", MsSqlAccountScripts.Inventory, StringComparison.Ordinal);
    }

    private static IEnumerable<string> ScriptsFor(string name) =>
    [
        MsSqlAccountScripts.CreateLogin(name, "passphrase"),
        MsSqlAccountScripts.CreateDatabase(name),
        MsSqlAccountScripts.GrantDatabaseOwnership(name),
        MsSqlAccountScripts.SetDefaultDatabase(name),
        MsSqlAccountScripts.DropDatabase(name),
        MsSqlAccountScripts.DropLogin(name),
    ];
}
