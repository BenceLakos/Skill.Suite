using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Competitors.DeleteCompetitor;

public sealed record DeleteCompetitorCommand(Guid Id) : IRequest<Result>;
