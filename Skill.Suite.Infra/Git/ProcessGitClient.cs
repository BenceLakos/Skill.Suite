using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.Git;

internal sealed class ProcessGitClient(ILogger<ProcessGitClient> logger) : IGitClient
{
    public async Task CloneAsync(GitCloneRequest request, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(request.TargetDirectory);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);

        var cloneUrl = InjectCredential(request.RepositoryUrl, request.Credential);

        var args = new List<string> { "clone", "--depth", "1" };
        if (!string.IsNullOrWhiteSpace(request.Branch))
        {
            args.Add("--branch");
            args.Add(request.Branch);
        }
        args.Add(cloneUrl);
        args.Add(request.TargetDirectory);

        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Prevent git from prompting on stdin if the URL is missing credentials.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await WaitForExitOrKillAsync(process, "clone", cancellationToken);
        var stderr = await stderrTask;
        var stdout = await stdoutTask;

        if (process.ExitCode != 0)
        {
            // The clone URL may contain a secret in userinfo; log only the safe form.
            logger.LogError("git clone failed (exit {ExitCode}) for {Url}: {Stderr}",
                process.ExitCode, request.RepositoryUrl, stderr);
            throw new InvalidOperationException($"git clone exited {process.ExitCode}: {stderr}");
        }

        logger.LogInformation("Cloned {RepoUrl} to {Target}. {Stdout}",
            request.RepositoryUrl, request.TargetDirectory, stdout);
    }

    public async Task PushDirectoryAsync(GitPushDirectoryRequest request, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(request.SourceDirectory))
            throw new DirectoryNotFoundException($"Starter package directory not found: {request.SourceDirectory}");

        // Assembled in a scratch directory rather than in place: the source is the operator's starter
        // package, which may itself be a git working tree, and `git init` there would either fail or
        // commit into their repository.
        var staging = Path.Combine(Path.GetTempPath(), $"skill-suite-push-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try
        {
            CopyTree(request.SourceDirectory, staging);

            var remote = InjectCredential(request.RepositoryUrl, request.Credential);

            await RunAsync(staging, cancellationToken, "init", "--initial-branch", request.Branch);
            // Identity is set per-repository rather than relying on global config: the application container
            // has no git identity, and a commit without one fails.
            await RunAsync(staging, cancellationToken, "config", "user.email", "skill-suite@localhost");
            await RunAsync(staging, cancellationToken, "config", "user.name", "Skill Suite");
            await RunAsync(staging, cancellationToken, "add", "-A");
            await RunAsync(staging, cancellationToken, "commit", "-m", request.CommitMessage);
            await RunAsync(staging, cancellationToken, "remote", "add", "origin", remote);
            await RunAsync(staging, cancellationToken, "push", "-u", "origin", request.Branch);

            logger.LogInformation("Pushed {Source} to {Url} ({Branch})",
                request.SourceDirectory, request.RepositoryUrl, request.Branch);
        }
        finally
        {
            try { Directory.Delete(staging, recursive: true); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not remove staging directory {Dir}", staging); }
        }
    }

    /// <summary>
    /// Copies the starter package, skipping anything that would make the competitor's first commit wrong.
    /// </summary>
    /// <remarks>
    /// <c>.git</c> is excluded so the package's own history does not become the competitor's, and build
    /// output is excluded because a stale <c>bin/</c> is both noise and a way to ship a prebuilt assembly.
    /// The same exclusions the judge applies when it swaps a submission's folder.
    /// </remarks>
    private static void CopyTree(string source, string destination)
    {
        string[] skipped = [".git", "bin", "obj", ".vs", ".idea"];

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            if (IsSkipped(relative, skipped)) continue;
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (IsSkipped(relative, skipped)) continue;

            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static bool IsSkipped(string relativePath, string[] skipped) =>
        relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => skipped.Contains(segment, StringComparer.OrdinalIgnoreCase));

    private async Task RunAsync(string workingDirectory, CancellationToken cancellationToken, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await WaitForExitOrKillAsync(process, args[0], cancellationToken);
        var stderr = await stderrTask;
        await stdoutTask;

        if (process.ExitCode == 0)
            return;

        // args[0] only: a push argument list contains the authenticated remote URL.
        logger.LogError("git {Verb} failed (exit {ExitCode}): {Stderr}", args[0], process.ExitCode, stderr);
        throw new InvalidOperationException($"git {args[0]} exited {process.ExitCode}: {stderr}");
    }

    /// <summary>
    /// Waits for a git invocation, killing it if the wait is cancelled.
    /// </summary>
    /// <remarks>
    /// <c>WaitForExitAsync(token)</c> stops waiting but does not stop the process, and abandoning a git process
    /// is not harmless here: a clone cancelled by a competitor's second push kept fetching into the submission
    /// directory it no longer owned, holding a connection to the git host and workdir space, with nothing left
    /// to reap it. Over a session of repeated pushes those accumulate, and the next attempt at the same folder
    /// races the orphan. The whole tree is killed because git spawns helpers (git-remote-https, askpass).
    /// </remarks>
    private void KillIfRunning(Process process, string verb)
    {
        try
        {
            if (process.HasExited) return;

            process.Kill(entireProcessTree: true);
            logger.LogWarning("Killed the git {Verb} process after cancellation.", verb);
        }
        catch (Exception ex)
        {
            // Losing the race with a process that exited on its own is the expected case, not a problem.
            logger.LogDebug(ex, "Could not kill the git {Verb} process; it had probably already exited.", verb);
        }
    }

    private async Task WaitForExitOrKillAsync(Process process, string verb, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillIfRunning(process, verb);
            throw;
        }
    }

    /// <summary>
    /// Splices a basic-auth userinfo segment into HTTP(S) URLs. SSH urls are returned
    /// unchanged — they negotiate auth via key material outside the URL.
    /// </summary>
    private static string InjectCredential(string url, BasicCredential? credential)
    {
        if (credential is null || string.IsNullOrEmpty(credential.Secret))
            return url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return url;

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            return url;

        var user = Uri.EscapeDataString(credential.Username);
        var pass = Uri.EscapeDataString(credential.Secret);
        var userInfo = string.IsNullOrEmpty(credential.Username) ? pass : $"{user}:{pass}";

        var rebuilt = new UriBuilder(parsed) { UserName = string.Empty, Password = string.Empty };
        // UriBuilder URL-encodes UserInfo, so we set it on the underlying Uri-formatted string
        // to keep our already-escaped values intact.
        var asString = rebuilt.Uri.ToString();
        var schemeEnd = asString.IndexOf("://", StringComparison.Ordinal) + 3;
        return string.Concat(asString.AsSpan(0, schemeEnd), userInfo, "@", asString.AsSpan(schemeEnd));
    }
}
