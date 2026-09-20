namespace Skill.Suite.Application.Sessions.MySession;

/// <summary>
/// Everything a competitor needs to connect to SQL Server, for both databases they may have.
/// </summary>
/// <remarks>
/// Reported from what the platform would have created rather than from the server itself. Asking SQL Server
/// whether the login exists means an outbound connection on the render path of a page a competitor keeps open
/// for the whole session — so a restarting server would blank the page that tells them what to connect to.
/// <para>
/// The login and the personal database share the competitor's username, which is how
/// <c>IMsSqlAdminClient.EnsureLoginAndDatabaseAsync</c> creates them: one name to type. The session database
/// is a second, separate one — named for the session and this competitor, and created by starting the
/// session rather than by provisioning their account.
/// </para>
/// </remarks>
/// <param name="Server">The server as this competitor's machine reaches it, or null when it cannot be derived.</param>
/// <param name="SessionDatabase">
/// This competitor's own database for the session, <c>{base}-{username}</c>, or null when the session
/// configures none. Nobody else is granted anything on it.
/// </param>
public sealed record MySessionDatabaseDto(
    string? Server,
    string Login,
    string Password,
    string PersonalDatabase,
    string? PersonalConnectionString,
    string? SessionDatabase,
    bool SessionReadAccess,
    bool SessionWriteAccess,
    string? SessionConnectionString);
