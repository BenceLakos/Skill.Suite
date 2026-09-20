namespace Skill.Suite.Application.Sessions.CloseSession;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

public sealed class CloseSessionHandler(
    IAppDbContext db,
    IContainerServiceManager containerServices,
    ILogger<CloseSessionHandler> logger)
    : IRequestHandler<CloseSessionCommand, Result<CloseSessionResult>>
{
    public async ValueTask<Result<CloseSessionResult>> Handle(
        CloseSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (session is null)
            return SessionErrors.NotFound(request.Id);

        var closed = session.Close();
        if (closed.IsFailure)
            return Result.Failure<CloseSessionResult>(closed.Error);

        // Committed before the containers are touched. The status is what stops the session accepting pushes,
        // so it must not depend on a docker daemon being reachable — and removal is by label, which finds the
        // containers again on a later attempt whether or not this one got through.
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            // One label, deliberately: closing takes down everything the session ever started, competition
            // containers and any marking containers left behind alike, because both carry the session label.
            var removed = await containerServices.RemoveByLabelsAsync(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SessionServiceLabels.SessionKey] = session.Slug,
                },
                cancellationToken);

            logger.LogInformation(
                "Session {SessionId} closed; {Removed} service container(s) removed.", session.Id, removed);

            return new CloseSessionResult(removed, ServiceRemovalError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Session {SessionId} was closed but its service containers could not be removed", session.Id);

            return new CloseSessionResult(NothingRemoved, ExternalMessage.Trim(ex.Message));
        }
    }

    private const int NothingRemoved = 0;
}
