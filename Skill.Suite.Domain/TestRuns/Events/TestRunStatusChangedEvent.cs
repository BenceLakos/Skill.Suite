using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.TestRuns.Events;

public sealed record TestRunStatusChangedEvent(Guid TestRunId, TestRunStatus Status) : IDomainEvent;
