namespace Skill.Suite.Application.Tests.Sql;

using Skill.Suite.Infra.Sql;
using Xunit;

/// <summary>
/// Properties of the shared-database access T-SQL that are decisions rather than formatting.
/// </summary>
public sealed class MsSqlDatabaseAccessScriptsTests
{
    /// <summary>A name that would break out of a bracketed identifier if it were not escaped.</summary>
    private const string HostileName = "c01]; DROP DATABASE master --";

    /// <summary>Legal in SQL Server once bracketed, and the kind of thing an admin types into a session.</summary>
    private const string AwkwardName = "session db 'one' [2026]";

    [Fact]
    public void TheUserIsMappedToTheLoginOfTheSameName() =>
        // Same name on both sides is what lets the caller hand out one identifier per competitor: the login
        // they connect with is the user the role membership is attached to.
        Assert.Equal(
            "CREATE USER [c01] FOR LOGIN [c01];",
            MsSqlDatabaseAccessScripts.CreateDatabaseUser("c01"));

    [Fact]
    public void ReadAndWriteUseTheFixedDatabaseRoles()
    {
        Assert.Equal("db_datareader", MsSqlDatabaseAccessScripts.ReaderRole);
        Assert.Equal("db_datawriter", MsSqlDatabaseAccessScripts.WriterRole);
    }

    [Fact]
    public void GrantingReadAddsTheUserToTheReaderRole() =>
        Assert.Equal(
            "ALTER ROLE [db_datareader] ADD MEMBER [c01];",
            MsSqlDatabaseAccessScripts.AddRoleMember(MsSqlDatabaseAccessScripts.ReaderRole, "c01"));

    [Fact]
    public void RevokingReadDropsTheUserFromTheReaderRole() =>
        Assert.Equal(
            "ALTER ROLE [db_datareader] DROP MEMBER [c01];",
            MsSqlDatabaseAccessScripts.DropRoleMember(MsSqlDatabaseAccessScripts.ReaderRole, "c01"));

    [Fact]
    public void GrantingWriteAddsTheUserToTheWriterRole() =>
        Assert.Equal(
            "ALTER ROLE [db_datawriter] ADD MEMBER [c01];",
            MsSqlDatabaseAccessScripts.AddRoleMember(MsSqlDatabaseAccessScripts.WriterRole, "c01"));

    [Fact]
    public void RevokingWriteDropsTheUserFromTheWriterRole() =>
        Assert.Equal(
            "ALTER ROLE [db_datawriter] DROP MEMBER [c01];",
            MsSqlDatabaseAccessScripts.DropRoleMember(MsSqlDatabaseAccessScripts.WriterRole, "c01"));

    [Theory]
    [InlineData("c01")]
    [InlineData(AwkwardName)]
    [InlineData(HostileName)]
    public void EveryStatementEscapesTheNameTheSameWay(string name)
    {
        // The role statements take a name that reached the server as a login, but the drop variants are run
        // against whatever the admin typed, and one statement forgetting the escaping is all it takes.
        var quoted = TSql.QuoteName(name);

        foreach (var script in ScriptsFor(name))
            Assert.Contains(quoted, script, StringComparison.Ordinal);
    }

    [Fact]
    public void ABracketInTheNameCannotCloseTheIdentifier()
    {
        foreach (var script in ScriptsFor(HostileName))
            Assert.DoesNotContain($"[{HostileName}]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AQuoteInTheNameIsNotTurnedIntoALiteral() =>
        // Identifiers are bracketed, not quoted: a single quote inside one is an ordinary character and must
        // not be doubled, or the user created is not the user the login is called.
        Assert.Equal(
            $"CREATE USER [{AwkwardName.Replace("]", "]]")}] FOR LOGIN [{AwkwardName.Replace("]", "]]")}];",
            MsSqlDatabaseAccessScripts.CreateDatabaseUser(AwkwardName));

    [Theory]
    [InlineData("c01")]
    [InlineData(AwkwardName)]
    [InlineData(HostileName)]
    public void NoScriptContainsABatchSeparator(string name)
    {
        // GO is a client-side separator SqlCommand does not understand, and CREATE USER has to be the first
        // statement in its batch — which is why the caller opens the connection on the target database.
        foreach (var script in ScriptsFor(name))
            Assert.DoesNotContain("GO", script, StringComparison.Ordinal);
    }

    [Fact]
    public void NoScriptNamesTheDatabaseItRunsAgainst()
    {
        // Every statement is scoped to the current database. A USE would not help: CREATE USER may not share
        // a batch with it.
        foreach (var script in ScriptsFor("c01"))
            Assert.DoesNotContain("USE ", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheUserLookupIsParameterised()
    {
        Assert.Contains(
            MsSqlDatabaseAccessScripts.NameParameter,
            MsSqlDatabaseAccessScripts.DatabaseUserExists,
            StringComparison.Ordinal);

        Assert.Contains(
            "sys.database_principals",
            MsSqlDatabaseAccessScripts.DatabaseUserExists,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheMembershipLookupIsParameterisedOnBothTheUserAndTheRole()
    {
        // Membership is read before the ALTER ROLE so the outcome is the flags, not whatever the server
        // happened to say about a member that was already there.
        Assert.Contains(
            MsSqlDatabaseAccessScripts.NameParameter,
            MsSqlDatabaseAccessScripts.RoleMembershipExists,
            StringComparison.Ordinal);

        Assert.Contains(
            MsSqlDatabaseAccessScripts.RoleParameter,
            MsSqlDatabaseAccessScripts.RoleMembershipExists,
            StringComparison.Ordinal);

        Assert.Contains(
            "sys.database_role_members",
            MsSqlDatabaseAccessScripts.RoleMembershipExists,
            StringComparison.Ordinal);
    }

    private static IEnumerable<string> ScriptsFor(string name) =>
    [
        MsSqlDatabaseAccessScripts.CreateDatabaseUser(name),
        MsSqlDatabaseAccessScripts.AddRoleMember(MsSqlDatabaseAccessScripts.ReaderRole, name),
        MsSqlDatabaseAccessScripts.DropRoleMember(MsSqlDatabaseAccessScripts.ReaderRole, name),
        MsSqlDatabaseAccessScripts.AddRoleMember(MsSqlDatabaseAccessScripts.WriterRole, name),
        MsSqlDatabaseAccessScripts.DropRoleMember(MsSqlDatabaseAccessScripts.WriterRole, name),
    ];
}
