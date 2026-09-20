namespace Skill.Suite.Application.Competitors.GetLoginPrefill;

/// <summary>
/// The credentials the sign-in form is filled in with for a recognised workstation.
/// </summary>
/// <param name="Password">
/// Plaintext, decrypted from the competitor row. It is rendered into an anonymous page, which is the whole
/// point of the feature and the reason the lookup refuses anything less than an exact, unambiguous match.
/// </param>
public sealed record CompetitorLoginPrefillDto(string Username, string Password);
