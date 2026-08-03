using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.DockerImages.DeleteDockerImage;

public sealed record DeleteDockerImageCommand(Guid Id) : IRequest<Result>;
