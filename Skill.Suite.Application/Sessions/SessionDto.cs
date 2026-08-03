using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions;

public sealed record SessionDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    SessionStatus Status,
    string? TemplateFolder,
    string? JudgementImage,
    string? DatabaseName,
    bool DatabaseReadAccess,
    bool DatabaseWriteAccess,
    Guid? GitCredentialId,
    Guid? JudgementImagePullCredentialId,
    List<SessionDockerImage> DockerImages,
    /// <summary>Organisation on the git host owning this session's repositories; derived from the slug.</summary>
    string GitOrganization,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
