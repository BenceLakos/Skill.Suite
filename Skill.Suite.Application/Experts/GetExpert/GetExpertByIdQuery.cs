using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Experts.GetExpert;

public sealed record GetExpertByIdQuery(Guid Id) : IRequest<Result<ExpertDto>>;
