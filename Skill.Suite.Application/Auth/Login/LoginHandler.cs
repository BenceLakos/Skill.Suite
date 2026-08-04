using Mediator;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Auth.Login;

public sealed class LoginHandler(IUserAuthService auth) : IRequestHandler<LoginCommand, Result>
{
    public async ValueTask<Result> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var success = await auth.SignInAsync(request.Username, request.Password, request.RememberMe, cancellationToken);
        return success ? Result.Success() : Result.Failure(AuthErrors.InvalidCredentials);
    }
}
