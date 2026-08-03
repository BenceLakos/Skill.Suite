using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Experts.UpdateExpert;

public sealed record UpdateExpertCommand(Guid Id, string DisplayName) : IRequest<Result>;
