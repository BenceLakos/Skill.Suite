namespace Skill.Suite.Application.StarterPackages;

using System.Text;

/// <summary>
/// The names an administrator may give a file or folder inside a starter package.
/// </summary>
/// <remarks>
/// Deliberately far looser than <see cref="StarterPackageName"/>: a package's name ends up in every session's
/// template folder and has to survive <c>scripts/starter-packages.sh</c>, while what is inside a package is
/// the author's own and may be called anything Linux accepts — spaces, accents and parentheses included.
/// What is refused is what cannot be a single name: a separator would make it a path, <c>.</c> and
/// <c>..</c> already mean something, and a control character is never what somebody meant to type.
/// <para>
/// The length is counted the way Linux counts it, in UTF-8 bytes — 255 plain letters, fewer with accents —
/// so a name this accepts is one the volume can actually hold.
/// </para>
/// </remarks>
public static class StarterPackageEntryName
{
    public const int MaxLength = 255;

    public static bool IsValid(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && Encoding.UTF8.GetByteCount(name) <= MaxLength
        && name is not ("." or "..")
        && !name.Any(character =>
            character is StarterPackagePath.Separator or StarterPackagePath.AlternativeSeparator
            || char.IsControl(character));
}
