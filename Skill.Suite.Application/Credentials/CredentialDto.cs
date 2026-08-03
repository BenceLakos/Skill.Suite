using Skill.Suite.Domain.Credentials;

namespace Skill.Suite.Application.Credentials;

public sealed record CredentialDto(
    Guid Id,
    string Name,
    CredentialKind Kind,
    string Secret,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
