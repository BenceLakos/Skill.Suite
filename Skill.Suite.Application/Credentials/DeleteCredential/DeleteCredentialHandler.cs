using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials;

namespace Skill.Suite.Application.Credentials.DeleteCredential;

public sealed class DeleteCredentialHandler(IAppDbContext db) : IRequestHandler<DeleteCredentialCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteCredentialCommand request, CancellationToken cancellationToken)
    {
        var credential = await db.Credentials.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (credential is null)
            return Result.Failure(CredentialErrors.NotFound(request.Id));

        credential.MarkRemoved();
        db.Credentials.Remove(credential);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
