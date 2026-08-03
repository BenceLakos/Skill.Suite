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

    private static void SignalCancel(CancellationTokenSource cts)
    {
        try { cts.Cancel(); }
        catch (ObjectDisposedException) { /* race with Unregister; nothing to cancel */ }
    }

    private sealed record Entry(Guid TestRunId, CancellationTokenSource Cts);
}
