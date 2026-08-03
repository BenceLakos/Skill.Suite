using System.Runtime.CompilerServices;

namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// Reads a checked-in golden file, and on mismatch writes the actual output next to it for diffing.
/// </summary>
internal static class GoldenFile
{
    /// <summary>Reads the golden lines, normalized the same way captured output is.</summary>
    internal static string[] Read(string name) =>
        EventNormalizer.NormalizeAll(File.ReadAllLines(PathTo(name)));

    /// <summary>
    /// Writes what the producer actually emitted to <c>&lt;name&gt;.actual</c> in the source tree, so a
    /// failing run leaves a diffable artifact rather than only an assertion message.
    /// </summary>
    internal static string WriteActual(string name, IEnumerable<string> lines)
    {
        var path = SourcePathTo(name) + ".actual";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        return path;
    }

    /// <summary>Golden file as copied next to the test binary.</summary>
    internal static string PathTo(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", name);

    /// <summary>Golden file in the source tree, resolved from this file's compile-time path.</summary>
    private static string SourcePathTo(string name, [CallerFilePath] string callerPath = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(callerPath)!)!, "Golden", name);
}
