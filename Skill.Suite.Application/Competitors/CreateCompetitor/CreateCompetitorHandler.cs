using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Authorization;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;

namespace Skill.Suite.Application.Competitors.CreateCompetitor;

public sealed class CreateCompetitorHandler(
    IAppDbContext db,
    IPasswordVault vault,
    IUserAccountService userAccounts)
    : IRequestHandler<CreateCompetitorCommand, Result<CompetitorDto>>
{
    public async ValueTask<Result<CompetitorDto>> Handle(CreateCompetitorCommand request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();

        var taken = await db.Competitors.AnyAsync(c => c.Username == username, cancellationToken);
        if (taken)
            return CompetitorErrors.UsernameConflict;

        var competitor = Competitor.Create(
            username,
            request.FullName,
            vault.Protect(request.Password),
            request.IpAddress,
            request.CountryCode);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Competitors.Add(competitor);
        await db.SaveChangesAsync(cancellationToken);

        var userResult = await userAccounts.CreateUserAsync(
            competitor.Id,
            competitor.Username,
            competitor.FullName,
            request.Password,
            Roles.Competitor,
            cancellationToken);

        if (userResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CompetitorErrors.UserProvisioningFailed(userResult.Error.Message);
        }

        await transaction.CommitAsync(cancellationToken);

        return CompetitorMapper.ToDto(competitor, vault);
    }
}
