namespace Skill.Suite.Application.Credentials;

using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Credentials;

/// <summary>
/// Reads a stored credential back into the username and secret an external system is called with.
/// </summary>
/// <remarks>
/// Competitor account provisioning has no per-operation credential to point at the way a session does — it is
/// an admin pressing a button on a grid — so it takes whichever credential of that kind is configured. Ordering
/// by name makes "whichever" deterministic: with two Gitea credentials in the list the same one is picked every
/// time rather than whatever the database happened to return first.
/// </remarks>
internal static class CredentialLookup
{
    /// <summary>
    /// The credential a session names by id, or null when the row it names is gone.
    /// </summary>
    /// <remarks>
    /// A missing row is not the same as no credential selected, and only the caller knows which of the two it
    /// is holding — a null id means nothing was chosen, while a null result for a non-null id means the
    /// credential was deleted after the session was configured. That distinction is why this returns null
    /// rather than deciding for the caller.
    /// </remarks>
    public static async Task<BasicCredential?> FindByIdAsync(
        this IAppDbContext db,
        IPasswordVault vault,
        Guid id,
        CancellationToken cancellationToken)
    {
        var credential = await db.Credentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return credential is null ? null : BasicCredential.Parse(vault.Unprotect(credential.EncryptedSecret));
    }

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
