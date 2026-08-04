using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Auth.Login;

public sealed record LoginCommand(string Username, string Password, bool RememberMe) : IRequest<Result>;
