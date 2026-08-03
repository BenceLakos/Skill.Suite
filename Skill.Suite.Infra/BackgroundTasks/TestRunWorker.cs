using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.TestRuns.ExecuteTestRun;
using Skill.Suite.Application.Webhooks;

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
}
