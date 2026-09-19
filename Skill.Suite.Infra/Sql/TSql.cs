namespace Skill.Suite.Infra.Sql;

/// <summary>
/// Escapes identifiers and literals for the handful of T-SQL statements that cannot be parameterised.
/// </summary>
/// <remarks>
/// <c>CREATE LOGIN</c>, <c>CREATE DATABASE</c>, <c>ALTER AUTHORIZATION</c> and <c>DROP</c> take no parameters —
/// the name and the password have to be text in the batch. That makes this the one place in the application
/// where a competitor-controlled string is concatenated into SQL, so the escaping lives here on its own, is
/// tested on its own, and is never open-coded at a call site.
/// <para>
/// Escaping is not the only guard: the username the caller passes has already been through
/// <c>CompetitorRules.UsernamePattern</c>, which admits only letters, digits, dot, underscore and hyphen. The
/// password has not, and cannot be — it is a passphrase an admin chose.
/// </para>
/// </remarks>
internal static class TSql
{
    /// <summary>SQL Server's own limit on a regular identifier, in characters.</summary>
    internal const int MaxIdentifierLength = 128;

    /// <summary>
    /// Wraps an identifier in brackets, doubling any closing bracket inside it.
    /// </summary>
    /// <remarks>
    /// Doubling <c>]</c> is what keeps the identifier one identifier: without it <c>a]; DROP DATABASE x --</c>
    /// closes the bracket early and the rest of the name becomes statements.
    /// </remarks>
    public static string QuoteName(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("An identifier cannot be empty.", nameof(identifier));

        if (identifier.Length > MaxIdentifierLength)
        {
            throw new ArgumentException(
                $"An identifier cannot be longer than {MaxIdentifierLength} characters.", nameof(identifier));
        }

        return $"[{identifier.Replace("]", "]]")}]";
    }

    /// <summary>Wraps a value in single quotes, doubling any single quote inside it.</summary>
    public static string Literal(string value) => $"'{value.Replace("'", "''")}'";
}
