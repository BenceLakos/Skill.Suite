using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Webhooks;
using Skill.Suite.Infra.BackgroundTasks;
using Xunit;

namespace Skill.Suite.Application.Tests.BackgroundTasks;

/// <summary>
/// Covers how many judgement runs execute at once.
/// </summary>
/// <remarks>
/// This is worth asserting because the failure is invisible in normal use: a serial worker looks perfectly
/// healthy right up to the moment one competitor's hung submission stalls everybody else's.
/// </remarks>
public sealed class TestRunWorkerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task TwoSlots_ProcessTwoRunsAtOnce()
    {
        using var harness = new WorkerHarness(maxConcurrentRuns: 2, queued: 4);

        Assert.True(
            await harness.Sender.WaitForStartsAsync(2, Timeout),
            "expected two runs to start concurrently");

        // Settle, then assert the pool did not exceed its budget - concurrency has to be bounded as well as
        // greater than one, or a burst of pushes would start a container per submission.
        await Task.Delay(250);
        Assert.Equal(2, harness.Sender.MaxConcurrent);

        await harness.StopAsync();
    }

    [Fact]
    public async Task OneSlot_IsStrictlySerial()
    {
        using var harness = new WorkerHarness(maxConcurrentRuns: 1, queued: 4);

        Assert.True(await harness.Sender.WaitForStartsAsync(1, Timeout), "expected one run to start");

        await Task.Delay(250);
        Assert.Equal(1, harness.Sender.MaxConcurrent);

        await harness.StopAsync();
    }

    [Fact]
    public async Task AllQueuedRunsAreEventuallyProcessed()
    {
        using var harness = new WorkerHarness(maxConcurrentRuns: 2, queued: 6);

        Assert.True(await harness.Sender.WaitForStartsAsync(2, Timeout));

        // Releasing lets the held runs finish and the consumers pick up the rest; every item must be taken up
        // exactly once, which is what multi-reader correctness on the channel means in practice.
        harness.Sender.ReleaseAll();

        Assert.True(
            await harness.Sender.WaitForStartsAsync(4, Timeout),
            "expected the remaining queued runs to be picked up");

        await harness.StopAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task NonPositiveConcurrency_FallsBackToOneSlot(int configured)
    {
        // A misconfigured 0 must not silently stop judging altogether.
        using var harness = new WorkerHarness(maxConcurrentRuns: configured, queued: 2);

        Assert.True(await harness.Sender.WaitForStartsAsync(1, Timeout), "expected the worker to still run");
        Assert.Equal(1, harness.Sender.MaxConcurrent);

        await harness.StopAsync();
    }

    [Fact]
    public void DefaultConcurrency_IsGreaterThanOne()
    {
        // The whole point of the change: out of the box, one slow run must not block every other competitor.
        Assert.True(new WebhookOptions().MaxConcurrentRuns > 1);
    }

    private sealed class WorkerHarness : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly TestRunWorker _worker;

        internal BlockingSender Sender { get; } = new();

        internal WorkerHarness(int maxConcurrentRuns, int queued)
        {
            var sender = Sender;
            var services = new ServiceCollection();
            services.AddScoped<ISender>(_ => sender);
            _provider = services.BuildServiceProvider();

            var queue = new ChannelBackgroundTaskQueue();
            for (var i = 0; i < queued; i++)
                queue.EnqueueAsync(new TestRunWorkItem(Guid.NewGuid())).AsTask().GetAwaiter().GetResult();

            _worker = new TestRunWorker(
                queue,
                _provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new WebhookOptions { MaxConcurrentRuns = maxConcurrentRuns }),
                NullLogger<TestRunWorker>.Instance);

            _worker.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        internal async Task StopAsync()
        {
            Sender.ReleaseAll();
            using var cts = new CancellationTokenSource(Timeout);
            await _worker.StopAsync(cts.Token);
        }

        public void Dispose()
        {
            Sender.ReleaseAll();
            _worker.Dispose();
            _provider.Dispose();
        }
    }
}
