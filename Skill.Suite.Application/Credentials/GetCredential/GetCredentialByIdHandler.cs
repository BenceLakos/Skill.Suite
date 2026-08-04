using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Credentials;

namespace Skill.Suite.Application.Credentials.GetCredential;

public sealed class GetCredentialByIdHandler(IAppDbContext db, IPasswordVault vault)
    : IRequestHandler<GetCredentialByIdQuery, Result<CredentialDto>>
{
    public async ValueTask<Result<CredentialDto>> Handle(GetCredentialByIdQuery request, CancellationToken cancellationToken)
    {
        var credential = await db.Credentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        return credential is null
            ? CredentialErrors.NotFound(request.Id)
            : CredentialMapper.ToDto(credential, vault);
    }
}
