using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Sessions.Events;

public sealed record SessionStartedEvent(Guid SessionId) : IDomainEvent;
