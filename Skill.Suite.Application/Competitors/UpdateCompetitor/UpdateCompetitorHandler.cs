using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;

namespace Skill.Suite.Application.Competitors.UpdateCompetitor;

public sealed class UpdateCompetitorHandler(
    IAppDbContext db,
    IPasswordVault vault,
    IUserAccountService userAccounts)
    : IRequestHandler<UpdateCompetitorCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateCompetitorCommand request, CancellationToken cancellationToken)
    {
        var competitor = await db.Competitors.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (competitor is null)
            return Result.Failure(CompetitorErrors.NotFound(request.Id));

        var username = request.Username.Trim();
        if (!string.Equals(competitor.Username, username, StringComparison.Ordinal))
        {
            var taken = await db.Competitors.AnyAsync(c => c.Id != request.Id && c.Username == username, cancellationToken);
            if (taken)
                return Result.Failure(CompetitorErrors.UsernameConflict);
        }

        competitor.UpdateProfile(
            username, request.FullName, request.IpAddress, request.MobileIpAddress, request.CountryCode);
        competitor.SetPassword(vault.Protect(request.Password));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var userResult = await userAccounts.UpdateUserAsync(
            competitor.Id,
            competitor.Username,
            competitor.FullName,
            request.Password,
            cancellationToken);

        if (userResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CompetitorErrors.UserProvisioningFailed(userResult.Error.Message));
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
