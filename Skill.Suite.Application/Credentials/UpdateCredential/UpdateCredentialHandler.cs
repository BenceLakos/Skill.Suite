using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials;

namespace Skill.Suite.Application.Credentials.UpdateCredential;

public sealed class UpdateCredentialHandler(IAppDbContext db, IPasswordVault vault)
    : IRequestHandler<UpdateCredentialCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateCredentialCommand request, CancellationToken cancellationToken)
    {
        var credential = await db.Credentials.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (credential is null)
            return Result.Failure(CredentialErrors.NotFound(request.Id));

        var name = request.Name.Trim();
        if (!string.Equals(credential.Name, name, StringComparison.Ordinal))
        {
            var taken = await db.Credentials.AnyAsync(c => c.Id != request.Id && c.Name == name, cancellationToken);
            if (taken)
                return Result.Failure(CredentialErrors.NameConflict);
        }

        credential.Update(name, request.Kind, vault.Protect(request.Secret));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
