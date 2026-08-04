using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Competitors.UpdateCompetitor;

public sealed record UpdateCompetitorCommand(
    Guid Id,
    string Username,
    string FullName,
    string Password,
    string IpAddress,
    string CountryCode) : IRequest<Result>;
