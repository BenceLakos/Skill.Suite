namespace Skill.Suite.Application.Credentials;

using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Credentials;

/// <summary>
/// Resolves the single credential a whole system is administered with, by kind.
/// </summary>
/// <remarks>
/// Competitor account provisioning has no per-operation credential to point at the way a session does — it is
/// an admin pressing a button on a grid — so it takes whichever credential of that kind is configured. Ordering
/// by name makes "whichever" deterministic: with two Gitea credentials in the list the same one is picked every
/// time rather than whatever the database happened to return first.
/// </remarks>
internal static class CredentialLookup
{
    public static async Task<BasicCredential?> FindByKindAsync(
        this IAppDbContext db,
        IPasswordVault vault,
        CredentialKind kind,
        CancellationToken cancellationToken)
    {
        var credential = await db.Credentials
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .FirstOrDefaultAsync(c => c.Kind == kind, cancellationToken);

        return credential is null ? null : BasicCredential.Parse(vault.Unprotect(credential.EncryptedSecret));
    }
}
