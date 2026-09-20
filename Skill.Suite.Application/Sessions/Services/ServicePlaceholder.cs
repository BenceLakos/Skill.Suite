namespace Skill.Suite.Application.Sessions.Services;

/// <summary>
/// One value a session's docker service can ask for by name in its environment, labels and volume host paths.
/// </summary>
/// <remarks>
/// A closed catalogue rather than free-form interpolation, because the set is what the administrator is
/// promised on the form and what the validators refuse anything outside of: a placeholder nobody resolves
/// would otherwise reach the container verbatim and be discovered as a connection string reading
/// <c>Server='{{database.sever}}'</c> once the competition has begun.
/// <para>
/// The enum is the identity; <see cref="ServicePlaceholders"/> holds the one spelling each value is written
/// as, so the name exists exactly once in the codebase.
/// </para>
/// </remarks>
public enum ServicePlaceholder
{
    /// <summary>The session's name, as the administrator typed it.</summary>
    SessionName,

    /// <summary>The session's slug — the organisation name, and the container name prefix.</summary>
    SessionSlug,

    /// <summary>The competitor's username: their git account, their SQL login and their repository name.</summary>
    CompetitorUsername,

    /// <summary>The competitor's full name, for a service that greets them by it.</summary>
    CompetitorFullName,

    /// <summary>The workstation address recorded for the competitor.</summary>
    CompetitorIpAddress,

    /// <summary>Their second device's address, or empty when they have none.</summary>
    CompetitorMobileIpAddress,

    /// <summary>The competitor's country code, upper-cased.</summary>
    CompetitorCountryCode,

    /// <summary>The competitor's own password, decrypted from the vault.</summary>
    CompetitorPassword,

    /// <summary>This competitor's session database, <c>{base}-{username}</c>.</summary>
    DatabaseName,

    /// <summary>The SQL Server as the service CONTAINER reaches it, host and port.</summary>
    DatabaseServer,

    /// <summary>
    /// The SQL login the service connects as — the competitor's own while the competition runs, the
    /// administrator's while marking.
    /// </summary>
    DatabaseLogin,

    /// <summary>The password belonging to <see cref="DatabaseLogin"/>.</summary>
    DatabasePassword,

    /// <summary>A ready-made ADO.NET connection string built from the four values above.</summary>
    DatabaseConnectionString,
}
