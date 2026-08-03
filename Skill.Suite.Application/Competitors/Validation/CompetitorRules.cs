using System.Net;
using System.Text.RegularExpressions;

namespace Skill.Suite.Application.Competitors.Validation;

internal static class CompetitorRules
{
    public static readonly Regex UsernamePattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);
    public static readonly Regex CountryCodePattern = new("^[A-Za-z]{3}$", RegexOptions.Compiled);

    public static bool IsValidIpAddress(string value) => IPAddress.TryParse(value, out _);
}
