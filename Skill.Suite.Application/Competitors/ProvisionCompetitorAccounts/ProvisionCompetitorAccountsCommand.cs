namespace Skill.Suite.Application.Competitors.ProvisionCompetitorAccounts;

using Mediator;
using Skill.Suite.Domain.Common;

/// <summary>
/// Creates the competitor's git-host user and SQL Server login, if they are not already there.
/// </summary>
/// <remarks>
/// Additive and idempotent, which is why the UI asks for no confirmation: the worst outcome of pressing it
/// twice is two "already existed" lines.
/// </remarks>
public sealed record ProvisionCompetitorAccountsCommand(Guid CompetitorId)
    : IRequest<Result<ProvisionCompetitorAccountsResult>>;
