using Mediator;
using Skill.Suite.Application.Common;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Infra.Persistence;

internal sealed class DomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var @event in events)
            await publisher.Publish(new DomainEventNotification(@event), cancellationToken);
    }
}
