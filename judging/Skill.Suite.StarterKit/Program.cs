using Skill.Suite.StarterKit;

// skill-starter <reference-project-dir> <output-dir>
//
// Regenerates the competitor starter kit from the reference implementation. Deliberately a tool rather than a
// checked-in folder: a hand-maintained starter kit drifts as soon as the implementation changes, and nobody
// notices until a competitor cannot compile what they were handed.

if (args.Length is 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine("""
        usage: skill-starter <reference-project-dir> <output-dir> [--kind whitebox|blackbox]

          skill-starter ./My.Session.Services  ./competitor-start/My.Session.Services
          skill-starter ./My.Session.UnitTests ./competitor-start/My.Session.UnitTests --kind blackbox

        Copies the .csproj verbatim and rewrites every source file. The two modes are inverses, because the two
        session types are:

        --kind whitebox (default)  the competitor implements the services, so the implementation is stubbed
          * public members keep their exact declaration and get a NotImplementedException body
          * non-public members are removed - a private helper's name gives away the approach
          * fields are removed unless const - a lookup table is the answer in data form

        --kind blackbox            the competitor writes the tests, so the reference suite is emptied
          * every method is removed - the tests ARE the answer
          * private fields and constructors are KEPT - in a suite those are the harness wiring, not the answer
          * a commented-out example test is added, so the delivered project compiles with zero tests

        The output directory is recreated, not merged, so a stub for a member you have since deleted cannot
        survive into what competitors receive.
        """);
    return 0;
}

var kind = StarterKitKind.WhiteBox;
var positional = new List<string>();

for (var i = 0; i < args.Length; i++)
{
    if (args[i] is not ("--kind" or "-k"))
    {
        positional.Add(args[i]);
        continue;
    }

    if (i + 1 >= args.Length)
    {
        Console.Error.WriteLine("skill-starter: --kind needs a value (whitebox or blackbox).");
        return 2;
    }

    switch (args[++i].ToLowerInvariant())
    {
        case "whitebox": kind = StarterKitKind.WhiteBox; break;
        case "blackbox": kind = StarterKitKind.BlackBox; break;
        default:
            Console.Error.WriteLine($"skill-starter: unknown kind '{args[i]}'. Use whitebox or blackbox.");
            return 2;
    }
}

if (positional.Count != 2)
{
    Console.Error.WriteLine("skill-starter: expected a source and an output directory. Run with --help.");
    return 2;
}

try
{
    var result = new StarterKitGenerator().Generate(
        Path.GetFullPath(positional[0]), Path.GetFullPath(positional[1]), kind);

    Console.WriteLine(
        $"skill-starter ({kind}): {result.ProjectFile} + {result.StubbedFiles.Count} rewritten source(s)");
    foreach (var file in result.StubbedFiles)
        Console.WriteLine($"  {file}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"skill-starter: {ex.Message}");
    return 1;
}
