namespace Skill.Suite.Infra.Sql;

/// <summary>
/// The T-SQL a login's access to its session database is managed with.
/// </summary>
/// <remarks>
/// Same rules as <see cref="MsSqlAccountScripts"/>: one batch per method and never a <c>GO</c>. Nothing here
/// names the database either — every statement is run on a connection already opened against it, because
/// <c>CREATE USER</c> has to be the first statement in its batch and so cannot be prefixed with a <c>USE</c>.
/// </remarks>
internal static class MsSqlDatabaseAccessScripts
{
    /// <summary>The fixed role whose members may read every table in the database.</summary>
    public const string ReaderRole = "db_datareader";

    /// <summary>The fixed role whose members may write every table in the database.</summary>
    public const string WriterRole = "db_datawriter";

    /// <summary>The principal name parameter, spelled the same as the account scripts' so both share a helper.</summary>
    internal const string NameParameter = MsSqlAccountScripts.NameParameter;

    /// <summary>The role name parameter.</summary>
    internal const string RoleParameter = "@role";

    /// <summary>1 when the current database already has a user of that name.</summary>
    /// <remarks>
    /// No <c>type</c> filter. Any principal holding the name blocks <c>CREATE USER</c>, whatever kind it is, so
    /// narrowing this would report "missing" for a name that cannot in fact be created.
    /// </remarks>
    public const string DatabaseUserExists =
        $"SELECT 1 FROM sys.database_principals WHERE name = {NameParameter};";

    /// <summary>1 when the named user is a member of the named role in the current database.</summary>
    public const string RoleMembershipExists =
        "SELECT 1 FROM sys.database_role_members rm " +
        "INNER JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id " +
        "INNER JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id " +
        $"WHERE r.name = {RoleParameter} AND m.name = {NameParameter};";

    /// <summary>
    /// Maps the server login into the current database under the same name.
    /// </summary>
    /// <remarks>
    /// The user is deliberately given no permissions of its own here — on its own it can connect and see
    /// nothing. Everything the competitor can actually do comes from the role membership below, which is the
    /// only thing the read and write flags have to move.
    /// </remarks>
    public static string CreateDatabaseUser(string login) =>
        $"CREATE USER {TSql.QuoteName(login)} FOR LOGIN {TSql.QuoteName(login)};";

    public static string AddRoleMember(string role, string user) =>
        $"ALTER ROLE {TSql.QuoteName(role)} ADD MEMBER {TSql.QuoteName(user)};";

    public static string DropRoleMember(string role, string user) =>
        $"ALTER ROLE {TSql.QuoteName(role)} DROP MEMBER {TSql.QuoteName(user)};";
}
