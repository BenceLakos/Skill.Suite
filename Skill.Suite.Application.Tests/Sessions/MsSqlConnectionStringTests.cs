namespace Skill.Suite.Application.Tests.Sessions;

using Skill.Suite.Application.Sessions.MySession;
using Xunit;

/// <summary>
/// The connection string a competitor copies off their own page.
/// </summary>
/// <remarks>
/// The quoting is the reason this is tested rather than eyeballed. Competitor passwords are generated
/// passphrases, and an unquoted one containing a semicolon does not fail — it silently truncates the string
/// into one that connects with no password, which a competitor would spend competition time on.
/// </remarks>
public sealed class MsSqlConnectionStringTests
{
    private const string Server = "sql.skills.local,1433";
    private const string Database = "c01";
    private const string Login = "c01";

    [Fact]
    public void EveryKeywordIsPresentAndQuoted()
    {
        var connection = MsSqlConnectionString.For(Server, Database, Login, "passphrase");

        Assert.Equal(
            "Server='sql.skills.local,1433';Database='c01';User ID='c01';Password='passphrase';"
            + "TrustServerCertificate=True",
            connection);
    }

    [Fact]
    public void ASemicolonInThePasswordStaysInsideTheValue()
    {
        var connection = MsSqlConnectionString.For(Server, Database, Login, "pass;word");

        Assert.Contains("Password='pass;word';", connection);
    }

    [Fact]
    public void ASingleQuoteInThePasswordIsDoubled()
    {
        var connection = MsSqlConnectionString.For(Server, Database, Login, "pass'word");

        Assert.Contains("Password='pass''word';", connection);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NoServerMeansNoConnectionString(string? server)
    {
        // Half a connection string is worse than none: it is something a competitor pastes and watches fail.
        Assert.Null(MsSqlConnectionString.For(server, Database, Login, "passphrase"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NoDatabaseMeansNoConnectionString(string? database)
    {
        Assert.Null(MsSqlConnectionString.For(Server, database, Login, "passphrase"));
    }
}
