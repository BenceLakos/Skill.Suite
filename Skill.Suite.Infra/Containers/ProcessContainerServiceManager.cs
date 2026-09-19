namespace Skill.Suite.Infra.Containers;

using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;

/// <summary>
/// <see cref="IContainerServiceManager"/> over the <c>docker</c> CLI, the same way
/// <see cref="ProcessContainerRunner"/> runs judgements.
/// </summary>
/// <remarks>
/// The CLI rather than the HTTP API because the application already reaches the daemon through the mounted
/// <c>/var/run/docker.sock</c> and the CLI is what knows how to talk to it, including the credential handling
/// a private registry needs.
/// </remarks>
internal sealed class ProcessContainerServiceManager(ILogger<ProcessContainerServiceManager> logger)
    : IContainerServiceManager
{
    /// <summary>Asks <c>docker inspect</c> for the one field that decides whether the service is up.</summary>
    private const string RunningStateFormat = "{{.State.Running}}";

    /// <summary>What <see cref="RunningStateFormat"/> prints for a running container.</summary>
    private const string RunningStateTrue = "true";

    public async Task<ContainerServiceStart> EnsureRunningAsync(
        ContainerServiceRequest request,
        CancellationToken cancellationToken)
    {
        var state = await InspectAsync(request.ContainerName, cancellationToken);
        if (state == ContainerRunningState.Running)
            return ContainerServiceStart.AlreadyRunning;

        // A stopped container still owns its name and its published ports, so `docker run` on the same name
        // fails outright. Removing it is also the only way to pick up a changed image or port set: docker
        // cannot reconfigure an existing container.
        var recreated = state == ContainerRunningState.Stopped;
        if (recreated)
        {
            logger.LogWarning(
                "Service container {ContainerName} existed but was not running; recreating it.",
                request.ContainerName);
            await RemoveAsync([request.ContainerName], cancellationToken);
        }

        await using var registryLogin =
            await DockerRegistryLogin.OpenAsync(request.RegistryAuth, logger, cancellationToken);

        var result = await DockerCli.RunAsync(
            DockerServiceArguments.Build(request),
            registryLogin.ConfigDirectory,
            standardInput: null,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new ContainerServiceException(
                $"Starting service container '{request.ContainerName}' from '{request.Image}' " +
                $"exited {result.ExitCode}: {Describe(result)}");
        }

        logger.LogInformation(
            "Service container {ContainerName} started from {Image}.", request.ContainerName, request.Image);

        return recreated ? ContainerServiceStart.Recreated : ContainerServiceStart.Started;
    }

    public async Task<int> RemoveByLabelAsync(
        string labelKey,
        string labelValue,
        CancellationToken cancellationToken)
    {
        var filter = $"label={labelKey}={labelValue}";
        var listed = await DockerCli.RunAsync(
            ["ps", "-a", "-q", "--filter", filter],
            dockerConfigDirectory: null,
            standardInput: null,
            cancellationToken);

        if (listed.ExitCode != 0)
            throw new ContainerServiceException($"Listing containers by {filter} exited {listed.ExitCode}: {Describe(listed)}");

        var ids = listed.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // `docker rm` with no arguments is a usage error, not a no-op, so an empty session is answered here.
        if (ids.Length == 0)
            return 0;

        await RemoveAsync(ids, cancellationToken);

        logger.LogInformation("Removed {Count} service container(s) matching {Filter}.", ids.Length, filter);

        return ids.Length;
    }

    /// <summary>
    /// Whether a container with this name exists and, if so, whether it is running.
    /// </summary>
    /// <remarks>
    /// A non-zero exit means "no such object" rather than a failure — that is how <c>docker inspect</c>
    /// reports an unknown name — so it maps to <see cref="ContainerRunningState.Absent"/> instead of
    /// throwing. Anything else that exists but does not print <c>true</c> is a leftover to be replaced.
    /// </remarks>
    private async Task<ContainerRunningState> InspectAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await DockerCli.RunAsync(
            ["inspect", "--format", RunningStateFormat, containerName],
            dockerConfigDirectory: null,
            standardInput: null,
            cancellationToken);

        if (result.ExitCode != 0)
            return ContainerRunningState.Absent;

        return result.StandardOutput.Trim().Equals(RunningStateTrue, StringComparison.OrdinalIgnoreCase)
            ? ContainerRunningState.Running
            : ContainerRunningState.Stopped;
    }

    private async Task RemoveAsync(IReadOnlyList<string> containers, CancellationToken cancellationToken)
    {
        List<string> arguments = ["rm", "--force", .. containers];

        var result = await DockerCli.RunAsync(
            arguments,
            dockerConfigDirectory: null,
            standardInput: null,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new ContainerServiceException(
                $"Removing service container(s) {string.Join(", ", containers)} " +
                $"exited {result.ExitCode}: {Describe(result)}");
        }
    }

    /// <summary>The daemon's own explanation, falling back to stdout when it said nothing on stderr.</summary>
    private static string Describe(DockerCliResult result)
    {
        var stderr = result.StandardError.Trim();
        return stderr.Length > 0 ? stderr : result.StandardOutput.Trim();
    }
}
