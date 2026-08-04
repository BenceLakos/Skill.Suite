using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Credentials;

namespace Skill.Suite.Application.Credentials;

// Manual mapper because the DTO surface includes the decrypted secret — the projection
// from byte[] ciphertext to string plaintext requires the password vault, so Mapperly
// can't generate this for us.
public static class CredentialMapper
{
    public static CredentialDto ToDto(Credential credential, IPasswordVault vault) =>
        new(credential.Id,
            credential.Name,
            credential.Kind,
            vault.Unprotect(credential.EncryptedSecret),
            credential.CreatedAt,
            credential.CreatedBy,
            credential.UpdatedAt,
            credential.UpdatedBy);

    public static List<CredentialDto> ToDtoList(IEnumerable<Credential> credentials, IPasswordVault vault) =>
        credentials.Select(c => ToDto(c, vault)).ToList();
}
