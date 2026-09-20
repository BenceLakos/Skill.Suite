namespace Skill.Suite.Application.Competitors.RemoveCompetitorAccounts;

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
/// Removes the competitor's accounts from both external systems, refusing on either side that is still in use.
/// </summary>
/// <remarks>
/// The in-use checks are the point of the command rather than a nicety. Deleting a git-host user takes their
/// repositories with them, and dropping the login somebody is connected with cuts them off mid-statement. So
/// each side is skipped, with a reason, rather than forced; the admin can clear the blocker and press the
/// button again.
/// <para>
/// Only the login goes. The session databases the competitor worked in are left untouched, because they hold
/// the evidence a marking dispute is settled from and their lifetime belongs to the session.
/// </para>
/// </remarks>
public sealed class RemoveCompetitorAccountsHandler(
    IAppDbContext db,
    IPasswordVault vault,
    IGitHostClient gitHost,
    IMsSqlAdminClient msSql,
    ILogger<RemoveCompetitorAccountsHandler> logger)
    : IRequestHandler<RemoveCompetitorAccountsCommand, Result<RemoveCompetitorAccountsResult>>
{
    /// <summary>Why the git-host user was left alone.</summary>
    private const string StillOwnsRepositories = "The Gitea user still owns repositories.";

    /// <summary>Why the SQL Server login was left alone. Formatted with the connection count.</summary>
    private const string StillConnectedFormat = "{0} open connection(s) for the login.";

    public async ValueTask<Result<RemoveCompetitorAccountsResult>> Handle(
        RemoveCompetitorAccountsCommand request, CancellationToken cancellationToken)
    {
        var competitor = await db.Competitors
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CompetitorId, cancellationToken);

        if (competitor is null)
            return CompetitorErrors.NotFound(request.CompetitorId);

        var giteaCredential = await db.FindByKindAsync(vault, CredentialKind.Gitea, cancellationToken);
        var msSqlCredential = await db.FindByKindAsync(vault, CredentialKind.MsSql, cancellationToken);

        if (giteaCredential is null && msSqlCredential is null)
            return CompetitorAccountErrors.NoCredentialsConfigured;

        var gitea = await RemoveGiteaAsync(competitor.Username, giteaCredential, cancellationToken);
        var sql = await RemoveMsSqlAsync(competitor.Username, msSqlCredential, cancellationToken);

        return new RemoveCompetitorAccountsResult(competitor.Username, gitea, sql);
    }

    private async Task<AccountActionResult> RemoveGiteaAsync(
        string username, BasicCredential? credential, CancellationToken cancellationToken)
    {
        if (credential is null)
            return AccountActionResult.Skipped(CompetitorAccountErrors.MissingGiteaCredential.Message);

        try
        {
            if (await gitHost.HasRepositoriesAsync(username, credential, cancellationToken))
                return AccountActionResult.Skipped(StillOwnsRepositories);

            var outcome = await gitHost.DeleteUserAsync(username, credential, cancellationToken);

            return outcome == AccountRemoval.Removed
                ? AccountActionResult.Removed()
                : AccountActionResult.AlreadyMissing();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not remove the git host account for {Username}", username);
            return AccountActionResult.Failed(ExternalMessage.Trim(ex.Message));
        }
    }

    private async Task<AccountActionResult> RemoveMsSqlAsync(
        string username, BasicCredential? credential, CancellationToken cancellationToken)
    {
        if (credential is null)
            return AccountActionResult.Skipped(CompetitorAccountErrors.MissingMsSqlCredential.Message);

        try
        {
            var connections = await msSql.CountActiveConnectionsAsync(username, credential, cancellationToken);
            if (connections > 0)
            {
                return AccountActionResult.Skipped(
                    string.Format(StillConnectedFormat, connections));
            }

            var outcome = await msSql.DropLoginAsync(username, credential, cancellationToken);

            return outcome == AccountRemoval.Removed
                ? AccountActionResult.Removed()
                : AccountActionResult.AlreadyMissing();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not remove the SQL Server account for {Username}", username);
            return AccountActionResult.Failed(ExternalMessage.Trim(ex.Message));
        }
    }
}
