using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.DockerImages.PushDockerImage;

public sealed record PushDockerImageCommand(Guid Id, string Tag) : IRequest<Result>;
