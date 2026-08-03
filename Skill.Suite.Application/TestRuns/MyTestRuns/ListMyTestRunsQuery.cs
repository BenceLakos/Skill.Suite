using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.MyTestRuns;

public sealed record ListMyTestRunsQuery() : IRequest<Result<List<MyTestRunSummaryDto>>>;
