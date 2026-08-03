using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Credentials.ListCredentials;

public sealed class ListCredentialsHandler(IAppDbContext db, IPasswordVault vault)
    : IRequestHandler<ListCredentialsQuery, Result<List<CredentialDto>>>
{
    public async ValueTask<Result<List<CredentialDto>>> Handle(ListCredentialsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Credentials.AsNoTracking();

        if (request.Kind.HasValue)
            query = query.Where(c => c.Kind == request.Kind.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        var credentials = await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return CredentialMapper.ToDtoList(credentials, vault);
    }
}
