using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Tests.BackgroundTasks;

/// <summary>
/// An <see cref="ISender"/> that holds every request open until released, and records how many were in flight
/// at once.
/// </summary>
/// <remarks>
/// Holding requests open is the only way to observe concurrency: if the fake returned immediately, the worker
/// would process items so fast that a single consumer and four consumers would be indistinguishable.
/// </remarks>
internal sealed class BlockingSender : ISender
{
    private readonly SemaphoreSlim _started = new(0);
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _current;
    private int _max;

    /// <summary>The greatest number of requests observed in flight simultaneously.</summary>
    internal int MaxConcurrent => Volatile.Read(ref _max);

    /// <summary>Waits until <paramref name="count"/> requests have started.</summary>
    internal async Task<bool> WaitForStartsAsync(int count, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            for (var i = 0; i < count; i++)
                await _started.WaitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Lets every held request complete.</summary>
    internal void ReleaseAll() => _release.TrySetResult();

    public async ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var running = Interlocked.Increment(ref _current);

        // Interlocked.CompareExchange loop rather than a plain compare-and-set: two consumers can reach this
        // at the same moment, which is precisely the situation under test.
        var observed = Volatile.Read(ref _max);
        while (running > observed)
        {
            var previous = Interlocked.CompareExchange(ref _max, running, observed);
            if (previous == observed) break;
            observed = previous;
        }

        _started.Release();

        try
        {
            await _release.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _current);
        }

        return (TResponse)(object)Result.Success();
    }

    // The rest of the interface is not exercised by the worker.
    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
