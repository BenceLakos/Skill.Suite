namespace Skill.Suite.Application.Sessions.Services;

using System.Text.RegularExpressions;

/// <summary>
/// The hostname a routed session service answers on.
/// </summary>
/// <remarks>
/// A bare hostname, never a URL: no scheme, no port and no path. The proxy matches on the <c>Host</c> header
/// alone, so anything else in the field is not a stricter rule — it is a rule that matches nothing, and the
/// service is simply unreachable for the whole competition.
/// <para>
/// One name serves every competitor. What sends a request to the right container is the source address of
/// the machine it came from, not the name, which is why the same domain is used again unchanged while
/// marking — only the address it is paired with changes.
/// </para>
/// </remarks>
internal static partial class ServiceDomain
{
    /// <summary>The longest a DNS name may be, in total.</summary>
    internal const int MaxLength = 253;

    /// <summary>
    /// Lower case only, because the proxy rule is written with the value verbatim.
    /// </summary>
    /// <remarks>
    /// Host headers compare case-insensitively in principle, but the rule is a literal in a generated label
    /// and DNS names are conventionally lower case; accepting <c>Shop.Skills.Local</c> would leave the
    /// administrator comparing two spellings of the same name when a route does not match.
    /// </remarks>
    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)*$")]
    private static partial Regex HostnamePattern { get; }

    public static bool IsValid(string domain) =>
        domain.Length <= MaxLength && HostnamePattern.IsMatch(domain);
}
