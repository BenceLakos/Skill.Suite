using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.ListStarterPackages;

public sealed record ListStarterPackagesQuery : IRequest<Result<IReadOnlyList<StarterPackageDto>>>;
