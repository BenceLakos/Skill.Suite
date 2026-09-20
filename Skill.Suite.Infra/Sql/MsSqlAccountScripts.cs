namespace Skill.Suite.Infra.Sql;

/// <summary>
/// The T-SQL a competitor's login, and the databases a session creates for them, are managed with.
/// </summary>
/// <remarks>
/// Every method returns exactly ONE batch and never contains <c>GO</c>. <c>GO</c> is a client-side separator
/// that <c>sqlcmd</c> understands and <c>SqlCommand</c> does not — sending it is a syntax error. More
/// importantly <c>CREATE DATABASE</c> may not share a batch with other statements, so each statement is
/// executed as its own <c>SqlCommand</c> rather than concatenated into a script.
/// </remarks>
internal static class MsSqlAccountScripts
{
    /// <summary>The single parameter every parameterised script here takes.</summary>
    internal const string NameParameter = "@name";

    /// <summary>1 when a SQL login with that name exists.</summary>
    public const string LoginExists =
        $"SELECT 1 FROM sys.server_principals WHERE name = {NameParameter} AND type = 'S';";

    /// <summary>1 when a database with that name exists.</summary>
    public const string DatabaseExists =
        $"SELECT 1 FROM sys.databases WHERE name = {NameParameter};";

    /// <summary>
    /// Every SQL login, then every database — two result sets in one round trip.
    /// </summary>
    /// <remarks>
    /// <c>type = 'S'</c> keeps Windows principals, certificates and server roles out: the page only speaks
    /// about logins it could itself have created.
    /// </remarks>
    public const string Inventory =
        "SELECT name FROM sys.server_principals WHERE type = 'S'; " +
        "SELECT name FROM sys.databases;";

    /// <summary>
    /// Sessions other than this one that belong to the login.
    /// </summary>
    /// <remarks>
    /// The login and nothing else: what removal drops is the login, so a connection somebody else holds to a
    /// session database is not a reason to refuse — that database is staying either way.
    /// <para>
    /// <c>session_id &lt;&gt; @@SPID</c> excludes the connection asking the question, which is itself a
    /// session and would otherwise never let the count reach zero once the admin login matched.
    /// </para>
    /// </remarks>
    public const string ActiveConnectionCount =
        "SELECT COUNT(*) FROM sys.dm_exec_sessions " +
        $"WHERE session_id <> @@SPID AND login_name = {NameParameter};";

    /// <summary>
    /// Creates the login.
    /// </summary>
    /// <remarks>
    /// <c>CHECK_POLICY = OFF</c> is deliberate. Competitor passwords are generated passphrases — long, but
    /// often without the digit-and-symbol mix the Windows policy demands — and with the policy on, the server
    /// rejects exactly the passwords this platform hands out. It also switches off expiry and lockout, which a
    /// competition login must not have: a competitor locked out mid-session loses the time.
    /// </remarks>
    public static string CreateLogin(string name, string password) =>
        $"CREATE LOGIN {TSql.QuoteName(name)} WITH PASSWORD = {TSql.Literal(password)}, CHECK_POLICY = OFF;";

    public static string CreateDatabase(string name) =>
        $"CREATE DATABASE {TSql.QuoteName(name)};";

    public static string DropLogin(string name) =>
        $"DROP LOGIN {TSql.QuoteName(name)};";
}
