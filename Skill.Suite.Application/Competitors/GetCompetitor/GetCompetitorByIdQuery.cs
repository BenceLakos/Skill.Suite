using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Competitors.GetCompetitor;

public sealed record GetCompetitorByIdQuery(Guid Id) : IRequest<Result<CompetitorDto>>;
