namespace Skill.Suite.Application.Sessions.ListSessionCompetitors;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// The competitors enrolled in one session, in username order.
/// </summary>
/// <remarks>
/// The enrolments rather than every competitor on the platform: marking acts on what a session actually
/// created, and offering a competitor the session was never started for would start containers pointed at a
/// database that does not exist.
/// </remarks>
public sealed record ListSessionCompetitorsQuery(Guid SessionId)
    : IRequest<Result<List<SessionCompetitorDto>>>;
