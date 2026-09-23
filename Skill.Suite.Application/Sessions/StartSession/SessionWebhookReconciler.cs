namespace Skill.Suite.Application.Sessions.StartSession;

using Microsoft.Extensions.Logging;
using Skill.Suite.Application.Abstractions;
using Skill.Suite.Domain.Sessions;

/// <summary>
/// Brings the organisation's push webhook into line with whether the session is judged at all.
/// </summary>
/// <remarks>
/// Both directions are performed on every Start rather than only on a change, because nothing records what
/// the git host currently has: a hook can be installed, removed or edited on the host itself, and Start is
/// the one action allowed to talk to it.
/// <para>
/// Its own type rather than a step inside the start handler, for the reason <c>RegistryAuthFactory</c> is:
/// the rule is one decision with two irreversible-looking outcomes on somebody else's server, and reaching
/// it through the handler means standing up a git host, a SQL Server and a docker daemon first.
/// </para>
/// </remarks>
internal static class SessionWebhookReconciler
{
    public static async Task ReconcileAsync(
        IGitHostClient gitHost,
        ILogger logger,
        Session session,
        string targetUrl,
        string branchFilter,
        string secret,
        BasicCredential admin,
        CancellationToken cancellationToken)
    {
        var organization = session.GitOrganization;

        if (session.RequiresJudgement)
        {
            await gitHost.EnsureOrganizationWebhookAsync(
                new EnsureWebhookRequest(organization, targetUrl, secret, branchFilter, admin),
                cancellationToken);

            return;
        }

        // A session with no judgement image is provisioned exactly like any other - organisation,
        // repositories, databases, services - but nothing can mark a push, so a hook would deliver every one
        // of them to an endpoint whose only possible answer is a refusal, painting the administrator's hook
        // page red for a session that is working correctly.
        //
        // REMOVED rather than merely not installed: the image can be cleared on a session that was once
        // judged, and the hook installed by that earlier Start would otherwise keep firing forever. Removal
        // is the delete the install already performs before reinstalling, so it costs one request and is a
        // no-op on an organisation that has no matching hook.
        await gitHost.RemoveOrganizationWebhookAsync(
            new RemoveWebhookRequest(organization, targetUrl, admin), cancellationToken);

        logger.LogInformation(
            "Session {SessionId} declares no judgement image, so {Org} was left without a push webhook and " +
            "no submission will be marked automatically",
            session.Id, organization);
    }
}
