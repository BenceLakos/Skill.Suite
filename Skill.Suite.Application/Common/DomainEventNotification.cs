using Mediator;
using Microsoft.Extensions.Logging;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Common;

// Wrapper so Domain types stay free of any Mediator dependency.
// Handlers subscribe to INotificationHandler<DomainEventNotification> and pattern-match on Event.
public sealed record DomainEventNotification(IDomainEvent Event) : INotification;

// Stub handler so the source generator stops warning about a message without subscribers.
// Replace with feature-specific handlers (e.g. WhenSessionCreated) once they're needed.
internal sealed class DomainEventNotificationLogger(ILogger<DomainEventNotificationLogger> logger)
    : INotificationHandler<DomainEventNotification>
{
    public ValueTask Handle(DomainEventNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Domain event raised: {EventType}", notification.Event.GetType().Name);
        return ValueTask.CompletedTask;
    }
}
