using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Experts.Events;

public sealed record ExpertCreatedEvent(Guid ExpertId) : IDomainEvent;
