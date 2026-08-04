using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials;

namespace Skill.Suite.Application.Credentials.CreateCredential;

public sealed class CreateCredentialHandler(IAppDbContext db, IPasswordVault vault)
    : IRequestHandler<CreateCredentialCommand, Result<CredentialDto>>
{
    public async ValueTask<Result<CredentialDto>> Handle(CreateCredentialCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var taken = await db.Credentials.AnyAsync(c => c.Name == name, cancellationToken);
        if (taken)
            return CredentialErrors.NameConflict;

        var credential = Credential.Create(name, request.Kind, vault.Protect(request.Secret));
        db.Credentials.Add(credential);
        await db.SaveChangesAsync(cancellationToken);

        return CredentialMapper.ToDto(credential, vault);
    }
}
