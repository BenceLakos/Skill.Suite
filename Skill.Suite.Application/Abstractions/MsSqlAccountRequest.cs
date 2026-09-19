namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// A competitor's SQL Server account: a login, a database of the same name, and the admin credential that is
/// allowed to create both.
/// </summary>
/// <param name="Name">Login and database name — deliberately the same, so the competitor has one name to type.</param>
/// <param name="Password">Plaintext login password. Never logged.</param>
public sealed record MsSqlAccountRequest(string Name, string Password, BasicCredential Admin);
