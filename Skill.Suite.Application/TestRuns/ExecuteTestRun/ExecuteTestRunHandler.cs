using System.Globalization;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Credentials;
using Skill.Suite.Application.DockerImages;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.ExecuteTestRun;

public sealed class ExecuteTestRunHandler(
    IAppDbContext db,
    IGitClient git,
    IContainerRunner runner,
    IPasswordVault vault,
    IActiveTestRunRegistry registry,
    IOptions<WebhookOptions> options,
    ILogger<ExecuteTestRunHandler> logger)
    : IRequestHandler<ExecuteTestRunCommand, Result>
{
    private const string SupersededReason = TestRunReasons.Superseded;
    private const string NoResultsReason = TestRunReasons.NoResults;
    private const string TimedOutReasonFormat = TestRunReasons.TimedOutFormat;

    /// <summary>Diagnostic recorded when the judge's event log was larger than the ingest limit.</summary>
    private const string TruncatedEventLogDetail =
        "The event log this run produced was larger than the platform will ingest, so only its first events "
        + "were recorded. The results below are incomplete and must not be used as a mark without review.";

    public async ValueTask<Result> Handle(ExecuteTestRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.TestRuns
            .Include(r => r.Fixtures)
            .ThenInclude(f => f.UnitTests)
            .FirstOrDefaultAsync(r => r.Id == request.TestRunId, cancellationToken);

        if (run is null)
            return Result.Failure(TestRunErrors.NotFound(request.TestRunId));

        // The webhook handler may have marked this run as Cancelled in the DB before the
        // worker picked it up (e.g., a second push arrived before we dequeued). Honor
        // that decision and exit early instead of clobbering it.
        if (IsTerminal(run.Status))
        {
            logger.LogInformation(
                "Run {TestRunId} already terminal ({Status}); worker exiting.",
                run.Id, run.Status);
            return Result.Success();
        }

        var session = await db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == run.SessionId, cancellationToken);

        var opts = options.Value;
        var appSubmissionPath = Path.Combine(opts.WorkingDirectory, run.FolderName);
        var containerName = $"testrun-{run.Id:N}"[..Math.Min(63, $"testrun-{run.Id:N}".Length)];

        // The writable log mount lives next to the (readonly) source checkout on the same
        // workdir volume. The judge writes its JSON-lines event log here; the app reads
        // back from it after the container exits.
        var logSubpath = run.FolderName + ".log";
        var appLogDirectory = Path.Combine(opts.WorkingDirectory, logSubpath);
        var appLogFilePath = Path.Combine(appLogDirectory, opts.EventLogFileName);

        // Backstop wall clock for the whole run. A judge image enforces its own per-step budgets, but those
        // cannot cover a slow image pull, a hanging clone, or an image that ignores the shell library — and
        // because the worker is serial, one unbounded run stalls every other competitor.
        using var timeoutCts = new CancellationTokenSource(opts.RunTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var token = linkedCts.Token;

        // Registered with the linked source so a supersede still cancels, but note the registry only holds
        // the CTS - it cannot tell a supersede from a timeout, which is why the two are distinguished below
        // by asking timeoutCts directly.

        if (run.CompetitorId.HasValue)
            registry.Register(run.Id, run.CompetitorId.Value, linkedCts);

        // Stdout is captured as a fallback for images that don't follow the file convention.
        var stdoutBuffer = new List<string>();

        try
        {
            Directory.CreateDirectory(opts.WorkingDirectory);
            // volume-subpath requires the directory to exist on the volume — without this
            // mkdir, docker run fails with "no such file or directory".
            Directory.CreateDirectory(appLogDirectory);

            run.MarkCloning();
            await db.SaveChangesAsync(token);

            // The same row can reach this point twice: the worker's startup recovery pass adopts rows left
            // Cloning or Running by a process that died, and the folder name is stored on the row, so the
            // checkout from the previous attempt is still sitting here. `git clone` refuses a non-empty
            // destination, which turned every recovered run into a Failed one with a raw git error — the exact
            // outcome the recovery pass exists to prevent. Clearing first is what makes re-judging idempotent.
            if (Directory.Exists(appSubmissionPath))
            {
                logger.LogInformation(
                    "Removing the checkout left at {Path} by an earlier attempt at run {TestRunId}.",
                    appSubmissionPath, run.Id);
                Directory.Delete(appSubmissionPath, recursive: true);
            }

            var gitCredential = await ResolveCredentialAsync(session?.GitCredentialId, cancellationToken);
            var cloneUrl = RepositoryUrlRewriter.Rewrite(run.RepositoryUrl, opts.GitInternalBaseUrl);

            // Validated before it becomes a `git clone` argument, not merely rewritten. Rewrite returns the
            // original string untouched when it does not parse as an absolute URI, and git reads a leading dash
            // as an option — so a payload naming `--upload-pack=<command>` as its clone URL would execute that
            // command inside this container. Every other field of that payload is already treated as hostile.
            if (!IsCloneableUrl(cloneUrl))
            {
                run.MarkFailed(TestRunReasons.InvalidRepositoryUrl, DateTime.UtcNow);
                await db.SaveChangesAsync(CancellationToken.None);
                logger.LogError(
                    "Run {TestRunId} rejected: {Url} is not an absolute http(s) repository URL.",
                    run.Id, run.RepositoryUrl);
                return Result.Success();
            }

            await git.CloneAsync(
                new GitCloneRequest(cloneUrl, appSubmissionPath, run.Branch, gitCredential),
                token);

            run.MarkRunning(DateTime.UtcNow);
            await db.SaveChangesAsync(token);

            var registryAuth = await ResolveRegistryAuthAsync(
                session?.JudgementImagePullCredentialId, run.JudgementImage, cancellationToken);

            // Judgement image contract:
            //   COMPETITOR_DIRECTORY → readonly path to the competitor's checkout
            //   LOG_DIRECTORY        → writable directory the judge writes events.jsonl into
            var containerEnv = new Dictionary<string, string>
            {
                ["COMPETITOR_DIRECTORY"] = opts.ContainerWorkdir,
                ["LOG_DIRECTORY"] = opts.ContainerLogDirectory,
            };

            var result = await runner.RunAsync(
                new ContainerRunRequest(
                    Image: run.JudgementImage,
                    ContainerName: containerName,
                    WorkdirVolumeName: opts.WorkdirVolumeName,
                    WorkdirSubpath: run.FolderName,
                    ContainerWorkdir: opts.ContainerWorkdir,
                    RegistryAuth: registryAuth,
                    Environment: containerEnv,
                    LogMount: new ContainerLogMount(
                        VolumeName: opts.WorkdirVolumeName,
                        Subpath: logSubpath,
                        ContainerPath: opts.ContainerLogDirectory),
                    Limits: new ContainerLimits(
                        IsolateNetwork: opts.IsolateJudgeNetwork,
                        Memory: opts.JudgeMemoryLimit,
                        Cpus: opts.JudgeCpuLimit,
                        PidsLimit: opts.JudgePidsLimit)),
                onStdoutLine: (line, _) =>
                {
                    // Buffer only; the file is the authoritative source after the container
                    // exits. Stdout is parsed as fallback below if no file is present.
                    stdoutBuffer.Add(line);
                    return Task.CompletedTask;
                },
                token);

            await ApplyEventLogAsync(run, appLogFilePath, stdoutBuffer, cancellationToken);

            // Force EF to track every newly-built fixture / unit-test as Added. We set
            // application-generated Guid keys in the factories; without an explicit Add()
            // call, EF's graph-attachment heuristic mistakes those for existing rows and
            // emits UPDATE statements that affect 0 rows — the root cause of the
            // DbUpdateConcurrencyException the runner used to throw at the end of a run.
            foreach (var fixture in run.Fixtures)
            {
                db.Add(fixture);
                foreach (var unit in fixture.UnitTests)
                    db.Add(unit);
            }

            TestRunStatus terminalStatus;
            string? terminalReason = null;

            // Order matters: a timeout also trips the linked token, so it has to be checked before the
            // supersede branch or a run the platform killed would be reported to the competitor as
            // "superseded by a newer submission" - which is both wrong and impossible to debug.
            if (timeoutCts.IsCancellationRequested)
            {
                terminalStatus = TestRunStatus.Failed;
                terminalReason = string.Format(
                    CultureInfo.InvariantCulture, TimedOutReasonFormat, opts.RunTimeout.TotalMinutes);
                run.MarkFailed(terminalReason, DateTime.UtcNow);
            }
            else if (result.WasStopped || token.IsCancellationRequested)
            {
                terminalStatus = TestRunStatus.Cancelled;
                terminalReason = SupersededReason;
                run.MarkCancelled(terminalReason, DateTime.UtcNow);
            }
            else if (result.ExitCode == 0 && run.Fixtures.Count == 0)
            {
                // Exit 0 with nothing to show is not success. `dotnet test` returns 1 for both red tests and
                // a compile error, so a judge that tolerates red tests (which white-box sessions need) maps
                // a non-compiling submission to exit 0 as well. Without this guard such a run is recorded
                // Completed with no results, which reads as a clean pass. Also covers an events.jsonl that
                // was created and then left empty.
                terminalStatus = TestRunStatus.Failed;
                terminalReason = NoResultsReason;
                run.MarkFailed(terminalReason, DateTime.UtcNow);
            }
            else if (result.ExitCode == 0)
            {
                terminalStatus = TestRunStatus.Completed;
                run.MarkCompleted(DateTime.UtcNow);
            }
            else
            {
                terminalStatus = TestRunStatus.Failed;
                terminalReason = $"Judgement container exited with code {result.ExitCode}. {result.StandardError}".TrimEnd();
                run.MarkFailed(terminalReason, DateTime.UtcNow);
            }

            try
            {
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateException saveEx)
            {
                // Tracker-based save failed (concurrency / FK / etc.). The fixture and
                // unit-test rows are lost in this branch, but flipping the run's status
                // via a direct UPDATE is the most important thing — otherwise the UI
                // shows the run stuck in Running forever.
                logger.LogError(saveEx,
                    "Final SaveChanges failed for run {TestRunId}; falling back to direct UPDATE.", run.Id);
                await TryMarkTerminalAsync(run.Id, terminalStatus, terminalReason);
            }

            return Result.Success();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // The backstop wall clock fired. Reported as Failed, not Cancelled: nothing superseded this run
            // and it is not going to be retried, so "cancelled" would read as someone else's action.
            var reason = string.Format(
                CultureInfo.InvariantCulture, TimedOutReasonFormat, opts.RunTimeout.TotalMinutes);
            logger.LogWarning(
                "Test run {TestRunId} exceeded the {Timeout} budget and was abandoned.",
                request.TestRunId, opts.RunTimeout);
            await TryMarkTerminalAsync(request.TestRunId, TestRunStatus.Failed, reason);
            return Result.Success();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The webhook handler signals cancellation through the registry on a fresh
            // push. The DB write may already be done by the webhook handler; this is a
            // belt-and-braces UPDATE that is a no-op if the row is already terminal.
            await TryMarkTerminalAsync(request.TestRunId, TestRunStatus.Cancelled, SupersededReason);
            return Result.Success();
        }
        catch (Exception ex)
        {
            // Don't try the tracker-based save here — it's exactly the path that produced
            // the DbUpdateConcurrencyException we used to see. ExecuteUpdateAsync is a
            // single SQL UPDATE that doesn't care about tracker state.
            logger.LogError(ex, "Test run {TestRunId} failed", request.TestRunId);
            await TryMarkTerminalAsync(request.TestRunId, TestRunStatus.Failed, ex.Message);
            return Result.Failure(Error.Failure("TestRun.ExecutionFailed", ex.Message));
        }
        finally
        {
            registry.Unregister(request.TestRunId);
        }
    }

    /// <summary>
    /// Applies test events to <paramref name="run"/>. The file written by the judgement
    /// image is the canonical source — if it has any lines we use it. Otherwise we parse
    /// whatever showed up on stdout so legacy images keep working.
    /// </summary>
    private async Task ApplyEventLogAsync(
        TestRun run,
        string logFilePath,
        IReadOnlyList<string> stdoutFallback,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> lines;

        if (File.Exists(logFilePath))
        {
            try
            {
                // Bounded: the judge writes this file from inside the competitor's test process, so its size is
                // theirs to choose. See EventLogReader.
                var content = await EventLogReader.ReadAsync(logFilePath, cancellationToken);
                var fromFile = content.Lines;

                if (content.Truncated)
                {
                    logger.LogWarning(
                        "Event log {Path} for run {TestRunId} exceeded the ingest limit; the remainder was dropped.",
                        logFilePath, run.Id);
                    run.RecordDiagnostic(TruncatedEventLogDetail, DateTime.UtcNow);
                }

                // Existence alone is not enough. A judge that creates the file and then dies leaves an empty
                // one, and preferring it would report zero results *and* suppress this fallback - so the run
                // would look cleanly empty rather than broken.
                if (fromFile.Any(line => !string.IsNullOrWhiteSpace(line)))
                {
                    lines = fromFile;
                    logger.LogInformation(
                        "Read {LineCount} event lines from {Path} for run {TestRunId}.",
                        fromFile.Count, logFilePath, run.Id);
                }
                else
                {
                    logger.LogWarning(
                        "Event log {Path} exists but has no content for run {TestRunId}; falling back to {LineCount} captured stdout lines.",
                        logFilePath, run.Id, stdoutFallback.Count);
                    lines = stdoutFallback;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read event log {Path}; falling back to stdout.", logFilePath);
                lines = stdoutFallback;
            }
        }
        else
        {
            logger.LogInformation(
                "Event log {Path} not produced by the judge; falling back to {LineCount} captured stdout lines.",
                logFilePath, stdoutFallback.Count);
            lines = stdoutFallback;
        }

        foreach (var line in lines)
            TestLogParser.Apply(run, line);
    }

    /// <summary>
    /// Whether a rewritten repository URL is safe to pass to <c>git clone</c>.
    /// </summary>
    /// <remarks>
    /// Absolute-and-http(s) is the whole test, and it is the scheme check that matters: anything git would read
    /// as an option rather than a remote fails <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> or carries
    /// a different scheme.
    /// </remarks>
    private static bool IsCloneableUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Atomic, tracker-free transition to a terminal state. Used by every catch path and
    /// as the fallback when SaveChangesAsync fails — the row is guaranteed to leave the
    /// in-progress states (Pending / Cloning / Running) so the UI doesn't show the run
    /// stuck forever. The status filter makes the call idempotent: if another path
    /// already moved the row to a terminal state, no rows are affected.
    /// </summary>
    private async Task TryMarkTerminalAsync(Guid runId, TestRunStatus terminalStatus, string? reason)
    {
        try
        {
            var affected = await db.TestRuns
                .Where(r => r.Id == runId &&
                            (r.Status == TestRunStatus.Pending ||
                             r.Status == TestRunStatus.Cloning ||
                             r.Status == TestRunStatus.Running))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, terminalStatus)
                    .SetProperty(r => r.FailureReason, reason)
                    .SetProperty(r => r.FinishedAt, (DateTime?)DateTime.UtcNow));

            if (affected > 0)
                logger.LogInformation("Marked run {TestRunId} as {Status} via direct UPDATE.", runId, terminalStatus);
            else
                logger.LogDebug("Run {TestRunId} already terminal; UPDATE skipped.", runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to mark run {TestRunId} as {Status} via direct UPDATE.", runId, terminalStatus);
        }
    }

    private static bool IsTerminal(TestRunStatus status) =>
        status is TestRunStatus.Completed or TestRunStatus.Failed or TestRunStatus.Cancelled;

    private async Task<BasicCredential?> ResolveCredentialAsync(Guid? credentialId, CancellationToken cancellationToken) =>
        credentialId is null ? null : await db.FindByIdAsync(vault, credentialId.Value, cancellationToken);

    private async Task<RegistryAuth?> ResolveRegistryAuthAsync(
        Guid? credentialId, string image, CancellationToken cancellationToken)
    {
        var basic = await ResolveCredentialAsync(credentialId, cancellationToken);
        return basic is null ? null : RegistryAuthFactory.For(basic, image);
    }
}
