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
/// <para>
/// The username order is applied to the joined rows and the DTO is built after it. Sorting a projection
/// the record constructor produced is untranslatable — EF cannot see a constructor argument as a column —
/// so composing the sort on top of the projection fails the whole query at runtime rather than at build.
/// </para>
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
                (enrolment, competitor) => new { enrolment, competitor })
            .OrderBy(row => row.competitor.Username)
            .Select(row => new SessionCompetitorDto(
                row.competitor.Id,
                row.competitor.Username,
                row.competitor.FullName,
                row.competitor.CountryCode,
                row.competitor.IpAddress,
                row.competitor.MobileIpAddress,
                row.enrolment.Ordinal,
                row.enrolment.ProvisionStatus))
            .ToListAsync(cancellationToken);

        return competitors;
    }
}
