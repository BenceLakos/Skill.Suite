namespace Skill.Suite.Application.Competitors.GetCompetitorAccountStatuses;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Live account status for every competitor, read from the external systems rather than from a stored flag.
/// </summary>
/// <remarks>
/// Nothing is persisted about these accounts, so the remote systems are the only record. That is deliberate: a
/// login dropped from a SQL client, or a user deleted in the Gitea UI, shows up here on the next refresh
/// instead of leaving the platform confidently wrong.
/// </remarks>
public sealed record GetCompetitorAccountStatusesQuery
    : IRequest<Result<List<CompetitorAccountStatusDto>>>;
