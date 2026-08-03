using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.Containers;

internal sealed class ProcessContainerRunner(ILogger<ProcessContainerRunner> logger) : IContainerRunner
{
    private const int StopGraceSeconds = 5;

    public async Task<ContainerRunResult> RunAsync(
        ContainerRunRequest request,
        Func<string, CancellationToken, Task> onStdoutLine,
        CancellationToken cancellationToken)
    {
        // If the registry needs auth, log in before docker run pulls.
        //
        // The credentials go into a throwaway DOCKER_CONFIG directory rather than the daemon's shared store.
        // That is what makes concurrent runs safe: `docker login` and `docker logout` both edit one global
        // config file, so with a shared store one run's logout would revoke the credentials another run was
        // in the middle of pulling with. It also keeps competition registry credentials out of the host's
        // own docker config entirely.
        string? dockerConfigDir = null;
        if (request.RegistryAuth is not null)
        {
            dockerConfigDir = Directory.CreateTempSubdirectory("skill-suite-docker-").FullName;
            await DockerLoginAsync(request.RegistryAuth, dockerConfigDir, cancellationToken);
        }

        try
        {
            return await RunInternalAsync(request, dockerConfigDir, onStdoutLine, cancellationToken);
        }
        finally
        {
            if (dockerConfigDir is not null)
            {
                await TryDockerLogoutAsync(request.RegistryAuth!.Server, dockerConfigDir);

                // Deleting the directory is the real revocation; the logout above just keeps any credential
                // helper informed.
                try { Directory.Delete(dockerConfigDir, recursive: true); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not remove the temporary docker config at {Path}", dockerConfigDir);
                }
            }
        }
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
        ApplyDockerConfig(psi, dockerConfigDir);
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
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderr.AppendLine(e.Data);
            lines.Writer.TryWrite(e.Data);
        };

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
    /// Scopes a docker invocation to a private credentials store, so concurrent runs cannot revoke each
    /// other's login and registry credentials never touch the host's own docker config.
    /// </summary>
    private static void ApplyDockerConfig(ProcessStartInfo psi, string? dockerConfigDir)
    {
        if (dockerConfigDir is not null)
            psi.Environment["DOCKER_CONFIG"] = dockerConfigDir;
    }

    private async Task DockerLoginAsync(RegistryAuth auth, string dockerConfigDir, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("docker")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        ApplyDockerConfig(psi, dockerConfigDir);
        psi.ArgumentList.Add("login");
        psi.ArgumentList.Add("-u");
        psi.ArgumentList.Add(auth.Username);
        psi.ArgumentList.Add("--password-stdin");
        if (!string.IsNullOrWhiteSpace(auth.Server))
            psi.ArgumentList.Add(auth.Server);

        using var login = Process.Start(psi)
            ?? throw new InvalidOperationException("docker login could not be started.");

        await login.StandardInput.WriteAsync(auth.Password);
        login.StandardInput.Close();

        await login.WaitForExitAsync(cancellationToken);

        if (login.ExitCode != 0)
        {
            var stderr = await login.StandardError.ReadToEndAsync(cancellationToken);
            logger.LogError("docker login failed (exit {ExitCode}) for {Server}: {Stderr}",
                login.ExitCode, auth.Server ?? "<default>", stderr);
            throw new InvalidOperationException($"docker login exited {login.ExitCode}.");
        }
    }

    private async Task TryDockerLogoutAsync(string? server, string dockerConfigDir)
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
            ApplyDockerConfig(psi, dockerConfigDir);
            psi.ArgumentList.Add("logout");
            if (!string.IsNullOrWhiteSpace(server))
                psi.ArgumentList.Add(server);

            using var logout = Process.Start(psi);
            if (logout is null) return;
            await logout.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "docker logout failed for {Server}", server ?? "<default>");
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
