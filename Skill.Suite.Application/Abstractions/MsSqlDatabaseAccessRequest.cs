namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// The access one competitor's login is to end up with on a session's shared database.
/// </summary>
/// <remarks>
/// Both flags are the wanted end state, not a delta: <see langword="false"/> means "must not be able to", so
/// re-running after an admin clears a checkbox takes the access away again.
/// </remarks>
/// <param name="Database">The session database. Not the competitor's own database.</param>
/// <param name="Login">The competitor's SQL login, which already exists on the server.</param>
/// <param name="Read">Whether the login may read every table in the database.</param>
/// <param name="Write">Whether the login may write every table in the database.</param>
public sealed record MsSqlDatabaseAccessRequest(
    string Database, string Login, bool Read, bool Write, BasicCredential Admin);
