using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Sessions;

namespace Skill.Suite.Application.Sessions;

/// <summary>
/// Returns the id of the currently active session, if any. Used by every test-run
/// list query so competitors and the admin overview default to "what's happening
/// right now" instead of all-time history. Picks the lowest <c>StartsAt</c> when
/// multiple sessions are Active simultaneously — matches the webhook handler's pick.
/// </summary>
public static class ActiveSessionResolver
{
    public static Task<Guid?> ResolveAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        db.Sessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Active)
            .OrderBy(s => s.StartsAt)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
