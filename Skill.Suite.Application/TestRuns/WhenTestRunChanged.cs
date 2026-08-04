using Mediator;
using Skill.Suite.Application.Common;
using Skill.Suite.Domain.TestRuns.Events;

namespace Skill.Suite.Application.TestRuns;

/// <summary>
/// Bridges TestRun domain events to <see cref="ITestRunChangeNotifier"/> so live UIs
/// can refresh without polling.
/// </summary>
internal sealed class WhenTestRunChanged(ITestRunChangeNotifier notifier)
    : INotificationHandler<DomainEventNotification>
{
    public ValueTask Handle(DomainEventNotification notification, CancellationToken cancellationToken)
    {
        var testRunId = notification.Event switch
        {
            TestRunCreatedEvent e => e.TestRunId,
            TestRunStatusChangedEvent e => e.TestRunId,
            TestRunCompletedEvent e => e.TestRunId,
            TestRunFailedEvent e => e.TestRunId,
            _ => (Guid?)null,
        };

        if (testRunId.HasValue)
            notifier.Notify(testRunId.Value);

        return ValueTask.CompletedTask;
    }
}
