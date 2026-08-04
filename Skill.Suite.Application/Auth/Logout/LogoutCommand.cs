using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Auth.Logout;

public sealed record LogoutCommand : IRequest<Result>;
