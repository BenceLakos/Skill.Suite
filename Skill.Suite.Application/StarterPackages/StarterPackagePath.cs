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
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.Contains('\0'))
                return false;
        }

        var candidate = Path.GetFullPath(Path.Combine([resolvedRoot, .. segments]));
        var prefix = resolvedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? resolvedRoot
            : resolvedRoot + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        fullPath = candidate;
        return true;
    }
}
