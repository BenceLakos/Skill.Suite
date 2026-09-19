namespace Skill.Suite.Infra.Sql;

/// <summary>
/// A SQL Server administration operation failed. The message is written to be shown to an admin, because it
/// lands in a snackbar on the competitors page and is the only thing they have to act on.
/// </summary>
/// <remarks>
/// The competitor's password is never part of the message: the statements that carry it are built here, so a
/// raw <c>SqlException</c> body could otherwise put a plaintext credential on screen and into the logs.
/// </remarks>
public sealed class MsSqlAdminException(string message) : Exception(message);
