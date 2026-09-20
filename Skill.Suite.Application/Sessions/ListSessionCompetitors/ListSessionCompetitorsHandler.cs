namespace Skill.Suite.Application.Sessions.ListSessionCompetitors;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Common;

/// <summary>
/// Joins a session's enrolments to the competitor rows they point at.
/// </summary>
/// <remarks>
/// An inner join, so an enrolment whose competitor has since been deleted is left out. There is nothing left
/// to act on for such a row — no username to name containers with, no database of theirs to connect to — and
/// listing it would offer the admin a competitor who cannot be marked.
/// </remarks>
public sealed class ListSessionCompetitorsHandler(IAppDbContext db)
    : IRequestHandler<ListSessionCompetitorsQuery, Result<List<SessionCompetitorDto>>>
{
    public async ValueTask<Result<List<SessionCompetitorDto>>> Handle(
        ListSessionCompetitorsQuery request, CancellationToken cancellationToken)
    {
        var competitors = await db.SessionCompetitors
            .AsNoTracking()
            .Where(enrolment => enrolment.SessionId == request.SessionId)
            .Join(
                db.Competitors.AsNoTracking(),
                enrolment => enrolment.CompetitorId,
                competitor => competitor.Id,
                (enrolment, competitor) => new SessionCompetitorDto(
                    competitor.Id,
                    competitor.Username,
                    competitor.FullName,
                    competitor.IpAddress,
                    enrolment.Ordinal,
                    enrolment.ProvisionStatus))
            .OrderBy(competitor => competitor.Username)
            .ToListAsync(cancellationToken);

        return competitors;
    }
}
