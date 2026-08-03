using Mediator;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.DockerImages;

namespace Skill.Suite.Application.DockerImages.CreateDockerImage;

public sealed record CreateDockerImageCommand(
    string Name,
    string ImageName,
    DockerImageSource Source,
    string? BuildContext,
    string? DockerfilePath,
    Dictionary<string, string>? BuildArgs,
    Guid? NexusCredentialId) : IRequest<Result<DockerImageDto>>;
