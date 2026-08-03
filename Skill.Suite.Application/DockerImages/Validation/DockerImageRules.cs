using System.Text.RegularExpressions;

namespace Skill.Suite.Application.DockerImages.Validation;

internal static class DockerImageRules
{
    public static readonly Regex NamePattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    // Loose docker image reference (without tag). Allows alphanumerics, slashes,
    // dots, dashes and underscores — covers things like nexus.example.com/team/app.
    public static readonly Regex ImageNamePattern = new("^[a-z0-9]+([._-][a-z0-9]+)*(\\/[a-z0-9]+([._-][a-z0-9]+)*)*(:[a-z0-9][a-z0-9._-]*)?$|^[A-Za-z0-9._/-]+$", RegexOptions.Compiled);

    // POSIX-ish env name for build args (--build-arg KEY=value).
    public static readonly Regex BuildArgKeyPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    // Docker tag: lowercase-friendly, alnum + dot/underscore/dash, up to 128 chars,
    // can't start with dot/dash. We accept uppercase too because semantic-version
    // pre-release strings often include them.
    public static readonly Regex TagPattern = new("^[A-Za-z0-9_][A-Za-z0-9._-]{0,127}$", RegexOptions.Compiled);
}
