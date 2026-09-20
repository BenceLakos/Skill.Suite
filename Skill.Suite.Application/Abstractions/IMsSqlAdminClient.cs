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
    /// For a session database, which is one competitor's but is not owned by them: no login is made its
    /// owner, and the access their login gets inside it is set by <see cref="GrantDatabaseAccessAsync"/> from
    /// the session's read and write flags. That is the difference from
    /// <see cref="EnsureLoginAndDatabaseAsync"/>, where the competitor owns their personal database outright.
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

    /// <summary>
    /// Runs an administrator-supplied script against one database, batch by batch.
    /// </summary>
    /// <remarks>
    /// The one method here that is NOT idempotent, because it cannot be: the script is the administrator's and
    /// says what it says. That is why the caller only runs it on a database it has just created — re-running a
    /// seed script over a database competitors have been working in would undo their work, not repair it.
    /// <para>
    /// Not wrapped in a transaction either. A seed script exported from a SQL client manages its own where it
    /// wants one, and several of the statements such a script is made of — <c>CREATE DATABASE</c>,
    /// <c>ALTER DATABASE</c>, a full-text index — cannot run inside one at all. An outer transaction would
    /// therefore fail on exactly the scripts it was meant to protect. A script that fails halfway leaves what
    /// it had done behind, which the caller reports as a failure against the database.
    /// </para>
    /// </remarks>
    Task ExecuteScriptAsync(
        string database, string script, BasicCredential admin, CancellationToken cancellationToken);

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
