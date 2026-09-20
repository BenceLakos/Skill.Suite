namespace Skill.Suite.Application.Competitors;

public sealed record CompetitorDto(
    Guid Id,
    string Username,
    string FullName,
    string Password,
    string IpAddress,
    /// <summary>Their second device, or null when they have none.</summary>
    string? MobileIpAddress,
    string CountryCode,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
