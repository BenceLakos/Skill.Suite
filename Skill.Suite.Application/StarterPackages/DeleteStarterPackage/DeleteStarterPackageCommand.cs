using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.StarterPackages.DeleteStarterPackage;

public sealed record DeleteStarterPackageCommand(string Name) : IRequest<Result>;
