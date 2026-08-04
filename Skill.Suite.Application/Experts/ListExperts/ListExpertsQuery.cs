using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Experts.ListExperts;

public sealed record ListExpertsQuery(string? Search) : IRequest<Result<List<ExpertDto>>>;
