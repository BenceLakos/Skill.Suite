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
/// <para>
/// An account is a login and nothing else. Databases arrive and leave with the sessions that need them, which
/// is why creating one and granting access to it are separate operations from creating the account.
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

    public async Task<AccountProvisioning> EnsureLoginAsync(
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
            created = await TryExecuteAsync(
                connection,
                MsSqlAccountScripts.CreateLogin(name, request.Password),
                $"create the login '{name}'",
                MsSqlErrorNumbers.LoginAlreadyExists,
                cancellationToken);
        }

        logger.LogInformation(
            "SQL Server login {Name}: {Outcome}", name, created ? "created" : "already existed");

        return created ? AccountProvisioning.Created : AccountProvisioning.AlreadyExisted;
    }

    public async Task<AccountProvisioning> EnsureDatabaseAsync(
        string database, BasicCredential admin, CancellationToken cancellationToken)
    {
        await using var connection = Connect(admin);
        await connection.OpenAsync(cancellationToken);

        var created = false;

        if (!await ExistsAsync(connection, MsSqlAccountScripts.DatabaseExists, database, cancellationToken))
        {
            created = await TryExecuteAsync(
                connection,
                MsSqlAccountScripts.CreateDatabase(database),
                $"create the database '{database}'",
                MsSqlErrorNumbers.DatabaseAlreadyExists,
                cancellationToken);
        }

        logger.LogInformation(
            "SQL Server database {Database}: {Outcome}", database, created ? "created" : "already existed");

        return created ? AccountProvisioning.Created : AccountProvisioning.AlreadyExisted;
    }

    public async Task GrantDatabaseAccessAsync(
        MsSqlDatabaseAccessRequest request, CancellationToken cancellationToken)
    {
        // Opened against the competitor's session database rather than master, because everything below is scoped to the
        // current database and none of it can say which one it means. USE is not an alternative: CREATE USER
        // has to be the first statement in its batch, and this client sends one statement per command anyway.
        await using var connection = Connect(request.Admin, request.Database);
        await connection.OpenAsync(cancellationToken);

        var login = request.Login;

        if (!await ExistsAsync(
                connection, MsSqlDatabaseAccessScripts.DatabaseUserExists, login, cancellationToken))
        {
            await TryExecuteAsync(
                connection,
                MsSqlDatabaseAccessScripts.CreateDatabaseUser(login),
                $"create the database user '{login}' in '{request.Database}'",
                MsSqlErrorNumbers.DatabaseUserAlreadyExists,
                cancellationToken);
        }

        await SetRoleMembershipAsync(
            connection, request, MsSqlDatabaseAccessScripts.ReaderRole, request.Read, cancellationToken);

        await SetRoleMembershipAsync(
            connection, request, MsSqlDatabaseAccessScripts.WriterRole, request.Write, cancellationToken);

        logger.LogInformation(
            "SQL Server access for {Login} on {Database}: read {Read}, write {Write}",
            login, request.Database, request.Read, request.Write);
    }

    public async Task ExecuteScriptAsync(
        string database, string script, BasicCredential admin, CancellationToken cancellationToken)
    {
        var batches = SqlBatchSplitter.Split(script);
        if (batches.Count == 0)
        {
            logger.LogInformation("The script for {Database} contains no statements; nothing was run", database);
            return;
        }

        // Against the target database rather than master, for the same reason the grants are: nothing in an
        // author's script names the database, and a USE cannot be prefixed onto a batch whose first statement
        // has to be the first statement.
        await using var connection = Connect(admin, database);
        await connection.OpenAsync(cancellationToken);

        for (var index = 0; index < batches.Count; index++)
        {
            // One connection for the whole script, sequentially: temporary tables, variables and settings a
            // batch sets up for the next one live on the connection, so a script split across several would
            // fail on statements that are correct.
            await ExecuteAsync(
                connection,
                batches[index],
                $"run batch {index + 1} of {batches.Count} of the script against '{database}'",
                cancellationToken);
        }

        logger.LogInformation(
            "Ran {Batches} script batches against the database {Database}", batches.Count, database);
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

    /// <remarks>
    /// The login only. Every database it was given access to belongs to a session and stays, including the
    /// database user the login was mapped to inside each one. Those users are left orphaned on purpose:
    /// hunting them down means a connection to every database on the server, and re-provisioning the login
    /// under the same name is not what re-attaches them anyway — the marker reads the data, not the mapping.
    /// <para>
    /// A login that OWNS a database cannot be dropped at all (error 15174). Nothing this platform does makes a
    /// competitor an owner any more, so hitting it means somebody set the ownership by hand; it is reported
    /// rather than worked around, because the fix is a decision about that database, not about the account.
    /// </para>
    /// </remarks>
    public async Task<AccountRemoval> DropLoginAsync(
        string name, BasicCredential admin, CancellationToken cancellationToken)
    {
        await using var connection = Connect(admin);
        await connection.OpenAsync(cancellationToken);

        var removed = false;

        if (await ExistsAsync(connection, MsSqlAccountScripts.LoginExists, name, cancellationToken))
            removed = await TryDropLoginAsync(connection, name, cancellationToken);

        logger.LogInformation(
            "SQL Server login {Name}: {Outcome}", name, removed ? "removed" : "already missing");

        return removed ? AccountRemoval.Removed : AccountRemoval.AlreadyMissing;
    }

    // ------------------------------------------------------------------ plumbing

    private SqlConnection Connect(BasicCredential admin, string catalog = AdminCatalog)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.Value.Server,
            InitialCatalog = catalog,
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

    /// <summary>
    /// Adds or removes the login from the role so that its membership matches <paramref name="wanted"/>.
    /// </summary>
    /// <remarks>
    /// Membership is read first rather than inferred from whether the <c>ALTER ROLE</c> complained. The server
    /// is not consistent about what it reports for a member that is already there or already gone, and this has
    /// to converge on the flags every run, not the first one.
    /// </remarks>
    private async Task SetRoleMembershipAsync(
        SqlConnection connection,
        MsSqlDatabaseAccessRequest request,
        string role,
        bool wanted,
        CancellationToken cancellationToken)
    {
        var login = request.Login;

        if (await IsRoleMemberAsync(connection, login, role, cancellationToken) == wanted) return;

        var sql = wanted
            ? MsSqlDatabaseAccessScripts.AddRoleMember(role, login)
            : MsSqlDatabaseAccessScripts.DropRoleMember(role, login);

        var what = wanted
            ? $"add '{login}' to '{role}' in '{request.Database}'"
            : $"remove '{login}' from '{role}' in '{request.Database}'";

        await ExecuteAsync(connection, sql, what, cancellationToken);
    }

    private async Task<bool> IsRoleMemberAsync(
        SqlConnection connection, string login, string role, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, MsSqlDatabaseAccessScripts.RoleMembershipExists);
        command.Parameters.AddWithValue(MsSqlDatabaseAccessScripts.NameParameter, login);
        command.Parameters.AddWithValue(MsSqlDatabaseAccessScripts.RoleParameter, role);

        try
        {
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }
        catch (SqlException ex)
        {
            throw Wrap(ex, $"check whether '{login}' is a member of '{role}'");
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

    /// <summary>
    /// Drops the login, distinguishing "it was already gone" from "the server will not let go of it".
    /// </summary>
    /// <returns><see langword="true"/> when this call is the one that removed the login.</returns>
    /// <remarks>
    /// Written out rather than routed through <see cref="TryExecuteAsync(SqlConnection,string,string,int,int,CancellationToken)"/>
    /// because 15174 needs a message of its own: "SQL Server returned error 15174" tells an admin nothing they
    /// can act on, whereas naming the ownership tells them exactly what to change first.
    /// </remarks>
    private async Task<bool> TryDropLoginAsync(
        SqlConnection connection, string name, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, MsSqlAccountScripts.DropLogin(name));

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (SqlException ex)
            when (ex.Number is MsSqlErrorNumbers.LoginNotFound or MsSqlErrorNumbers.PrincipalDoesNotExist)
        {
            logger.LogInformation(
                "Did not need to drop the login '{Name}': the server reported {Number}", name, ex.Number);

            return false;
        }
        catch (SqlException ex) when (ex.Number == MsSqlErrorNumbers.LoginOwnsDatabases)
        {
            throw Wrap(ex, $"drop the login '{name}', because it still owns at least one database");
        }
        catch (SqlException ex)
        {
            throw Wrap(ex, $"drop the login '{name}'");
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
