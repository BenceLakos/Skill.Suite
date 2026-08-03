using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.TestRuns.ExecuteTestRun;
using Microsoft.EntityFrameworkCore;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Infra.BackgroundTasks;

/// <summary>
/// Drains the judgement queue with a configurable number of concurrent consumers.
/// </summary>
/// <remarks>
/// <para>
/// Concurrency matters more than it looks: with a single consumer, one slow or hung judgement run stalls every
/// other competitor's submission until it finishes. That is a competition-wide outage caused by one person's
/// code.
/// </para>
/// <para>
/// No per-competitor fairness logic is needed on top of this. A new push supersedes that competitor's older
/// in-flight runs, so at most one run per competitor is ever active — the consumers therefore spread across
/// different competitors on their own.
/// </para>
/// </remarks>
internal sealed class TestRunWorker(
    IBackgroundTaskQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<WebhookOptions> options,
    ILogger<TestRunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = Math.Max(1, options.Value.MaxConcurrentRuns);

        logger.LogInformation("TestRunWorker started with {Concurrency} concurrent slot(s)", concurrency);

        await RequeueAbandonedRunsAsync(stoppingToken);

        var consumers = Enumerable
            .Range(0, concurrency)
            .Select(slot => ConsumeAsync(slot, stoppingToken))
            .ToArray();

        await Task.WhenAll(consumers);

        logger.LogInformation("TestRunWorker stopped");
    }

    private async Task ConsumeAsync(int slot, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TestRunWorkItem item;
            try
            {
                item = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                // A scope per work item, not per consumer: the handler resolves scoped services — the
                // DbContext above all — and two consumers sharing one would corrupt each other's change
                // tracker.
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new ExecuteTestRunCommand(item.TestRunId), stoppingToken);
            }
            catch (Exception ex)
            {
                // Swallowed deliberately: a consumer must survive one bad run, or a single unhandled exception
                // would permanently shrink the pool. The handler has already moved the run to a terminal state.
                logger.LogError(
                    ex, "Unhandled error processing test run {TestRunId} on slot {Slot}", item.TestRunId, slot);
            }
        }
    }

    /// <summary>
    /// Re-enqueues runs that were accepted but never finished, on startup.
    /// </summary>
    /// <remarks>
    /// The queue is in-process, so a redeploy, an OOM kill or a crash discards everything in it. Without this the
    /// rows those items pointed at sat on Pending or Running forever: the submission was accepted, the competitor
    /// was told it was accepted, and nothing would ever judge it — with no operator action available to fix it.
    /// <para>
    /// Running rows are adopted too, not just Pending. A run marked Running has no container behind it once the
    /// process that started it is gone, so leaving it alone would strand it exactly as before; re-judging is
    /// idempotent because the handler rebuilds fixtures from scratch for the run.
    /// </para>
    /// <para>
    /// This is a recovery pass, not a scheduler. It runs once per start and deliberately does not poll: a
    /// periodic sweep would race with the runs the process is legitimately executing.
    /// </para>
    /// </remarks>
    private async Task RequeueAbandonedRunsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var abandoned = await db.TestRuns
                .Where(r => r.Status == TestRunStatus.Pending
                            || r.Status == TestRunStatus.Cloning
                            || r.Status == TestRunStatus.Running)
                .OrderBy(r => r.CreatedAt)
                .Select(r => r.Id)
                .ToListAsync(stoppingToken);

            if (abandoned.Count == 0)
                return;

            logger.LogWarning(
                "Found {Count} run(s) left unfinished by a previous process; re-enqueueing them.",
                abandoned.Count);

            foreach (var id in abandoned)
                await queue.EnqueueAsync(new TestRunWorkItem(id), stoppingToken);
        }
        catch (Exception ex)
        {
            // A failure here must not stop the worker from starting: the recovery pass is a bonus, and refusing
            // to start would turn a recoverable situation into a total outage.
            logger.LogError(ex, "Could not re-enqueue unfinished runs; the worker is starting anyway.");
        }
    }
}
