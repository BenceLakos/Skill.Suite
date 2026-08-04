using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Skill.Suite.StarterKit;

/// <summary>
/// Produces the folder a competitor is handed, from the reference implementation as it stands right now.
/// </summary>
/// <remarks>
/// Generated rather than maintained by hand: a checked-in starter kit drifts the moment the author touches the
/// reference implementation, and the drift is invisible until a competitor cannot compile what they were given.
/// Re-run this whenever the implementation or the contract changes.
/// </remarks>
public sealed class StarterKitGenerator
{
    private static readonly string[] SkippedDirectories = ["bin", "obj", ".vs", ".idea", "_design"];

    /// <summary>Writes the starter kit for <paramref name="sourceProjectDirectory"/> into
    /// <paramref name="outputDirectory"/>, replacing whatever was there.</summary>
    /// <param name="sourceProjectDirectory">The reference project to derive from.</param>
    /// <param name="outputDirectory">Where to write the starter kit. Recreated, not merged.</param>
    /// <param name="kind">
    /// Which rewrite to apply. The two are inverses — see <see cref="StarterKitKind"/> — and choosing wrong
    /// hands out the answer, so it is never inferred from the folder name.
    /// </param>
    public StarterKitResult Generate(
        string sourceProjectDirectory,
        string outputDirectory,
        StarterKitKind kind = StarterKitKind.WhiteBox)
    {
        if (!Directory.Exists(sourceProjectDirectory))
            throw new DirectoryNotFoundException($"Source project directory not found: {sourceProjectDirectory}");

        var csproj = Directory.EnumerateFiles(sourceProjectDirectory, "*.csproj").FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No .csproj in {sourceProjectDirectory}. Point this at the reference implementation's project folder.");

        // Recreated, never merged. A stale stub left behind from a member that has since been deleted would
        // otherwise sit in the starter kit and fail to compile against the current contract.
        if (Directory.Exists(outputDirectory))
            Directory.Delete(outputDirectory, recursive: true);
        Directory.CreateDirectory(outputDirectory);

        // Copied verbatim: the competitor needs the same package references and target framework, and the
        // judge replaces this folder wholesale so the csproj must already be the one the image expects.
        File.Copy(csproj, Path.Combine(outputDirectory, Path.GetFileName(csproj)));

        var stubbed = new List<string>();
        foreach (var file in EnumerateSources(sourceProjectDirectory))
        {
            var relative = Path.GetRelativePath(sourceProjectDirectory, file);
            var target = Path.Combine(outputDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            File.WriteAllText(target, Rewrite(File.ReadAllText(file), kind));
            stubbed.Add(relative);
        }

        return new StarterKitResult(Path.GetFileName(csproj), stubbed);
    }

    private static IEnumerable<string> EnumerateSources(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(directory, f)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => SkippedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(f => f, StringComparer.Ordinal);

    private static string Rewrite(string source, StarterKitKind kind)
    {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source));
        CSharpSyntaxRewriter rewriter = kind switch
        {
            StarterKitKind.BlackBox => new TestSuiteSkeletonRewriter(),
            _ => new ImplementationStubber(),
        };

        var stubbed = (CompilationUnitSyntax)rewriter.Visit(tree.GetRoot())!;

        // Reformatted wholesale. Gutting a member leaves the removed body's trivia stranded - the arrow ends up
        // at column zero, the closing brace glues to the last member - and repairing that token by token is
        // both fiddly and easy to get subtly wrong. This is a generated file, so uniform formatting is the
        // better trade than a faithful echo of a layout whose bodies no longer exist.
        return stubbed.NormalizeWhitespace().ToFullString() + Environment.NewLine;
    }
}
