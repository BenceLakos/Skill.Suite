using Skill.Suite.Marker.Cli;
using Xunit;

namespace Skill.Suite.Marker.Tests;

/// <summary>
/// Covers the command-line grammar, including the shapes that must be rejected with exit code 2.
/// </summary>
public sealed class CommandLineTests
{
    [Fact]
    public void Score_ParsesEveryOption()
    {
        var command = Parse(
            "score", "--map", "m.json", "--events", "e.jsonl",
            "--trx", "r.trx", "--coverage", "c.xml", "--mutation", "s.json");

        Assert.Equal(MarkerVerb.Score, command.Verb);
        Assert.Equal("m.json", command.MapPath);
        Assert.Equal("e.jsonl", command.EventsPath);
        Assert.Equal(["r.trx"], command.Trx);
        Assert.Equal(["c.xml"], command.Coverage);
        Assert.Equal("s.json", command.MutationPath);
    }

    [Fact]
    public void Score_AcceptsAnExpandedShellGlobAsMultipleValues()
    {
        // The documented invocation is --trx "$TEST_RESULTS"/*.trx, which the shell expands into several
        // bare tokens. The original parser rejected everything after the first as a stray positional.
        var command = Parse("score", "--map", "m.json", "--events", "e.jsonl",
            "--trx", "a.trx", "b.trx", "c.trx");

        Assert.Equal(["a.trx", "b.trx", "c.trx"], command.Trx);
    }

    [Fact]
    public void Score_AcceptsRepeatedFlagsAndAccumulatesThem()
    {
        var command = Parse("score", "--map", "m.json", "--events", "e.jsonl",
            "--coverage", "a.xml", "--coverage", "b.xml");

        Assert.Equal(["a.xml", "b.xml"], command.Coverage);
    }

    [Fact]
    public void Report_RequiresAnOutputPath()
    {
        Assert.Null(CommandLine.Parse(["report", "--map", "m.json", "--events", "e.jsonl"], out var error));
        Assert.Contains("--out", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_ParsesItsOptions()
    {
        var command = Parse("report", "--map", "m.json", "--events", "e.jsonl", "--out", "cis.csv");

        Assert.Equal(MarkerVerb.Report, command.Verb);
        Assert.Equal("cis.csv", command.OutPath);
    }

    [Fact]
    public void Report_RejectsScoreOnlyOptions()
    {
        // Passing --coverage to report would silently do nothing, which reads as a broken tool.
        Assert.Null(CommandLine.Parse(
            ["report", "--map", "m.json", "--events", "e.jsonl", "--out", "o.csv", "--coverage", "c.xml"],
            out var error));
        Assert.Contains("score options", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void Score_RejectsAnOutputPath()
    {
        Assert.Null(CommandLine.Parse(
            ["score", "--map", "m.json", "--events", "e.jsonl", "--out", "o.csv"], out var error));
        Assert.Contains("--out", error!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_IsRecognised(string token)
    {
        Assert.Equal(MarkerVerb.Help, Parse(token).Verb);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("--version")]
    public void Version_IsRecognised(string token)
    {
        Assert.Equal(MarkerVerb.Version, Parse(token).Verb);
    }

    [Fact]
    public void NoArguments_IsAUsageError()
    {
        Assert.Null(CommandLine.Parse([], out var error));
        Assert.Contains("no subcommand", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownSubcommand_IsAUsageError()
    {
        Assert.Null(CommandLine.Parse(["mark"], out var error));
        Assert.Contains("mark", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownOption_IsAUsageError()
    {
        Assert.Null(CommandLine.Parse(
            ["score", "--map", "m.json", "--events", "e.jsonl", "--verbose"], out var error));
        Assert.Contains("--verbose", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void AStrayPositional_IsAUsageError()
    {
        Assert.Null(CommandLine.Parse(["score", "m.json"], out var error));
        Assert.Contains("unexpected argument", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void AFlagWithNoValue_IsAUsageError()
    {
        Assert.Null(CommandLine.Parse(["score", "--events", "e.jsonl", "--map"], out var error));
        Assert.Contains("--map", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ASingleValuedFlagGivenTwoValues_IsAUsageError()
    {
        Assert.Null(CommandLine.Parse(
            ["score", "--events", "e.jsonl", "--map", "a.json", "b.json"], out var error));
        Assert.Contains("one value", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void MapAndEvents_AreBothRequired()
    {
        Assert.Null(CommandLine.Parse(["score", "--events", "e.jsonl"], out var mapError));
        Assert.Contains("--map", mapError!, StringComparison.Ordinal);

        Assert.Null(CommandLine.Parse(["score", "--map", "m.json"], out var eventsError));
        Assert.Contains("--events", eventsError!, StringComparison.Ordinal);
    }

    private static MarkerCommand Parse(params string[] args)
    {
        var command = CommandLine.Parse(args, out var error);
        Assert.Null(error);
        return command!;
    }
}
