using System.Text.RegularExpressions;

namespace Skill.Suite.Application.Credentials.Validation;

internal static class CredentialRules
{
    public static readonly Regex NamePattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);
}
