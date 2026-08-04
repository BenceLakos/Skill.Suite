using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.DockerImages.ListDockerImageOptions;

/// <summary>
/// Returns the unique image references currently registered as docker images.
/// Used by the session editor to populate "pick an image" dropdowns.
/// </summary>
public sealed record ListDockerImageOptionsQuery : IRequest<Result<List<string>>>;
