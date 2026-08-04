using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Sessions.CloseSession;

public sealed record CloseSessionCommand(Guid Id) : IRequest<Result>;
