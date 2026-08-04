using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.TestRuns.Events;

public sealed record TestRunCompletedEvent(Guid TestRunId) : IDomainEvent;
