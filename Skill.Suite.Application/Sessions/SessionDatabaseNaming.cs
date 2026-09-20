namespace Skill.Suite.Application.Sessions;

/// <summary>
/// Names one competitor's database for a session, from the session's base name and their username.
/// </summary>
/// <remarks>
/// A session's database is not one database. Every competitor gets their own, named
/// <c>{base}-{username}</c> — <c>session-1-joe-doe</c> and <c>session-1-john-doe</c> for the base
/// <c>session-1</c> — and is granted access to that one only. Nothing else enforces the separation: one
/// database with everyone's grants on it is a competition where one competitor's <c>DELETE</c> is every
/// competitor's <c>DELETE</c>.
/// <para>
/// Derived rather than stored, like <see cref="SessionServiceNaming"/> and for the same reason: provisioning
/// has to be restartable, so the name a re-run computes must be the name the first run created. Deriving it
/// from two values that cannot change without the administrator changing them is what guarantees that, and
/// it is also what lets the competitor's own page name their database without asking the server.
/// </para>
/// </remarks>
internal static class SessionDatabaseNaming
{
    /// <summary>SQL Server's own limit on an identifier, which a database name is.</summary>
    /// <remarks>
    /// Restated here rather than taken from <c>TSql.MaxIdentifierLength</c> in the Infrastructure layer: this
    /// has to be checked before anything is sent, and the Application layer does not depend on that assembly.
    /// </remarks>
    internal const int MaxIdentifierLength = 128;

    /// <summary>The one character that reads as a separator and is legal in a bracketed identifier.</summary>
    private const string Separator = "-";

    /// <summary>
    /// How much of the identifier the session's base name may take, leaving the rest for the username.
    /// </summary>
    /// <remarks>
    /// A split rather than a derivation, because there is nothing to derive it from: a username may be up to
    /// 120 characters, and reserving that much would leave a base name of seven. Half the budget each is
    /// generous for both in practice — competition usernames are of the order of <c>c01</c> or
    /// <c>joe-doe</c> — and the remainder is checked per competitor by <see cref="FitsAnIdentifier"/>, so a
    /// username that does not fit is reported against that competitor instead of being truncated into a
    /// database somebody else can reach.
    /// </remarks>
    public const int MaxBaseNameLength = 64;

    public static string For(string baseName, string username) =>
        $"{baseName}{Separator}{username}";

    /// <summary>Whether the two would name a database SQL Server is willing to have.</summary>
    public static bool FitsAnIdentifier(string baseName, string username) =>
        baseName.Length + Separator.Length + username.Length <= MaxIdentifierLength;
}
