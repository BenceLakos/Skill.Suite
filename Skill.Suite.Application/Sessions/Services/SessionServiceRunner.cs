namespace Skill.Suite.Application.Sessions.Services;

using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Application.DockerImages;

/// <summary>
/// Brings a planned set of containers up, one at a time, reporting as it goes.
/// </summary>
/// <remarks>
/// Shared by starting a session and starting marking because the loop is the same loop: the two differ in
/// what the plan says, not in how it is carried out, and a second copy of this would be the place where
/// marking quietly stopped treating "already running" as success.
/// <para>
/// No container aborts another. A service the daemon refuses is recorded against the competitor or image it
/// belongs to and the rest are started regardless — the alternative is a session where nineteen competitors
/// have no service because the twentieth's port was taken.
/// </para>
/// </remarks>
internal static class SessionServiceRunner
{
    public static async Task<SessionServiceRunOutcome> RunAsync(
        IContainerServiceManager containerServices,
        ILogger logger,
        SessionServicePlan plan,
        SessionProvisioningStage stage,
        Guid sessionId,
        BasicCredential? pullCredential,
        Action<SessionProvisioningProgress> report,
        CancellationToken cancellationToken)
    {
        // The plan's own failures come first: a host port that leaves the port range is already decided, and
        // hiding it behind the containers that did start would leave that competitor unexplained.
        var failures = new List<SessionProvisioningFailure>(plan.Failures);
        var running = new List<PlannedSessionService>();

        var total = plan.Services.Count;
        report(new SessionProvisioningProgress(stage, NothingStarted, total));

        for (var index = 0; index < total; index++)
        {
            var service = plan.Services[index];

            if (!string.Equals(service.Image, service.ConfiguredImage, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Session {SessionId} pulls {DaemonImage}; {StoredImage} names a host only the docker "
                    + "network resolves.",
                    sessionId, service.Image, service.ConfiguredImage);
            }

            try
            {
                var outcome = await containerServices.EnsureRunningAsync(
                    new ContainerServiceRequest(
                        service.Image,
                        service.ContainerName,
                        service.Environment,
                        service.Labels,
                        service.Volumes,
                        service.PortMappings,
                        RegistryAuthFactory.ForHostedImage(pullCredential, service.Image)),
                    cancellationToken);

                logger.LogInformation(
                    "Session {SessionId} service {ContainerName} from {Image}: {Outcome}",
                    sessionId, service.ContainerName, service.ConfiguredImage, outcome);

                running.Add(service);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Could not start the service container {ContainerName} for session {SessionId}",
                    service.ContainerName, sessionId);

                failures.Add(new SessionProvisioningFailure(
                    stage, service.Subject, ExternalMessage.Trim(ex.Message)));
            }

            report(new SessionProvisioningProgress(stage, index + 1, total));
        }

        return new SessionServiceRunOutcome(
            new SessionProvisioningStageOutcome(running.Count, failures), running);
    }

    private const int NothingStarted = 0;
}
