namespace Skill.Suite.Application.Experts;

public sealed record ExpertDto(
    Guid Id,
    string DisplayName,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
