using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.ExecuteTestRun;

public sealed record ExecuteTestRunCommand(Guid TestRunId) : IRequest<Result>;
