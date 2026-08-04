using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.Common;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken = default);
}
