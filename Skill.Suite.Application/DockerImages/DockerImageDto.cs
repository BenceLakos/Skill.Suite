using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages;

public sealed record DockerImageDto(
    Guid Id,
    string Name,
    string ImageName,
    DockerImageSource Source,
    string? BuildContext,
    string? DockerfilePath,
    Dictionary<string, string> BuildArgs,
    Guid? NexusCredentialId,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
