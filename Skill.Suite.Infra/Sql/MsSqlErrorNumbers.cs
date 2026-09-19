namespace Skill.Suite.Infra.Sql;

/// <summary>
/// The <c>SqlException.Number</c> values that mean "somebody else already did this".
/// </summary>
/// <remarks>
/// Check-then-act against a shared server is a race: two admins pressing Provision at once, or a login created
/// from a SQL client between the check and the create. Losing that race is not an error — the end state is the
/// one that was asked for — so these numbers are folded into <c>AlreadyExisted</c> / <c>AlreadyMissing</c>
/// rather than surfaced.
/// </remarks>
internal static class MsSqlErrorNumbers
{
    /// <summary>The server principal already exists.</summary>
    public const int LoginAlreadyExists = 15025;

    /// <summary>A database with that name already exists.</summary>
    public const int DatabaseAlreadyExists = 1801;

    /// <summary>A user, group or role of that name already exists in the current database.</summary>
    public const int DatabaseUserAlreadyExists = 15023;

    /// <summary>Cannot drop the login, because it does not exist.</summary>
    public const int LoginNotFound = 15151;

    /// <summary>The server principal does not exist or the caller has no permission on it.</summary>
    public const int PrincipalDoesNotExist = 15007;

    /// <summary>Cannot drop the database, because it does not exist.</summary>
    public const int DatabaseNotFound = 3701;
}
