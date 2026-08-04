using System.Threading.Channels;
using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.BackgroundTasks;

internal sealed class ChannelBackgroundTaskQueue : IBackgroundTaskQueue
{
    // SingleReader is false because TestRunWorker runs several consumer loops concurrently. Leaving it true
    // while adding readers would be a silent correctness bug: the channel is free to optimise on the promise
    // that only one reader exists.
    private readonly Channel<TestRunWorkItem> _channel =
        Channel.CreateUnbounded<TestRunWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(TestRunWorkItem item, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(item, cancellationToken);

    public ValueTask<TestRunWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
