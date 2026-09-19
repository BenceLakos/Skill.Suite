namespace Skill.Suite.Application.DockerImages.ListSessionImageOptions;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Returns every image reference the session editor's pickers should offer: the ones registered as docker
/// images, plus every container package published on the git host's registry.
/// </summary>
public sealed record ListSessionImageOptionsQuery : IRequest<Result<SessionImageOptionsDto>>;
