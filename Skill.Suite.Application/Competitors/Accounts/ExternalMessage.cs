namespace Skill.Suite.Application.Competitors.Accounts;

/// <summary>
/// Cuts a message from a remote system down to something a tooltip or a snackbar can hold.
/// </summary>
/// <remarks>
/// A SQL Server error or a git-host response body can run to hundreds of characters of stack trace and HTML.
/// Untrimmed, one of them fills the screen and hides the twenty rows the admin was looking at.
/// </remarks>
internal static class ExternalMessage
{
    /// <summary>Roughly two lines in a snackbar.</summary>
    private const int DefaultMaxLength = 200;

    private const string Ellipsis = "…";

    public static string Trim(string message, int max = DefaultMaxLength)
    {
        var collapsed = message.Trim();
        return collapsed.Length <= max ? collapsed : collapsed[..max] + Ellipsis;
    }
}
