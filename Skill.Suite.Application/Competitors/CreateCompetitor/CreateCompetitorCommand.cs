using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Competitors.CreateCompetitor;

public sealed record CreateCompetitorCommand(
    string Username,
    string FullName,
    string Password,
    string IpAddress,
    /// <summary>Optional second device of theirs, which counts as the same competitor everywhere.</summary>
    string? MobileIpAddress,
    string CountryCode) : IRequest<Result<CompetitorDto>>;
