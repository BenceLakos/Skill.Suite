using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.GetTestRun;

public sealed record GetTestRunQuery(Guid Id) : IRequest<Result<TestRunDto>>;
