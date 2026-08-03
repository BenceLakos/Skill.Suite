using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Application-level abstraction over the Identity user store so handlers can
/// provision and maintain login accounts without depending on ASP.NET Identity directly.
/// </summary>
public interface IUserAccountService
{
    Task<Result> CreateUserAsync(
        Guid userId,
        string userName,
        string? fullName,
        string password,
        string role,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateUserAsync(
        Guid userId,
        string userName,
        string? fullName,
        string? newPassword,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
