using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Sessions.GetSession;

public sealed record GetSessionByIdQuery(Guid Id) : IRequest<Result<SessionDto>>;
