using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Experts.DeleteExpert;

public sealed record DeleteExpertCommand(Guid Id) : IRequest<Result>;
