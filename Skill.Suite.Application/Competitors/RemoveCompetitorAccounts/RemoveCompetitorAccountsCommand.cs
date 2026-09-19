namespace Skill.Suite.Application.Competitors.RemoveCompetitorAccounts;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Deletes the competitor's git-host user and SQL Server login, together with that login's database.
/// </summary>
/// <remarks>
/// Destructive and not recoverable, so the UI confirms first and both sides refuse while the account is still
/// in use — a user that still owns repositories, or a login something is connected to.
/// </remarks>
public sealed record RemoveCompetitorAccountsCommand(Guid CompetitorId)
    : IRequest<Result<RemoveCompetitorAccountsResult>>;
