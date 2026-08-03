using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Sessions.Events;

public sealed record SessionUpdatedEvent(Guid SessionId) : IDomainEvent;
