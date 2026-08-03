using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Competitors.CreateCompetitor;

public sealed record CreateCompetitorCommand(
    string Username,
    string FullName,
    string Password,
    string IpAddress,
    string CountryCode) : IRequest<Result<CompetitorDto>>;
