namespace Skill.Suite.Application.Sessions.StopMarking;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Takes down a session's marking containers and leaves everything else where it is.
/// </summary>
/// <remarks>
/// Filtered on the marking label, which carries the session slug as its VALUE. That is what makes one label
/// filter enough to be exact: it cannot reach another session's marking run, and it cannot reach this
/// session's competition containers, which never carry the label at all.
/// <para>
/// Allowed whatever the session's status, unlike starting marking. This only ever removes containers that a
/// marking run created, so it is a cleanup that should never be refused — least of all on a session
/// somebody reopened while marking containers were still holding its host ports.
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
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (session is null)
            return SessionErrors.NotFound(request.Id);

        try
        {
            var removed = await containerServices.RemoveByLabelAsync(
                SessionServiceLabels.MarkingKey, session.Slug, cancellationToken);

            logger.LogInformation(
                "Session {SessionId}: {Removed} marking container(s) removed.", session.Id, removed);

            return new StopMarkingResult(removed, ServiceRemovalError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "The marking containers of session {SessionId} could not be removed", session.Id);

            return new StopMarkingResult(NothingRemoved, ExternalMessage.Trim(ex.Message));
        }
    }

    private const int NothingRemoved = 0;
}
