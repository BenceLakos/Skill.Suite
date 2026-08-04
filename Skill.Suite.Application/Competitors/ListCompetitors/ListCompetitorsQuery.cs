using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Competitors.ListCompetitors;

public sealed record ListCompetitorsQuery(string? Search) : IRequest<Result<List<CompetitorDto>>>;
