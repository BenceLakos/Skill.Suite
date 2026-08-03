namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Application-level abstraction over the cookie-issuing sign-in machinery so
/// handlers don't depend on ASP.NET Identity directly.
/// </summary>
public interface IUserAuthService
{
    Task<bool> SignInAsync(string username, string password, bool isPersistent, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
