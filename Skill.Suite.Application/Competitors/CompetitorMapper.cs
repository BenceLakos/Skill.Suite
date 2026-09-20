using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Competitors;

namespace Skill.Suite.Application.Competitors;

// Manual mapper because the DTO surface includes the decrypted password — the projection
// from byte[] ciphertext to string plaintext requires the password vault, so Mapperly
// can't generate this for us.
public static class CompetitorMapper
{
    public static CompetitorDto ToDto(Competitor competitor, IPasswordVault vault) =>
        new(competitor.Id,
            competitor.Username,
            competitor.FullName,
            vault.Unprotect(competitor.EncryptedPassword),
            competitor.IpAddress,
            competitor.MobileIpAddress,
            competitor.CountryCode,
            competitor.CreatedAt,
            competitor.CreatedBy,
            competitor.UpdatedAt,
            competitor.UpdatedBy);

    public static List<CompetitorDto> ToDtoList(IEnumerable<Competitor> competitors, IPasswordVault vault) =>
        competitors.Select(c => ToDto(c, vault)).ToList();
}
