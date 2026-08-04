namespace Skill.Suite.Application.Competitors;

public sealed record CompetitorDto(
    Guid Id,
    string Username,
    string FullName,
    string Password,
    string IpAddress,
    string CountryCode,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
