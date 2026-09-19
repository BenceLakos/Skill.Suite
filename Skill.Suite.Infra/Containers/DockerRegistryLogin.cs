namespace Skill.Suite.Infra.Containers;

using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;

/// <summary>
/// A registry login held in a throwaway <c>DOCKER_CONFIG</c> directory for the length of one docker operation,
/// and revoked when the scope is disposed.
/// </summary>
/// <remarks>
/// The credentials go into a private config directory rather than the daemon's shared store. That is what
/// makes concurrent operations safe: <c>docker login</c> and <c>docker logout</c> both edit one global config
/// file, so with a shared store one operation's logout would revoke the credentials another was in the middle
/// of pulling with. It also keeps competition registry credentials out of the host's own docker config
/// entirely.
/// <para>
/// A request without credentials still gets a scope, an inert one whose <see cref="ConfigDirectory"/> is null:
/// the daemon then uses its usual configuration, which is what a public image needs.
/// </para>
/// </remarks>
internal sealed class DockerRegistryLogin(RegistryAuth? auth, ILogger logger, string? configDirectory)
    : IAsyncDisposable
{
    private const string TempDirectoryPrefix = "skill-suite-docker-";

    /// <summary>Stands in for the public registry in log messages, where <c>Server</c> is null.</summary>
    private const string DefaultServerLabel = "<default>";

    /// <summary>The private credentials store for this operation, or null when no login was needed.</summary>
    internal string? ConfigDirectory { get; } = configDirectory;

    /// <summary>Logs in, if there is anything to log in with, and returns the scope to dispose afterwards.</summary>
    internal static async Task<DockerRegistryLogin> OpenAsync(
        RegistryAuth? auth,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (auth is null)
            return new DockerRegistryLogin(auth: null, logger, configDirectory: null);

        var directory = Directory.CreateTempSubdirectory(TempDirectoryPrefix).FullName;
        try
        {
            await LoginAsync(auth, directory, logger, cancellationToken);
        }
        catch
        {
            TryDelete(directory, logger);
            throw;
        }

        return new DockerRegistryLogin(auth, logger, directory);
    }

    public async ValueTask DisposeAsync()
    {
        if (ConfigDirectory is null) return;

        await TryLogoutAsync(auth?.Server, ConfigDirectory, logger);

        // Deleting the directory is the real revocation; the logout above just keeps any credential helper
        // informed.
        TryDelete(ConfigDirectory, logger);
    }

    private static async Task LoginAsync(
        RegistryAuth auth,
        string configDirectory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "login", "-u", auth.Username, "--password-stdin" };
        if (!string.IsNullOrWhiteSpace(auth.Server))
            arguments.Add(auth.Server);

        var result = await DockerCli.RunAsync(arguments, configDirectory, auth.Password, cancellationToken);
        if (result.ExitCode == 0) return;

        logger.LogError("docker login failed (exit {ExitCode}) for {Server}: {Stderr}",
            result.ExitCode, auth.Server ?? DefaultServerLabel, result.StandardError);
        throw new InvalidOperationException($"docker login exited {result.ExitCode}.");
    }

    /// <summary>Best-effort logout, deliberately outside the operation's cancellation.</summary>
    /// <remarks>
    /// A cancelled run is the case where revoking matters most, so the logout must not be cancelled along
    /// with it; the directory delete below is what actually revokes, and this only informs a credential
    /// helper that may hold the login elsewhere.
    /// </remarks>
    private static async Task TryLogoutAsync(string? server, string configDirectory, ILogger logger)
    {
        try
        {
            var arguments = new List<string> { "logout" };
            if (!string.IsNullOrWhiteSpace(server))
                arguments.Add(server);

            await DockerCli.RunAsync(arguments, configDirectory, standardInput: null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "docker logout failed for {Server}", server ?? DefaultServerLabel);
        }
    }

    private static void TryDelete(string configDirectory, ILogger logger)
    {
        try
        {
            Directory.Delete(configDirectory, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not remove the temporary docker config at {Path}", configDirectory);
        }
    }
}
