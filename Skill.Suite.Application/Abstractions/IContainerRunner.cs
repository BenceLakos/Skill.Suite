namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// Runs a docker container and streams its stdout line-by-line. Each non-empty line is
/// invoked on <paramref name="onStdoutLine"/> as soon as it is read so the caller can
/// parse JSON log events while the container is still running.
/// </summary>
public interface IContainerRunner
{
    Task<ContainerRunResult> RunAsync(
        ContainerRunRequest request,
        Func<string, CancellationToken, Task> onStdoutLine,
        CancellationToken cancellationToken);
}

public sealed record ContainerRunRequest(
    string Image,
    string ContainerName,
    string WorkdirVolumeName,
    string WorkdirSubpath,
    string ContainerWorkdir,
    RegistryAuth? RegistryAuth = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    ContainerLogMount? LogMount = null,
    ContainerLimits? Limits = null);

/// <summary>
/// Writable mount the judgement image writes its JSON-lines event log into. The runner
/// emits a second <c>--mount</c> with this subpath at <see cref="ContainerPath"/> inside
/// the container; the volume itself is shared with the source mount so a single workdir
/// volume covers both directions of traffic.
/// </summary>
public sealed record ContainerLogMount(string VolumeName, string Subpath, string ContainerPath);

/// <summary>
/// Credentials for <c>docker login</c> before pulling the image. The server is derived
/// from the image reference's leading hostname (e.g. "nexus.example.com" from
/// "nexus.example.com/team/app:tag") — for docker.io images, leave <see cref="Server"/>
/// null and the daemon defaults to the public registry.
/// </summary>
public sealed record RegistryAuth(string? Server, string Username, string Password);

public sealed record ContainerRunResult(int ExitCode, string? StandardError, bool WasStopped);
