using System.Text.RegularExpressions;

namespace Skill.Suite.Application.StarterPackages;

/// <summary>
/// The names a starter package directory may take. Deliberately the same pattern
/// <c>scripts/starter-packages.sh</c> enforces, so a package pushed from a shell and one uploaded from the
/// UI cannot end up with names only one of the two accepts.
/// </summary>
public static partial class StarterPackageName
{
    public const int MaxLength = 100;

    public static bool IsValid(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= MaxLength && Pattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex Pattern();
}
