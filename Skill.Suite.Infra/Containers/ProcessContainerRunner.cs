using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.Containers;

internal sealed class ProcessContainerRunner(ILogger<ProcessContainerRunner> logger) : IContainerRunner
{
    /// <summary>
    /// Cap on the raw stderr copy kept for <c>FailureReason</c>.
    /// </summary>
    /// <remarks>
    /// Generous enough for a full MSBuild error list or a stack trace, small enough that a competitor cannot
    /// use it to exhaust the application's memory. Judgement containers that need to say more than this are
    /// misusing stderr — the event stream is the channel for detail.
    /// </remarks>
    private const int MaxStderrCharacters = 64 * 1024;

    private const int StopGraceSeconds = 5;

    public async Task<ContainerRunResult> RunAsync(
        ContainerRunRequest request,
        Func<string, CancellationToken, Task> onStdoutLine,
        CancellationToken cancellationToken)
    {
        // If the registry needs auth, log in before docker run pulls. The scope is inert when it does not,
        // and disposing it is what revokes the credentials again.
        await using var registryLogin =
            await DockerRegistryLogin.OpenAsync(request.RegistryAuth, logger, cancellationToken);

        return await RunInternalAsync(request, registryLogin.ConfigDirectory, onStdoutLine, cancellationToken);
    }

    private async Task<ContainerRunResult> RunInternalAsync(
        ContainerRunRequest request,
        string? dockerConfigDir,
        Func<string, CancellationToken, Task> onOutputLine,
        CancellationToken cancellationToken)
    {
        var args = DockerRunArguments.Build(request);

        var psi = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        DockerCli.ApplyConfigDirectory(psi, dockerConfigDir);
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.EnableRaisingEvents = true;

        var stderr = new StringBuilder();

        // The event-based async API is the only reliable way to stream both stdout and
        // stderr from a child process without risking a deadlock when one pipe fills.
        // Both streams are funneled into a single channel so the consumer handler runs
        // serially against the TestRun (no concurrent mutation).
        var lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lines.Writer.TryWrite(e.Data);
        };

        // Mirror stderr through the same parser — some judgement images emit JSON events
        // on stderr (e.g. when they redirect logger output) — and keep a raw copy so we
        // can surface it if the container exits non-zero.
        //
        // The raw copy is CAPPED. It ends up in TestRun.FailureReason, and competitor code chooses what goes
        // into it: a `while(true) Console.Error.WriteLine(...)` grew this buffer until the application process
        // died, which killed every other competitor being judged at the same time. The first bytes are the
        // diagnostic ones — a compiler error, a stack trace — so keeping the head and dropping the tail loses
        // nothing an expert needs.
        var stderrTruncated = false;
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;

            if (stderr.Length < MaxStderrCharacters)
            {
                stderr.AppendLine(e.Data);
            }
            else if (!stderrTruncated)
            {
                stderrTruncated = true;
                stderr.AppendLine($"[truncated at {MaxStderrCharacters} characters]");
            }

            lines.Writer.TryWrite(e.Data);
        };

        // The container name is derived from the run id, so a run the worker adopts after a crash collides with
        // whatever the dead process left behind: `docker run --name` fails outright with "name already in use",
        // failing the recovered run for a reason that has nothing to do with the submission. Best-effort removal
        // of a same-named container makes starting it idempotent the way re-judging needs.
        TryRemoveStaleContainer(request.ContainerName);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var wasStopped = false;
        await using var cancellation = cancellationToken.Register(() =>
        {
            wasStopped = true;
            TryDockerStop(request.ContainerName);
        });

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var line in lines.Reader.ReadAllAsync())
            {
                try
                {
                    await onOutputLine(line, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to handle judgement output line: {Line}", line);
                }
            }
        });

        // WaitForExitAsync (without a token) waits for the process and drains the async
        // output readers — only after this can we safely read ExitCode.
        await process.WaitForExitAsync(CancellationToken.None);

        // Close the channel so the consumer's `await foreach` terminates.
        lines.Writer.TryComplete();
        await consumerTask;

        logger.LogInformation(
            "Container {ContainerName} exited with code {ExitCode} (wasStopped={WasStopped}).",
            request.ContainerName, process.ExitCode, wasStopped);

        return new ContainerRunResult(process.ExitCode, stderr.Length == 0 ? null : stderr.ToString(), wasStopped);
    }

    /// <summary>
    /// Removes a container left over from an earlier attempt at the same run, if one exists.
    /// </summary>
    /// <remarks>
    /// Safe to call unconditionally: <c>docker rm -f</c> on a name that does not exist is a no-op with a
    /// non-zero exit, which is why nothing is thrown here. It cannot remove a container belonging to a
    /// different run, because the name is the run's own id.
    /// </remarks>
    private void TryRemoveStaleContainer(string containerName)
    {
        try
        {
            var psi = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("rm");
            psi.ArgumentList.Add("--force");
            psi.ArgumentList.Add(containerName);

            using var remove = Process.Start(psi);
            if (remove is null) return;

            remove.WaitForExit(TimeSpan.FromSeconds(StopGraceSeconds + 5));
            if (remove.ExitCode == 0)
                logger.LogWarning("Removed a stale container named {ContainerName} before starting this run.",
                    containerName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not check for a stale container named {ContainerName}", containerName);
        }
    }

    private void TryDockerStop(string containerName)
    {
        try
        {
            var psi = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("stop");
            psi.ArgumentList.Add("--time");
            psi.ArgumentList.Add(StopGraceSeconds.ToString());
            psi.ArgumentList.Add(containerName);

            using var stop = Process.Start(psi);
            stop?.WaitForExit(TimeSpan.FromSeconds(StopGraceSeconds + 5));
            logger.LogInformation("Stopped container {ContainerName} (cancellation requested).", containerName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "docker stop failed for container {ContainerName}", containerName);
        }
    }
}
