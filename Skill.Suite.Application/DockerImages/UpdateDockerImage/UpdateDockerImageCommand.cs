using Mediator;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages.UpdateDockerImage;

public sealed record UpdateDockerImageCommand(
    Guid Id,
    string Name,
    string ImageName,
    DockerImageSource Source,
    string? BuildContext,
    string? DockerfilePath,
    Dictionary<string, string>? BuildArgs,
    Guid? NexusCredentialId) : IRequest<Result>;
