using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.DockerImages.ListDockerImages;

public sealed record ListDockerImagesQuery(string? Search) : IRequest<Result<List<DockerImageDto>>>;
