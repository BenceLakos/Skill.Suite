using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;

namespace Skill.Suite.Application.Competitors.DeleteCompetitor;

public sealed class DeleteCompetitorHandler(
    IAppDbContext db,
    IUserAccountService userAccounts)
    : IRequestHandler<DeleteCompetitorCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteCompetitorCommand request, CancellationToken cancellationToken)
    {
        var competitor = await db.Competitors.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (competitor is null)
            return Result.Failure(CompetitorErrors.NotFound(request.Id));

        competitor.MarkRemoved();
        db.Competitors.Remove(competitor);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var userResult = await userAccounts.DeleteUserAsync(competitor.Id, cancellationToken);
        if (userResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CompetitorErrors.UserProvisioningFailed(userResult.Error.Message));
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
