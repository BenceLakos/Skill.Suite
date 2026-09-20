namespace Skill.Suite.Application.Sessions.StartMarking;

using Skill.Suite.Application.Abstractions;

/// <summary>
/// What marking needs of the SQL Server: who to connect as, and whose databases exist.
/// </summary>
/// <remarks>
/// The administrator rather than the competitor, which is the whole difference between a marking container
/// and a competition one. The session's read and write flags are what the competitor's own login is bounded
/// by, and a marker bounded by them would see only what the competitor was allowed to see rather than what
/// they produced.
/// </remarks>
/// <param name="Logins">
/// The logins the server holds, matched case-insensitively for the reason the account partitioner is: SQL
/// Server logins compare that way under the usual collation.
/// </param>
internal sealed record MarkingDatabaseAccess(
    BasicCredential Admin,
    IReadOnlySet<string> Logins);
