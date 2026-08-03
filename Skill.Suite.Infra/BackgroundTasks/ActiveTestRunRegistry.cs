using Skill.Suite.Application.Abstractions;

namespace Skill.Suite.Infra.BackgroundTasks;

internal sealed class ActiveTestRunRegistry : IActiveTestRunRegistry
{
    private readonly Dictionary<Guid, Entry> _byCompetitor = new();
    private readonly Dictionary<Guid, Guid> _competitorByRun = new();
    private readonly Lock _gate = new();

    public bool Register(Guid testRunId, Guid competitorId, CancellationTokenSource cts)
    {
        Entry? previous;
        lock (_gate)
        {
            _byCompetitor.TryGetValue(competitorId, out previous);
            _byCompetitor[competitorId] = new Entry(testRunId, cts);
            _competitorByRun[testRunId] = competitorId;
        }

        if (previous is null)
            return false;

        SignalCancel(previous.Cts);
        return true;
    }

    public bool CancelForCompetitor(Guid competitorId)
    {
        Entry? target;
        lock (_gate)
        {
            _byCompetitor.TryGetValue(competitorId, out target);
        }

        if (target is null)
            return false;

        SignalCancel(target.Cts);
        return true;
    }

    public void Unregister(Guid testRunId)
    {
        lock (_gate)
        {
            if (!_competitorByRun.Remove(testRunId, out var competitorId))
                return;

            if (_byCompetitor.TryGetValue(competitorId, out var entry) && entry.TestRunId == testRunId)
                _byCompetitor.Remove(competitorId);
        }
    }

    /// <summary>
    /// Signals cancellation without waiting for the callbacks to run.
    /// </summary>
    /// <remarks>
    /// <see cref="CancellationTokenSource.Cancel()"/> runs registered callbacks <b>inline on the calling
    /// thread</b>, and the executing run's callback stops a container — up to a 10-second SIGTERM grace. That
    /// put the whole of `docker stop` on the webhook request thread, so a competitor's second push could
    /// outlive Gitea's delivery timeout and the new run would never be written. <c>CancelAsync</c> hands the
    /// callbacks to the thread pool instead.
    /// <para>
    /// Deliberately not awaited: the caller only needs the older run to be told to stop, not to have finished
    /// stopping. Faults are observed so an <see cref="ObjectDisposedException"/> racing with
    /// <see cref="Unregister"/> cannot surface as an unobserved task exception.
    /// </para>
    /// </remarks>
    private static void SignalCancel(CancellationTokenSource cts)
    {
        try
        {
            _ = cts.CancelAsync().ContinueWith(
                static t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch (ObjectDisposedException) { /* race with Unregister; nothing to cancel */ }
    }

    private sealed record Entry(Guid TestRunId, CancellationTokenSource Cts);
}
