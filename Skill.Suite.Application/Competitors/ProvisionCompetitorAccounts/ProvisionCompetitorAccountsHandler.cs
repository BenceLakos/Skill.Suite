namespace Skill.Suite.Application.Competitors.ProvisionCompetitorAccounts;

using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Application.Competitors.Accounts;
using Skill.Suite.Application.Credentials;
using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.Competitors;
using Skill.Suite.Domain.Credentials;

/// <summary>
/// Creates the competitor's accounts on both external systems, reporting each independently.
/// </summary>
/// <remarks>
/// The two sides never abort one another. An admin provisioning twenty competitors while the SQL Server is
/// restarting should still end up with twenty git-host users, and a per-row error they can retry — not a
/// half-provisioned competition with no record of which half.
/// <para>
/// Nothing is written to this application's database, so there is no transaction and nothing to roll back. The
/// remote systems hold the state, and re-running the command is the repair.
/// </para>
/// </remarks>
public sealed class ProvisionCompetitorAccountsHandler(
    IAppDbContext db,
    IPasswordVault vault,
    IGitHostClient gitHost,
    IMsSqlAdminClient msSql,
    IOptions<CompetitorAccountsOptions> accountOptions,
    ILogger<ProvisionCompetitorAccountsHandler> logger)
    : IRequestHandler<ProvisionCompetitorAccountsCommand, Result<ProvisionCompetitorAccountsResult>>
{
    public async ValueTask<Result<ProvisionCompetitorAccountsResult>> Handle(
        ProvisionCompetitorAccountsCommand request, CancellationToken cancellationToken)
    {
        var competitor = await db.Competitors
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CompetitorId, cancellationToken);

        if (competitor is null)
            return CompetitorErrors.NotFound(request.CompetitorId);

        var giteaCredential = await db.FindByKindAsync(vault, CredentialKind.Gitea, cancellationToken);
        var msSqlCredential = await db.FindByKindAsync(vault, CredentialKind.MsSql, cancellationToken);

        // Neither configured is a configuration error, not a per-system skip: there is nothing this command
        // could do, and saying so once is clearer than saying "skipped" twice.
        if (giteaCredential is null && msSqlCredential is null)
            return CompetitorAccountErrors.NoCredentialsConfigured;

        // Held in plaintext only for the duration of this call, and never logged: the external systems need
        // the literal password so the competitor can sign in with the one they were handed.
        var password = vault.Unprotect(competitor.EncryptedPassword);

        var gitea = await ProvisionGiteaAsync(
            competitor.Username, password, giteaCredential, cancellationToken);

        var sql = await ProvisionMsSqlAsync(
            competitor.Username, password, msSqlCredential, cancellationToken);

        return new ProvisionCompetitorAccountsResult(competitor.Username, gitea, sql);
    }

    private async Task<AccountActionResult> ProvisionGiteaAsync(
        string username,
        string password,
        BasicCredential? credential,
        CancellationToken cancellationToken)
    {
        if (credential is null)
            return AccountActionResult.Skipped(CompetitorAccountErrors.MissingGiteaCredential.Message);

        try
        {
            var email = CompetitorEmail.For(username, accountOptions.Value.GiteaEmailDomain);

            var outcome = await gitHost.EnsureUserAsync(
                new EnsureGitHostUserRequest(username, email, password, credential), cancellationToken);

            return outcome == AccountProvisioning.Created
                ? AccountActionResult.Created()
                : AccountActionResult.AlreadyExists();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not provision the git host account for {Username}", username);
            return AccountActionResult.Failed(ExternalMessage.Trim(ex.Message));
        }
    }

    private async Task<AccountActionResult> ProvisionMsSqlAsync(
        string username,
        string password,
        BasicCredential? credential,
        CancellationToken cancellationToken)
    {
        if (credential is null)
            return AccountActionResult.Skipped(CompetitorAccountErrors.MissingMsSqlCredential.Message);

        try
        {
            var outcome = await msSql.EnsureLoginAndDatabaseAsync(
                new MsSqlAccountRequest(username, password, credential), cancellationToken);

            return outcome == AccountProvisioning.Created
                ? AccountActionResult.Created()
                : AccountActionResult.AlreadyExists();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not provision the SQL Server account for {Username}", username);
            return AccountActionResult.Failed(ExternalMessage.Trim(ex.Message));
        }
    }
}
