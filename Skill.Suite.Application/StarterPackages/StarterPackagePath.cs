namespace Skill.Suite.Application.StarterPackages;

/// <summary>
/// Resolves a browser-supplied relative path against the starter packages root, refusing anything that
/// would leave it.
/// </summary>
/// <remarks>
/// Pure and standalone so the rule can be tested directly: every path this feature touches — a browse
/// request, a download, each entry of an uploaded archive — goes through here. The segment rules do most of
/// the work, and the containment check on the fully resolved path is what catches whatever they do not, on
/// whichever host the application happens to run.
/// </remarks>
public static class StarterPackagePath
{
    public static bool TryResolve(string root, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(root))
            return false;

        var resolvedRoot = Path.GetFullPath(root);
        var normalized = (relativePath ?? string.Empty).Replace('\\', '/');

        // The root itself is a legitimate target: it is what the packages list and the "download everything"
        // archive address.
        if (normalized.Length == 0)
        {
            fullPath = resolvedRoot;
            return true;
        }

        if (Path.IsPathRooted(normalized) || normalized.StartsWith('/'))
            return false;

        var segments = normalized.Split('/');
        if (!segments.All(IsSafeSegment))
            return false;

        var candidate = Path.GetFullPath(Path.Combine([resolvedRoot, .. segments]));
        var prefix = resolvedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? resolvedRoot
            : resolvedRoot + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// Whether a path could name something inside the volume at all, without resolving it.
    /// </summary>
    /// <remarks>
    /// The segment half of <see cref="TryResolve"/>, exposed so a validator can refuse a traversal while the
    /// administrator is still on the form: nothing there knows where the volume is mounted, and a rule that
    /// only ran when the session was started would report the problem a competition too late. Resolution
    /// still has the final say — this cannot see what the path actually lands on.
    /// <para>
    /// The root itself is not one: unlike <see cref="TryResolve"/>, which the packages list addresses with an
    /// empty path, every caller of this means one named file or folder.
    /// </para>
    /// </remarks>
    public static bool IsSafeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        var normalized = relativePath.Replace('\\', '/');

        if (Path.IsPathRooted(normalized) || normalized.StartsWith('/'))
            return false;

        return normalized.Split('/').All(IsSafeSegment);
    }

    private static bool IsSafeSegment(string segment) =>
        segment.Length > 0 && segment is not ("." or "..") && !segment.Contains('\0');
}
