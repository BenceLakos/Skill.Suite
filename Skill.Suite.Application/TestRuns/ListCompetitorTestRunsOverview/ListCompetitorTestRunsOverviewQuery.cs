using Mediator;
using Skill.Suite.Domain.Common;

namespace Skill.Suite.Application.TestRuns.ListCompetitorTestRunsOverview;

/// <summary>
/// Admin overview of competitor test runs, scoped to a single session. When SessionId
/// is null the handler defaults to the active session — matching what competitors see
/// — so the admin's "Test runs" page lands on the live event by default.
/// </summary>
public sealed record ListCompetitorTestRunsOverviewQuery(Guid? SessionId = null)
    : IRequest<Result<List<CompetitorTestRunsOverviewDto>>>;
