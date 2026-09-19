namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Administrative operations against the Microsoft SQL Server instance competitors are given a login on.
/// </summary>
/// <remarks>
/// Every method is idempotent, for the same reason the git host's are: provisioning walks N competitors over a
/// network and has to be restartable, so "already there" and "already gone" are successes rather than errors.
/// <para>
/// Nothing here is persisted on this side. The server is the only record of which competitor has an account,
/// which is what keeps the page honest when a login is created or dropped from a SQL client instead.
/// </para>
/// </remarks>
public interface IMsSqlAdminClient
{
    /// <summary>
    /// Creates the login, its database, and makes the login the owner of that database.
    /// </summary>
    /// <remarks>
    /// An existing login's password is NOT reset — the same rule the git host side follows, so re-running
    /// provisioning never invalidates credentials a competitor is already using.
    /// </remarks>
    Task<AccountProvisioning> EnsureLoginAndDatabaseAsync(
        MsSqlAccountRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the database if it does not already exist.
    /// </summary>
    /// <remarks>
    /// For a session's shared database, which belongs to nobody: no login is made its owner, and access to it
    /// is handed out one competitor at a time by <see cref="GrantDatabaseAccessAsync"/>.
    /// </remarks>
    Task<AccountProvisioning> EnsureDatabaseAsync(
        string database, BasicCredential admin, CancellationToken cancellationToken);

    /// <summary>
    /// Makes the login's read and write access inside the database match the request's flags.
    /// </summary>
    /// <remarks>
    /// Converging rather than granting: a flag that is now <see langword="false"/> takes the access away, so an
    /// admin who unticks a box and restarts the session gets what the box says. The database user the login
    /// needs inside that database is created on the way if it is missing.
    /// </remarks>
    Task GrantDatabaseAccessAsync(
        MsSqlDatabaseAccessRequest request, CancellationToken cancellationToken);

    /// <summary>Lists every SQL login and every database, for bulk status evaluation.</summary>
    Task<MsSqlAccountInventory> GetInventoryAsync(
        BasicCredential admin, CancellationToken cancellationToken);

    /// <summary>
    /// Counts sessions other than this one that belong to the login or are connected to its database.
    /// </summary>
    /// <remarks>
    /// A non-zero count is why removal refuses: dropping the database out from under a live connection is how
    /// a competitor loses work they were in the middle of.
    /// </remarks>
    Task<int> CountActiveConnectionsAsync(
        string name, BasicCredential admin, CancellationToken cancellationToken);

    /// <summary>Drops the database and then the login. Destructive and not recoverable.</summary>
    Task<AccountRemoval> DropLoginAndDatabaseAsync(
        string name, BasicCredential admin, CancellationToken cancellationToken);
}
