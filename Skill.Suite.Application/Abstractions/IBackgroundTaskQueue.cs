using Skill.Suite.Application.TestRuns;

namespace Skill.Suite.Application.Abstractions;

/// <summary>
/// In-process work queue for test-run executions. The webhook endpoint enqueues an item
/// and returns immediately; a hosted worker dequeues and runs it under its own DI scope
/// so long test executions do not block HTTP responses.
/// </summary>
public interface IBackgroundTaskQueue
{
    ValueTask EnqueueAsync(TestRunWorkItem item, CancellationToken cancellationToken = default);
    ValueTask<TestRunWorkItem> DequeueAsync(CancellationToken cancellationToken);
}

public sealed record TestRunWorkItem(Guid TestRunId);
