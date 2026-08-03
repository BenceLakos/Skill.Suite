using Microsoft.AspNetCore.Identity;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Infra.Identity;

internal sealed class IdentityUserAccountService(UserManager<ApplicationUser> userManager) : IUserAccountService
{
    public async Task<Result> CreateUserAsync(
        Guid userId,
        string userName,
        string? fullName,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = userName,
            FullName = fullName,
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            return Result.Failure(ToError(created));

        var roled = await userManager.AddToRoleAsync(user, role);
        if (!roled.Succeeded)
            return Result.Failure(ToError(roled));

        return Result.Success();
    }

    public async Task<Result> UpdateUserAsync(
        Guid userId,
        string userName,
        string? fullName,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure(Error.NotFound("User.NotFound", $"User '{userId}' was not found."));

        if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
        {
            var renamed = await userManager.SetUserNameAsync(user, userName);
            if (!renamed.Succeeded)
                return Result.Failure(ToError(renamed));
        }

        user.FullName = fullName;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
            return Result.Failure(ToError(updated));

        if (!string.IsNullOrEmpty(newPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, newPassword);
            if (!reset.Succeeded)
                return Result.Failure(ToError(reset));
        }

        return Result.Success();
    }

    public async Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Success();

        var deleted = await userManager.DeleteAsync(user);
        return deleted.Succeeded
            ? Result.Success()
            : Result.Failure(ToError(deleted));
    }

    private static Error ToError(IdentityResult result)
    {
        var first = result.Errors.FirstOrDefault();
        var detail = first is null
            ? "Identity operation failed."
            : first.Description;
        return Error.Failure("Identity.OperationFailed", detail);
    }
}
