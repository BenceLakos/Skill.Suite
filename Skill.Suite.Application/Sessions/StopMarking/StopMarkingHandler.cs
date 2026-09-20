namespace Skill.Suite.Application.Sessions.StopMarking;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Takes down marking containers and leaves everything else where it is.
/// </summary>
/// <remarks>
/// Filtered on the marking label, which carries the session slug as its VALUE, plus — for one competitor —
/// the competitor label. Two filters are an AND on the daemon, and both are needed: the marking label alone
/// would take down every expert's run on the session, and the competitor label alone would take down that
/// competitor's COMPETITION containers on a session that is somehow running again.
/// <para>
/// Allowed whatever the session's status, unlike starting marking. This only ever removes containers a
/// marking run created, so it is a cleanup that should never be refused — least of all on a session somebody
/// reopened while marking containers were still holding its host ports.
/// </para>
/// </remarks>
public sealed class StopMarkingHandler(
    IAppDbContext db,
    IContainerServiceManager containerServices,
    ILogger<StopMarkingHandler> logger)
    : IRequestHandler<StopMarkingCommand, Result<StopMarkingResult>>
{
    public async ValueTask<Result<StopMarkingResult>> Handle(
        StopMarkingCommand request, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .AsNoTracking()
            .Include(s => s.Competitors)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (session is null)
            return SessionErrors.NotFound(request.Id);

        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SessionServiceLabels.MarkingKey] = session.Slug,
        };

        string? username = null;

        if (request.CompetitorId is { } competitorId)
        {
            if (session.Competitors.All(c => c.CompetitorId != competitorId))
                return SessionErrors.CompetitorNotEnrolled;

            username = await db.Competitors
                .AsNoTracking()
                .Where(c => c.Id == competitorId)
                .Select(c => c.Username)
                .FirstOrDefaultAsync(cancellationToken);

            // The competitor row is gone, which is how competitors are removed. Their containers are
            // labelled with the username that row held, and there is nothing left to reconstruct it from —
            // so stopping all marking for the session is the only thing that can still reach them.
            if (username is null)
                return SessionErrors.CompetitorNotEnrolled;

            labels[SessionServiceLabels.CompetitorKey] = username;
        }

        try
        {
            var removed = await containerServices.RemoveByLabelsAsync(labels, cancellationToken);

            logger.LogInformation(
                "Session {SessionId}: {Removed} marking container(s) removed for {Scope}.",
                session.Id, removed, username ?? WholeSession);

            return new StopMarkingResult(username, removed, ServiceRemovalError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "The marking containers of session {SessionId} could not be removed for {Scope}",
                session.Id, username ?? WholeSession);

            return new StopMarkingResult(username, NothingRemoved, ExternalMessage.Trim(ex.Message));
        }
    }

    /// <summary>Stands in for the competitor in the log line when every one of them is being stopped.</summary>
    private const string WholeSession = "the whole session";

    private const int NothingRemoved = 0;
}
