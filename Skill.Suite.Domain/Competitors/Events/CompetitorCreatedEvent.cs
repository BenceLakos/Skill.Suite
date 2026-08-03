using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.Competitors.Events;

public sealed record CompetitorCreatedEvent(Guid CompetitorId) : IDomainEvent;
