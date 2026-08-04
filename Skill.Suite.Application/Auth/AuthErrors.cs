using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Invalid username or password.");
}
