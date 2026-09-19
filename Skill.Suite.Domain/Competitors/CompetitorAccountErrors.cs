namespace Skill.Suite.Domain.Competitors;

using Skill.Suite.Domain.Common;

/// <summary>
/// Reasons the platform cannot act on a competitor's external accounts at all.
/// </summary>
/// <remarks>
/// A missing credential is not a failure of the remote system, it is a gap in this installation's
/// configuration — so the messages name the page that fixes it rather than describing a fault.
/// </remarks>
public static class CompetitorAccountErrors
{
    public static readonly Error MissingGiteaCredential = Error.NotFound(
        "CompetitorAccount.MissingGiteaCredential",
        "No Gitea credential is configured. Add one on the Credentials page.");

    public static readonly Error MissingMsSqlCredential = Error.NotFound(
        "CompetitorAccount.MissingMsSqlCredential",
        "No Microsoft SQL Server credential is configured. Add one on the Credentials page.");

    public static readonly Error NoCredentialsConfigured = Error.NotFound(
        "CompetitorAccount.NoCredentialsConfigured",
        "Neither a Gitea nor a Microsoft SQL Server credential is configured.");
}
