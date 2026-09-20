namespace Skill.Suite.Application.Sessions.StopSession;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Application.Credentials;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Stops an active session and withdraws what competitors reach it through.
/// </summary>
/// <remarks>
/// The session's shared database is deliberately left alone: the grants stay, and so does everything in it.
/// A stop is a pause, and a competitor whose SQL login was revoked and re-granted loses the connections and
/// the session state they had open — while the repository access has to go, because that is the only thing
/// stopping them committing more work while the session is suspended.
/// </remarks>
public sealed class StopSessionHandler(
    IAppDbContext db,
    IGitHostClient gitHost,
    IContainerServiceManager containerServices,
    IPasswordVault vault,
    ILogger<StopSessionHandler> logger)
    : IRequestHandler<StopSessionCommand, Result<StopSessionResult>>
{
    public async ValueTask<Result<StopSessionResult>> Handle(
        StopSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.Sessions
            .Include(s => s.Competitors)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (session is null)
            return SessionErrors.NotFound(request.Id);

        // Resolved before the status is touched, so a session whose git credential has been deleted fails
        // closed: stopping it without revoking anything would report a suspended competition in which every
        // competitor can still push.
        var admin = session.GitCredentialId is null
            ? null
            : await db.FindByIdAsync(vault, session.GitCredentialId.Value, cancellationToken);

        if (admin is null)
            return SessionErrors.MissingGitCredential;

        var stopped = session.Stop();
        if (stopped.IsFailure)
            return Result.Failure<StopSessionResult>(stopped.Error);

        // Committed before anything external is touched, the same reasoning as closing: the status is what
        // stops the webhook accepting pushes, so it must not depend on a git host or a docker daemon being
        // reachable. Whatever this run fails to withdraw is withdrawn by starting and stopping the session again.
        await db.SaveChangesAsync(cancellationToken);

        var failures = new List<SessionProvisioningFailure>();

        var access = await RevokeRepositoryAccessAsync(request, session, admin, cancellationToken);
        failures.AddRange(access.Failures);

        var services = await StopServicesAsync(request, session, cancellationToken);
        failures.AddRange(services.Failures);

        logger.LogInformation(
            "Session {SessionId} stopped: repository access revoked for {Revoked} competitors in {Org}, " +
            "{Stopped} of {ConfiguredServices} services stopped, {Failed} failures",
            session.Id, access.Succeeded, session.GitOrganization,
            services.Succeeded, session.DockerImages.Count, failures.Count);

        return new StopSessionResult(access.Succeeded, services.Succeeded, failures);
    }

    /// <summary>
    /// Takes every provisioned competitor's access to their own repository away.
    /// </summary>
    /// <remarks>
    /// Only the provisioned enrolments, because the rest never had a repository to be a collaborator on.
    /// The repository name equals the username by construction, but the username is read from the competitor
    /// row regardless: the two are set independently, and revoking under the wrong name silently leaves the
    /// real collaborator in place.
    /// </remarks>
    private async Task<SessionProvisioningStageOutcome> RevokeRepositoryAccessAsync(
        StopSessionCommand request,
        Session session,
        BasicCredential admin,
        CancellationToken cancellationToken)
    {
        var enrolments = session.Competitors
            .Where(c => c.ProvisionStatus == SessionProvisionStatus.Provisioned)
            .ToList();

        if (enrolments.Count == 0)
            return EmptyStage;

        var competitorIds = enrolments.Select(c => c.CompetitorId).ToList();

        var usernames = await db.Competitors
            .AsNoTracking()
            .Where(c => competitorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Username, cancellationToken);

        var failures = new List<SessionProvisioningFailure>();
        var total = enrolments.Count;
        var completed = 0;
        var revoked = 0;

        Report(request, new SessionProvisioningProgress(
            SessionProvisioningStage.RepositoryAccess, completed, total));

        foreach (var enrolment in enrolments)
        {
            if (usernames.TryGetValue(enrolment.CompetitorId, out var username))
            {
                var repository = new RepositoryReference(session.GitOrganization, enrolment.RepositoryName);

                try
                {
                    var removal = await gitHost.RemoveCollaboratorAsync(
                        repository, username, admin, cancellationToken);

                    logger.LogInformation(
                        "Session {SessionId}: access of {Username} to {Owner}/{Name}: {Removal}",
                        session.Id, username, repository.Owner, repository.Name, removal);

                    revoked++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One competitor's revocation failing must not leave the rest of them with access.
                    logger.LogError(ex,
                        "Could not revoke {Username}'s access to {Owner}/{Name}",
                        username, repository.Owner, repository.Name);

                    failures.Add(new SessionProvisioningFailure(
                        SessionProvisioningStage.RepositoryAccess,
                        username,
                        ExternalMessage.Trim(ex.Message)));
                }
            }
            else
            {
                // The competitor row is gone, which is how competitors are removed: their git account goes
                // with it, and an account that no longer exists holds no grant. Nothing to revoke, and
                // nothing the admin has to act on.
                logger.LogInformation(
                    "Session {SessionId}: enrolment {Repository} has no competitor row left; " +
                    "there is no account to revoke access from",
                    session.Id, enrolment.RepositoryName);
            }

            // Counted whether access was revoked or not: the bar tracks competitors dealt with, and a
            // failure the admin is told about afterwards must not leave it stalled short of the end.
            completed++;
            Report(request, new SessionProvisioningProgress(
                SessionProvisioningStage.RepositoryAccess, completed, total));
        }

        return new SessionProvisioningStageOutcome(revoked, failures);
    }

    /// <summary>
    /// Stops the session's service containers, leaving them in place to be started again.
    /// </summary>
    /// <remarks>
    /// Found by the name the start derived from the slug and the image's position, not by label: unlike
    /// closing, this is not a cleanup, so it acts on exactly the services the session is configured with.
    /// </remarks>
    private async Task<SessionProvisioningStageOutcome> StopServicesAsync(
        StopSessionCommand request, Session session, CancellationToken cancellationToken)
    {
        var images = session.DockerImages;
        if (images.Count == 0)
            return EmptyStage;

        var failures = new List<SessionProvisioningFailure>();
        var total = images.Count;
        var stopped = 0;

        Report(request, new SessionProvisioningProgress(
            SessionProvisioningStage.StoppingServices, NothingSucceeded, total));

        for (var index = 0; index < total; index++)
        {
            var image = images[index];

            try
            {
                var outcome = await containerServices.StopAsync(
                    SessionServiceNaming.ContainerName(session.Slug, index), cancellationToken);

                logger.LogInformation(
                    "Session {SessionId} service {Image}: {Outcome}", session.Id, image.Image, outcome);

                stopped++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One service refusing to stop must not leave the others running.
                logger.LogError(ex,
                    "Could not stop the service {Image} of session {SessionId}", image.Image, session.Id);

                failures.Add(new SessionProvisioningFailure(
                    SessionProvisioningStage.StoppingServices,
                    image.Image,
                    ExternalMessage.Trim(ex.Message)));
            }

            Report(request, new SessionProvisioningProgress(
                SessionProvisioningStage.StoppingServices, index + 1, total));
        }

        return new SessionProvisioningStageOutcome(stopped, failures);
    }

    /// <summary>
    /// Hands a report to the caller's sink, if it supplied one.
    /// </summary>
    /// <remarks>
    /// Every sink fault is logged and swallowed, for the reason the start handler's own <c>Report</c>
    /// explains: the sink belongs to a Blazor circuit that may already have gone away, and losing a progress
    /// update is never worth abandoning a half-stopped session for.
    /// </remarks>
    private void Report(StopSessionCommand request, SessionProvisioningProgress progress)
    {
        if (request.Progress is null)
            return;

        try
        {
            request.Progress.Report(progress);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A stop-session progress report could not be delivered");
        }
    }

    private const int NothingSucceeded = 0;

    /// <summary>A stage with nothing to act on: nothing attempted, nothing to report.</summary>
    private static readonly SessionProvisioningStageOutcome EmptyStage = new(NothingSucceeded, []);
}
