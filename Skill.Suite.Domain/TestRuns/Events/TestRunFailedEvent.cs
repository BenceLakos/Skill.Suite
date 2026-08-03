using Skill.Suite.Domain.Common;

namespace Skill.Suite.Domain.TestRuns.Events;

public sealed record TestRunFailedEvent(Guid TestRunId, string Reason) : IDomainEvent;
