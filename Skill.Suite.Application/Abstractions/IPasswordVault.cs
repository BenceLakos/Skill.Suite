namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Reversibly protects competitor passwords so judgement runners can recover the
/// plaintext later. Implementations should use a versioned protector purpose so the
/// key material can be rotated without invalidating stored ciphertexts.
/// </summary>
public interface IPasswordVault
{
    byte[] Protect(string plaintext);
    string Unprotect(byte[] ciphertext);
}
