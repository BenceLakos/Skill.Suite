using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Auth.Logout;

public sealed class LogoutHandler(IUserAuthService auth) : IRequestHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(cancellationToken);
        return Result.Success();
    }
}
