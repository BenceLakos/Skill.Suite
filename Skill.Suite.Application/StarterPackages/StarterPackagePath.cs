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
    /// <summary>The separator of every path this feature hands out, whichever host it runs on.</summary>
    internal const char Separator = '/';

    /// <summary>Accepted as a separator on the way in: a path typed or pasted on Windows arrives with it.</summary>
    internal const char AlternativeSeparator = '\\';

    public static bool TryResolve(string root, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(root))
            return false;

        var resolvedRoot = Path.GetFullPath(root);
        var normalized = (relativePath ?? string.Empty).Replace(AlternativeSeparator, Separator);

        // The root itself is a legitimate target: it is what the packages list and the "download everything"
        // archive address.
        if (normalized.Length == 0)
        {
            fullPath = resolvedRoot;
            return true;
        }

        if (Path.IsPathRooted(normalized) || normalized.StartsWith(Separator))
            return false;

        var segments = normalized.Split(Separator);
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

        var normalized = relativePath.Replace(AlternativeSeparator, Separator);

        if (Path.IsPathRooted(normalized) || normalized.StartsWith(Separator))
            return false;

        return normalized.Split(Separator).All(IsSafeSegment);
    }

    /// <summary>
    /// Joins a folder and a path inside it into one path from the root, separated by <c>/</c>.
    /// </summary>
    /// <remarks>
    /// A join, not a check: a <c>..</c> passes straight through, so the result still has to go through
    /// <see cref="TryResolve"/> like any other path. Either separator is accepted and empty segments are
    /// dropped, so a folder reported with a trailing separator joins the same as one without, and the result
    /// never doubles a separator or ends in one.
    /// </remarks>
    public static string Combine(string folderPath, string relativePath) =>
        string.Join(Separator, Segments(folderPath).Concat(Segments(relativePath)));

    /// <summary>
    /// The package a path lies in — its first segment — or <see langword="null"/> when it is not in one.
    /// </summary>
    /// <remarks>
    /// Not in one covers everything <see cref="IsSafeRelativePath"/> refuses, the root included, and a first
    /// segment no package can be called: a dot-prefixed staging directory, say, which is an upload in
    /// progress rather than something an administrator changes. The package's name is the same rule
    /// <see cref="StarterPackageName"/> applies when one is created, so nothing can be written into a
    /// directory the packages list would not show as a package.
    /// </remarks>
    public static string? PackageOf(string? relativePath) =>
        IsSafeRelativePath(relativePath)
        && Segments(relativePath) is [var package, ..]
        && StarterPackageName.IsValid(package)
            ? package
            : null;

    /// <summary>Whether a path names a package, or something inside one.</summary>
    public static bool IsInPackage(string? relativePath) => PackageOf(relativePath) is not null;

    /// <summary>
    /// The segments of a path below its package: empty for the package itself, and for a path that is not in
    /// a package at all.
    /// </summary>
    public static string[] SegmentsBelowPackage(string? relativePath) =>
        IsInPackage(relativePath) && Segments(relativePath) is [_, .. var belowPackage] ? belowPackage : [];

    /// <summary>Whether a path names a file or folder inside a package, rather than a package itself.</summary>
    public static bool IsBelowPackage(string? relativePath) => SegmentsBelowPackage(relativePath) is not [];

    private static string[] Segments(string? path) =>
        (path ?? string.Empty)
        .Replace(AlternativeSeparator, Separator)
        .Split(Separator, StringSplitOptions.RemoveEmptyEntries);

    private static bool IsSafeSegment(string segment) =>
        segment.Length > 0 && segment is not ("." or "..") && !segment.Contains('\0');
}
