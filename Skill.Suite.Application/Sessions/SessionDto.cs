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
    /// <summary>
    /// Whether pushes to this session are judged at all; false when it names no judgement image.
    /// </summary>
    /// <remarks>
    /// Carried rather than re-derived by every caller, for the reason <see cref="GitOrganization"/> is: a
    /// page that decides for itself what a blank image means is a second place for the answer to be wrong,
    /// and the answer is what tells a competitor's missing run apart from a session that never marks one.
    /// </remarks>
    bool RequiresJudgement,
    string? DatabaseName,
    bool DatabaseReadAccess,
    bool DatabaseWriteAccess,
    /// <summary>Path relative to the starter packages volume root, or null.</summary>
    string? DatabaseSeedScript,
    Guid? GitCredentialId,
    Guid? JudgementImagePullCredentialId,
    List<SessionDockerImage> DockerImages,
    /// <summary>Organisation on the git host owning this session's repositories; derived from the slug.</summary>
    string GitOrganization,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
