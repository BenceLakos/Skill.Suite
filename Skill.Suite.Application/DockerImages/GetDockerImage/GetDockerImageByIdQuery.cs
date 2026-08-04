using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.DockerImages.GetDockerImage;

public sealed record GetDockerImageByIdQuery(Guid Id) : IRequest<Result<DockerImageDto>>;
