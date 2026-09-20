namespace Skill.Suite.Application.Sessions.MySession;

/// <summary>The competitor's own sign-in credentials, as they were handed out.</summary>
/// <param name="Password">Plaintext, decrypted from the competitor row for the competitor themselves.</param>
public sealed record MySessionCredentialsDto(string Username, string Password);
