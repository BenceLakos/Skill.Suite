namespace Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// The outcome of one system's half of a provision or remove request, plus the reason when there is one.
/// </summary>
/// <remarks>
/// Not a <c>Result</c>: neither half failing is a failure of the request. Gitea being down must not hide the
/// fact that the SQL Server login was created, so both halves are reported side by side and the command itself
/// succeeds.
/// </remarks>
public sealed record AccountActionResult(AccountActionOutcome Outcome, string? Detail)
{
    public static AccountActionResult Created() => new(AccountActionOutcome.Created, null);

    public static AccountActionResult AlreadyExists() => new(AccountActionOutcome.AlreadyExists, null);

    public static AccountActionResult Removed() => new(AccountActionOutcome.Removed, null);

    public static AccountActionResult AlreadyMissing() => new(AccountActionOutcome.AlreadyMissing, null);

    public static AccountActionResult Skipped(string reason) => new(AccountActionOutcome.Skipped, reason);

    public static AccountActionResult Failed(string message) => new(AccountActionOutcome.Failed, message);
}
