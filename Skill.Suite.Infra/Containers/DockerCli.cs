namespace Skill.Suite.Infra.Containers;

using System.Diagnostics;

/// <summary>
/// Runs one short <c>docker</c> command to completion and collects its exit code and output.
/// </summary>
/// <remarks>
/// Shared by the registry login and by <see cref="ProcessContainerServiceManager"/>, which issues several of
/// these per service. The judgement run itself deliberately does not come through here: it streams stdout
/// line-by-line while the container is still alive, which is the opposite of collecting output at the end.
/// </remarks>
internal static class DockerCli
{
    private const string Executable = "docker";

    private const string ConfigDirectoryVariable = "DOCKER_CONFIG";

    /// <summary>
    /// Invokes <c>docker</c> with <paramref name="arguments"/>, optionally feeding
    /// <paramref name="standardInput"/> to it, and waits for it to exit.
    /// </summary>
    internal static async Task<DockerCliResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? dockerConfigDirectory,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(Executable)
        {
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        ApplyConfigDirectory(psi, dockerConfigDirectory);
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"docker {arguments[0]} could not be started.");

        // Both pipes are drained alongside the wait rather than after it: reading one to the end first
        // deadlocks as soon as the other pipe's buffer fills, and `docker rm` on a long list of ids is
        // exactly the case that fills one.
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync(cancellationToken);

        return new DockerCliResult(process.ExitCode, await stdout, await stderr);
    }

    /// <summary>
    /// Scopes a docker invocation to a private credentials store, so concurrent operations cannot revoke each
    /// other's login and registry credentials never touch the host's own docker config.
    /// </summary>
    internal static void ApplyConfigDirectory(ProcessStartInfo psi, string? dockerConfigDirectory)
    {
        if (dockerConfigDirectory is not null)
            psi.Environment[ConfigDirectoryVariable] = dockerConfigDirectory;
    }
}
