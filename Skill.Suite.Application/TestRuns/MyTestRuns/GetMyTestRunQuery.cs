using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.MyTestRuns;

public sealed record GetMyTestRunQuery(Guid Id) : IRequest<Result<MyTestRunDto>>;
