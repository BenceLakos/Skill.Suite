namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// A competitor's git-host user account, and the admin credential allowed to create it.
/// </summary>
/// <param name="Password">Plaintext. Used only when the user is created; an existing user's password is left alone.</param>
public sealed record EnsureGitHostUserRequest(
    string Username,
    string Email,
    string Password,
    BasicCredential Credential);
