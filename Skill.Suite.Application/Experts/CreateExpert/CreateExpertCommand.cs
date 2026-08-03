using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Experts.CreateExpert;

public sealed record CreateExpertCommand(string DisplayName) : IRequest<Result<ExpertDto>>;
