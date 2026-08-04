namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Username + secret tuple decrypted from a <see cref="Skill.Suite.Domain.Credentials.Credential"/>.
/// The on-disk format is <c>username:secret</c>; entries with no colon are treated as a
/// bare token (username left empty).
/// </summary>
public sealed record BasicCredential(string Username, string Secret)
{
    public static BasicCredential Parse(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return new BasicCredential(string.Empty, string.Empty);

        var colon = plaintext.IndexOf(':');
        return colon < 0
            ? new BasicCredential(string.Empty, plaintext)
            : new BasicCredential(plaintext[..colon], plaintext[(colon + 1)..]);
    }
}
