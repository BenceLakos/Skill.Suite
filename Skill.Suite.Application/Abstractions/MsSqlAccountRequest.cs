namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// A competitor's SQL Server account — a login — and the admin credential that is allowed to create it.
/// </summary>
/// <param name="Name">The login name, which is the competitor's username.</param>
/// <param name="Password">Plaintext login password. Never logged.</param>
public sealed record MsSqlAccountRequest(string Name, string Password, BasicCredential Admin);
