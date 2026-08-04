using Mediator;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions.ListSessions;

public sealed record ListSessionsQuery(SessionStatus? Status, string? Search) : IRequest<Result<List<SessionDto>>>;
