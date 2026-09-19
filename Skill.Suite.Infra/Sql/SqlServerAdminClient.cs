namespace Skill.Suite.Infra.Sql;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// <see cref="IMsSqlAdminClient"/> over <c>Microsoft.Data.SqlClient</c>, connecting to <c>master</c> as the
/// configured administrator.
/// </summary>
/// <remarks>
/// One connection per public method, opened and closed around the whole operation. Provisioning is a handful
/// of statements pressed by hand from a grid, not a hot path, and a pooled short-lived connection is what keeps
/// a SQL Server that is down from holding a Blazor circuit open.
/// <para>
/// Nothing here runs in a transaction: <c>CREATE DATABASE</c> cannot, so a half-finished provision is repaired
/// by pressing the button again rather than rolled back. Every step is written to make that safe.
/// </para>
/// </remarks>
internal sealed class SqlServerAdminClient(
    IOptions<MsSqlOptions> options,
    ILogger<SqlServerAdminClient> logger)
    : IMsSqlAdminClient
{
    /// <summary>The database an administrative connection is made against.</summary>
    private const string AdminCatalog = "master";

    /// <summary>How much of a server error message is worth showing an admin.</summary>
    private const int MaxErrorDetailLength = 300;

    public async Task<AccountProvisioning> EnsureLoginAndDatabaseAsync(
        MsSqlAccountRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name;

        await using var connection = Connect(request.Admin);
        await connection.OpenAsync(cancellationToken);

        var created = false;

        if (!await ExistsAsync(connection, MsSqlAccountScripts.LoginExists, name, cancellationToken))
        {
            // Not "if it does not exist, create it" as one statement: the check and the create are separated by
            // a round trip, so another admin can win the race between them. 15025 says they did, which is the
            // state that was wanted anyway.
            created |= await TryExecuteAsync(
                connection,
                MsSqlAccountScripts.CreateLogin(name, request.Password),
                $"create the login '{name}'",
                MsSqlErrorNumbers.LoginAlreadyExists,
                cancellationToken);
        }

        if (!await ExistsAsync(connection, MsSqlAccountScripts.DatabaseExists, name, cancellationToken))
        {
            created |= await TryExecuteAsync(
                connection,
                MsSqlAccountScripts.CreateDatabase(name),
                $"create the database '{name}'",
                MsSqlErrorNumbers.DatabaseAlreadyExists,
                cancellationToken);
        }

        // Both are idempotent and both are run every time, deliberately. They are what repairs an account left
        // half-provisioned by an earlier failure — ownership is the thing a competitor actually needs, and it
        // is the step most likely to have been the one that did not happen.
        await ExecuteAsync(
            connection, MsSqlAccountScripts.GrantDatabaseOwnership(name),
            $"make '{name}' the owner of its database", cancellationToken);

        await ExecuteAsync(
            connection, MsSqlAccountScripts.SetDefaultDatabase(name),
            $"set the default database for '{name}'", cancellationToken);

        logger.LogInformation(
            "SQL Server account {Name}: {Outcome}", name, created ? "created" : "already existed");

        return created ? AccountProvisioning.Created : AccountProvisioning.AlreadyExisted;
    }

    public async Task<MsSqlAccountInventory> GetInventoryAsync(
        BasicCredential admin, CancellationToken cancellationToken)
    {
        await using var connection = Connect(admin);
        await connection.OpenAsync(cancellationToken);

        await using var command = Command(connection, MsSqlAccountScripts.Inventory);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var logins = await ReadNamesAsync(reader, cancellationToken);
            await reader.NextResultAsync(cancellationToken);
            var databases = await ReadNamesAsync(reader, cancellationToken);

            return new MsSqlAccountInventory(logins, databases);
        }
        catch (SqlException ex)
        {
            throw Wrap(ex, "read the SQL Server logins and databases");
        }
    }

    public async Task<int> CountActiveConnectionsAsync(
        string name, BasicCredential admin, CancellationToken cancellationToken)
    {
        await using var connection = Connect(admin);
        await connection.OpenAsync(cancellationToken);

        await using var command = Command(connection, MsSqlAccountScripts.ActiveConnectionCount);
        command.Parameters.AddWithValue(MsSqlAccountScripts.NameParameter, name);

        try
        {
            var count = await command.ExecuteScalarAsync(cancellationToken);
            return count is int value ? value : 0;
        }
        catch (SqlException ex)
        {
            throw Wrap(ex, $"count the open connections for '{name}'");
        }
    }

    public async Task<AccountRemoval> DropLoginAndDatabaseAsync(
        string name, BasicCredential admin, CancellationToken cancellationToken)
    {
        await using var connection = Connect(admin);
        await connection.OpenAsync(cancellationToken);

        var removed = false;

        // Database first. A login that owns a database cannot be dropped, so the other order fails halfway and
        // leaves the database orphaned to a principal that no longer exists.
        if (await ExistsAsync(connection, MsSqlAccountScripts.DatabaseExists, name, cancellationToken))
        {
            removed |= await TryExecuteAsync(
                connection,
                MsSqlAccountScripts.DropDatabase(name),
                $"drop the database '{name}'",
                MsSqlErrorNumbers.DatabaseNotFound,
                cancellationToken);
        }

        if (await ExistsAsync(connection, MsSqlAccountScripts.LoginExists, name, cancellationToken))
        {
            removed |= await TryExecuteAsync(
                connection,
                MsSqlAccountScripts.DropLogin(name),
                $"drop the login '{name}'",
                MsSqlErrorNumbers.LoginNotFound,
                MsSqlErrorNumbers.PrincipalDoesNotExist,
                cancellationToken);
        }

        logger.LogInformation(
            "SQL Server account {Name}: {Outcome}", name, removed ? "removed" : "already missing");

        return removed ? AccountRemoval.Removed : AccountRemoval.AlreadyMissing;
    }

    // ------------------------------------------------------------------ plumbing

    private SqlConnection Connect(BasicCredential admin)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.Value.Server,
            InitialCatalog = AdminCatalog,
            UserID = admin.Username,
            Password = admin.Secret,
            Encrypt = true,
            // The server presents a self-signed certificate and a competition LAN has no CA that could have
            // signed a real one. The connection is encrypted either way; only the chain check is skipped.
            TrustServerCertificate = options.Value.TrustServerCertificate,
            ConnectTimeout = options.Value.ConnectTimeoutSeconds,
        };

        return new SqlConnection(builder.ConnectionString);
    }

    private SqlCommand Command(SqlConnection connection, string sql) =>
        new(sql, connection) { CommandTimeout = options.Value.CommandTimeoutSeconds };

    private async Task<bool> ExistsAsync(
        SqlConnection connection, string sql, string name, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, sql);
        command.Parameters.AddWithValue(MsSqlAccountScripts.NameParameter, name);

        try
        {
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }
        catch (SqlException ex)
        {
            throw Wrap(ex, $"check whether '{name}' already exists");
        }
    }

    private async Task ExecuteAsync(
        SqlConnection connection, string sql, string what, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, sql);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw Wrap(ex, what);
        }
    }

    /// <summary>
    /// Runs the statement, treating the given error numbers as "somebody else got there first".
    /// </summary>
    /// <returns><see langword="true"/> when this call is the one that changed the server.</returns>
    private async Task<bool> TryExecuteAsync(
        SqlConnection connection,
        string sql,
        string what,
        int benignError,
        CancellationToken cancellationToken) =>
        await TryExecuteAsync(connection, sql, what, benignError, benignError, cancellationToken);

    private async Task<bool> TryExecuteAsync(
        SqlConnection connection,
        string sql,
        string what,
        int firstBenignError,
        int secondBenignError,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, sql);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (SqlException ex) when (ex.Number == firstBenignError || ex.Number == secondBenignError)
        {
            logger.LogInformation("Did not need to {What}: the server reported {Number}", what, ex.Number);
            return false;
        }
        catch (SqlException ex)
        {
            throw Wrap(ex, what);
        }
    }

    private static async Task<List<string>> ReadNamesAsync(
        SqlDataReader reader, CancellationToken cancellationToken)
    {
        var names = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
            names.Add(reader.GetString(0));

        return names;
    }

    /// <summary>
    /// Turns a driver exception into something safe to put in front of an admin.
    /// </summary>
    /// <remarks>
    /// Only the server's own message is carried over, trimmed — never the statement, which for
    /// <c>CREATE LOGIN</c> contains the competitor's password in plaintext.
    /// </remarks>
    private static MsSqlAdminException Wrap(SqlException exception, string what)
    {
        var detail = exception.Message.Trim();
        if (detail.Length > MaxErrorDetailLength) detail = detail[..MaxErrorDetailLength];

        return new MsSqlAdminException(
            $"Could not {what}: SQL Server returned error {exception.Number}. {detail}");
    }
}
