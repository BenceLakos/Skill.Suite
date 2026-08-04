using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Sessions.GetSession;

public sealed record GetSessionBySlugQuery(string Slug) : IRequest<Result<SessionDto>>;
