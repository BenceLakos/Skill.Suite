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
/// <para>
/// A competitor's ACCOUNT is the login and nothing more. Databases are a separate concern with a separate
/// lifetime: each one belongs to a session, is created when that session starts, and outlives neither more nor
/// less than the session does.
/// </para>
/// </remarks>
public interface IMsSqlAdminClient
{
    /// <summary>
    /// Creates the login if it does not already exist.
    /// </summary>
    /// <remarks>
    /// An existing login's password is NOT reset — the same rule the git host side follows, so re-running
    /// provisioning never invalidates credentials a competitor is already using.
    /// </remarks>
    Task<AccountProvisioning> EnsureLoginAsync(
        MsSqlAccountRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the database if it does not already exist.
    /// </summary>
    /// <remarks>
    /// The database is one competitor's but is not owned by them: no login is made its owner, and the access
    /// their login gets inside it is set by <see cref="GrantDatabaseAccessAsync"/> from the session's read and
    /// write flags. Access that a flag grants is access the same flag can take away again, which ownership
    /// would put out of reach.
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
    /// Counts sessions other than this one that belong to the login.
    /// </summary>
    /// <remarks>
    /// A non-zero count is why removal refuses: dropping the login someone is connected with cuts a competitor
    /// off mid-statement, and the session database they were working in stays behind with nothing left that
    /// can sign in to it.
    /// </remarks>
    Task<int> CountActiveConnectionsAsync(
        string name, BasicCredential admin, CancellationToken cancellationToken);

    /// <summary>Drops the login. Destructive and not recoverable.</summary>
    /// <remarks>
    /// Only the login. The session databases it was granted access to are left exactly where they are, because
    /// they hold the work a marking dispute is settled from and their lifetime is the session's, not the
    /// account's.
    /// </remarks>
    Task<AccountRemoval> DropLoginAsync(
        string name, BasicCredential admin, CancellationToken cancellationToken);
}
