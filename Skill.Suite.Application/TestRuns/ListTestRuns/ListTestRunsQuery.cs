using Mediator;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns;

namespace Skill.Suite.Application.TestRuns.ListTestRuns;

public sealed record ListTestRunsQuery(
    Guid? SessionId,
    Guid? CompetitorId,
    TestRunStatus? Status) : IRequest<Result<List<TestRunSummaryDto>>>;
