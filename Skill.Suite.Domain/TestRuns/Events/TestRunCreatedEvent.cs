using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.TestRuns.Events;

public sealed record TestRunCreatedEvent(Guid TestRunId) : IDomainEvent;
