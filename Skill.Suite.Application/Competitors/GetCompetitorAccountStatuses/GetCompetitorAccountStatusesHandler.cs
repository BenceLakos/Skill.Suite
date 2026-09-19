namespace Skill.Suite.Application.Competitors.GetCompetitorAccountStatuses;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Application.Credentials;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;
using Skill.Suite.Domain.Credentials;

/// <summary>
/// Asks each external system once what it contains, then answers for every competitor from that snapshot.
/// </summary>
/// <remarks>
/// Always succeeds. A system being unreachable is reported per row as
/// <see cref="ExternalAccountStatus.Unknown"/> with the reason attached, never as a failed query: the grid this
/// feeds also carries names, IP addresses and the edit actions, and none of that should disappear because a
/// container is down.
/// </remarks>
public sealed class GetCompetitorAccountStatusesHandler(
    IAppDbContext db,
    IPasswordVault vault,
    IGitHostClient gitHost,
    IMsSqlAdminClient msSql,
    ILogger<GetCompetitorAccountStatusesHandler> logger)
    : IRequestHandler<GetCompetitorAccountStatusesQuery, Result<List<CompetitorAccountStatusDto>>>
{
    public async ValueTask<Result<List<CompetitorAccountStatusDto>>> Handle(
        GetCompetitorAccountStatusesQuery request, CancellationToken cancellationToken)
    {
        var competitors = await db.Competitors
            .AsNoTracking()
            .OrderBy(c => c.Username)
            .Select(c => new { c.Id, c.Username })
            .ToListAsync(cancellationToken);

        if (competitors.Count == 0)
            return new List<CompetitorAccountStatusDto>();

        var (giteaNames, giteaReason) = await LoadGiteaUsernamesAsync(cancellationToken);
        var (logins, databases, msSqlReason) = await LoadMsSqlInventoryAsync(cancellationToken);

        return competitors
            .Select(c =>
            {
                var gitea = AccountStatusEvaluator.Evaluate(c.Username, giteaNames, giteaReason);
                var sql = AccountStatusEvaluator.EvaluateMsSql(c.Username, logins, databases, msSqlReason);

                return new CompetitorAccountStatusDto(
                    c.Id, c.Username, gitea.Status, gitea.Detail, sql.Status, sql.Detail);
            })
            .ToList();
    }

    /// <summary>
    /// The git host's user list, or null plus the reason it could not be read.
    /// </summary>
    private async Task<(IReadOnlySet<string>? Names, string? Reason)> LoadGiteaUsernamesAsync(
        CancellationToken cancellationToken)
    {
        var credential = await db.FindByKindAsync(vault, CredentialKind.Gitea, cancellationToken);
        if (credential is null)
            return (null, CompetitorAccountErrors.MissingGiteaCredential.Message);

        try
        {
            var names = await gitHost.ListUsernamesAsync(credential, cancellationToken);

            // Case-insensitive: Gitea lower-cases account names, so a competitor stored as "C01" would
            // otherwise never match the "c01" the host reports.
            return (names.ToHashSet(StringComparer.OrdinalIgnoreCase), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not list the git host users for the competitor account status");
            return (null, ExternalMessage.Trim(ex.Message));
        }
    }

    /// <summary>
    /// The SQL Server logins and databases, or nulls plus the reason they could not be read.
    /// </summary>
    private async Task<(IReadOnlySet<string>? Logins, IReadOnlySet<string>? Databases, string? Reason)>
        LoadMsSqlInventoryAsync(CancellationToken cancellationToken)
    {
        var credential = await db.FindByKindAsync(vault, CredentialKind.MsSql, cancellationToken);
        if (credential is null)
            return (null, null, CompetitorAccountErrors.MissingMsSqlCredential.Message);

        try
        {
            var inventory = await msSql.GetInventoryAsync(credential, cancellationToken);

            return (inventory.Logins.ToHashSet(StringComparer.OrdinalIgnoreCase),
                inventory.Databases.ToHashSet(StringComparer.OrdinalIgnoreCase),
                null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not read the SQL Server inventory for the competitor account status");
            return (null, null, ExternalMessage.Trim(ex.Message));
        }
    }
}
