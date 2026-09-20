namespace Skill.Suite.Application.Sessions.MySession;

using System.Text;

/// <summary>
/// A ready-to-paste ADO.NET connection string for one competitor's SQL Server login.
/// </summary>
/// <remarks>
/// Built by hand rather than with <c>SqlConnectionStringBuilder</c>, which lives in
/// <c>Microsoft.Data.SqlClient</c> — an Infrastructure dependency, while this is a display value the
/// Application layer produces. The quoting is the rule ADO.NET itself applies: a value is wrapped in single
/// quotes and any single quote inside it is doubled. That matters because competitor passwords are generated
/// passphrases, and one <c>;</c> in an unquoted value silently truncates the connection string into something
/// that connects somewhere else.
/// <para>
/// <c>TrustServerCertificate=True</c> is not optional: the SQL Server image presents a self-signed
/// certificate, and a competition LAN has no authority that could have signed a real one.
/// </para>
/// </remarks>
internal static class MsSqlConnectionString
{
    private const string ServerKeyword = "Server";
    private const string DatabaseKeyword = "Database";
    private const string UserKeyword = "User ID";
    private const string PasswordKeyword = "Password";
    private const string TrustServerCertificate = "TrustServerCertificate=True";

    private const string Quote = "'";
    private const string EscapedQuote = "''";
    private const char Assignment = '=';
    private const char KeywordSeparator = ';';

    /// <summary>
    /// The connection string, or null when there is nothing honest to print.
    /// </summary>
    /// <remarks>
    /// Null rather than a partial string on purpose: a connection string missing its server or its database is
    /// something a competitor would paste, watch fail, and spend competition time on.
    /// </remarks>
    public static string? For(string? server, string? database, string login, string password)
    {
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
            return null;

        var builder = new StringBuilder();

        Append(builder, ServerKeyword, server);
        Append(builder, DatabaseKeyword, database);
        Append(builder, UserKeyword, login);
        Append(builder, PasswordKeyword, password);

        return builder.Append(TrustServerCertificate).ToString();
    }

    private static void Append(StringBuilder builder, string keyword, string value) =>
        builder
            .Append(keyword)
            .Append(Assignment)
            .Append(Quote)
            .Append(value.Replace(Quote, EscapedQuote, StringComparison.Ordinal))
            .Append(Quote)
            .Append(KeywordSeparator);
}
