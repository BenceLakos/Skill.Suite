using Microsoft.AspNetCore.Identity;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.Identity;

internal sealed class IdentityAuthService(SignInManager<ApplicationUser> signInManager) : IUserAuthService
{
    public async Task<bool> SignInAsync(string username, string password, bool isPersistent, CancellationToken cancellationToken = default)
    {
        var result = await signInManager.PasswordSignInAsync(username, password, isPersistent, lockoutOnFailure: false);
        return result.Succeeded;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        signInManager.SignOutAsync();
}
