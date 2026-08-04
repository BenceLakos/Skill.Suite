using Skill.Suite.Marker.Commands;
using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Model;
using Skill.Suite.Marker.Readers;
using Skill.Suite.Marker.Tests.Support;
using Xunit;

namespace Skill.Suite.Marker.Tests;

/// <summary>
/// The per-aspect yes/no truth table, and the replay it is built from.
/// </summary>
public sealed class ReportTests
{
    [Fact]
    public void Replay_ReconstructsVerdictsFromTheStream()
    {
        var units = EventReplay.Read(Fixture.Path(Fixture.EventsWithAspects));

        Assert.Equal(6, units.Count);
        Assert.Equal(UnitOutcome.Passed, Find(units, "Validate_WellFormed_Passes").Outcome);
        Assert.Equal(UnitOutcome.Failed, Find(units, "Validate_Unknown_Fails").Outcome);
        Assert.Null(Find(units, "Diagnostics_Smoke_Runs").Aspect);
    }

    [Fact]
    public void Replay_WillNotLetAnOptimisticOutcomeUndoAThrow()
    {
        // Optimize_Tie_LowestIndexWins logs a call with `threw` and then claims outcome "passed". The
        // platform refuses that downgrade, so the report must too - otherwise a suite could earn an aspect
        // by emitting a cheerful final outcome, and the CSV would contradict the UI for the same run.
        var units = EventReplay.Read(Fixture.Path(Fixture.EventsWithAspects));

        Assert.Equal(UnitOutcome.Errored, Find(units, "Optimize_Tie_LowestIndexWins").Outcome);
    }

    [Fact]
    public void Replay_DropsEventsForAUnitThatNeverStarted()
    {
        var units = EventReplay.Read(Fixture.Path(Fixture.EventsWithAspects));

        // Matches the platform: a finish without a start is discarded rather than inventing a unit.
        Assert.DoesNotContain(units, unit => unit.Test == "Never_Started_Ghost");
    }

    [Fact]
    public void Replay_SkipsUnparsableLines()
    {
        // The fixture contains a line of plain text. One corrupt line must not sink the report.
        Assert.Equal(6, EventReplay.Read(Fixture.Path(Fixture.EventsWithAspects)).Count);
    }

    [Fact]
    public void Replay_OnAMissingFile_Fails()
    {
        Assert.Throws<FileNotFoundException>(() => EventReplay.Read("/nowhere/events.jsonl"));
    }

    [Fact]
    public void Aspects_FollowTheTruthTable()
    {
        var map = MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts));
        var results = ReportCommand.Aggregate(EventReplay.Read(Fixture.Path(Fixture.EventsWithAspects)), map);

        // A1.1: two tests, both passed          -> yes
        // A1.2: one test, failed                -> no
        // C2.3: one test, errored via call.threw-> no
        // Z9.9: declared, no test matched       -> no, and still present
        // Q1.1: found in the stream, undeclared -> appended, one test passed -> yes
        Assert.Equal(
            [("A1.1", "yes", 2, 2), ("A1.2", "no", 1, 0), ("C2.3", "no", 1, 0), ("Z9.9", "no", 0, 0), ("Q1.1", "yes", 1, 1)],
            results.Select(r => (r.AspectId, r.Result, r.TestsMatched, r.TestsPassed)));
    }

    [Fact]
    public void DeclaredAspectsKeepTheirMapOrderSoTheCsvIsStable()
    {
        var map = MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts));
        var results = ReportCommand.Aggregate(EventReplay.Read(Fixture.Path(Fixture.EventsWithAspects)), map);

        Assert.Equal(
            map.Aspects.Select(aspect => aspect.Id),
            results.Take(map.Aspects.Count).Select(result => result.AspectId));
    }

    [Theory]
    [InlineData(UnitOutcome.Passed, "yes")]
    [InlineData(UnitOutcome.Failed, "no")]
    [InlineData(UnitOutcome.Errored, "no")]
    [InlineData(UnitOutcome.Skipped, "no")]
    [InlineData(UnitOutcome.Unknown, "no")]
    public void ASingleTest_DecidesItsAspect(UnitOutcome outcome, string expected)
    {
        var map = new MarkingMap { Aspects = [new AspectRule { Id = "A1.1" }] };
        var units = new[] { new UnitRecord("F", "T", "A1.1", outcome) };

        Assert.Equal(expected, ReportCommand.Aggregate(units, map).Single().Result);
    }

    [Fact]
    public void OneFailingTest_SinksTheWholeAspect()
    {
        var map = new MarkingMap { Aspects = [new AspectRule { Id = "A1.1" }] };
        var units = new[]
        {
            new UnitRecord("F", "T1", "A1.1", UnitOutcome.Passed),
            new UnitRecord("F", "T2", "A1.1", UnitOutcome.Passed),
            new UnitRecord("F", "T3", "A1.1", UnitOutcome.Failed),
        };

        var result = ReportCommand.Aggregate(units, map).Single();

        Assert.Equal("no", result.Result);
        Assert.Equal(3, result.TestsMatched);
        Assert.Equal(2, result.TestsPassed);
    }

    [Fact]
    public void AnAspectSpanningFixtures_IsGradedAsOne()
    {
        // The competitor-facing bucket is per fixture, but the report reads the whole stream, so an aspect
        // split across two fixtures is still one grading unit here.
        var map = new MarkingMap { Aspects = [new AspectRule { Id = "A1.1" }] };
        var units = new[]
        {
            new UnitRecord("FixtureA", "T1", "A1.1", UnitOutcome.Passed),
            new UnitRecord("FixtureB", "T2", "A1.1", UnitOutcome.Passed),
        };

        Assert.Equal(("yes", 2), ReportCommand.Aggregate(units, map).Single() is var only
            ? (only.Result, only.TestsMatched)
            : default);
    }

    [Fact]
    public void Run_WritesTheCsvWithItsHeaderAndCreatesTheDirectory()
    {
        using var workspace = new TempWorkspace();
        var outPath = Path.Combine(workspace.Path, "nested", "cis-report.csv");

        ReportCommand.Run(
            new Skill.Suite.Marker.Cli.MarkerCommand(
                Skill.Suite.Marker.Cli.MarkerVerb.Report,
                Fixture.Path(Fixture.MapTwoParts),
                Fixture.Path(Fixture.EventsWithAspects),
                OutPath: outPath),
            MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts)));

        var lines = File.ReadAllLines(outPath);

        Assert.Equal(ReportCommand.Header, lines[0]);
        Assert.Equal("A1.1,yes,2,2", lines[1]);
        Assert.Equal("A1.2,no,1,0", lines[2]);
        Assert.Equal("C2.3,no,1,0", lines[3]);
        Assert.Equal("Z9.9,no,0,0", lines[4]);
        Assert.Equal("Q1.1,yes,1,1", lines[5]);
    }

    [Fact]
    public void Run_QuotesAnAspectIdContainingASeparator()
    {
        using var workspace = new TempWorkspace();
        var map = workspace.WriteFile("map-comma.json", """{"aspects":[{"id":"A,1"}]}""");
        var events = workspace.CopyFixture(Fixture.EventsWithAspects, "events.jsonl");
        var outPath = workspace.PathTo("report.csv");

        ReportCommand.Run(
            new Skill.Suite.Marker.Cli.MarkerCommand(
                Skill.Suite.Marker.Cli.MarkerVerb.Report, map, events, OutPath: outPath),
            MarkingMapLoader.Load(map));

        Assert.Contains("\"A,1\",no,0,0", File.ReadAllText(outPath), StringComparison.Ordinal);
    }

    private static UnitRecord Find(IReadOnlyList<UnitRecord> units, string test) =>
        units.Single(unit => unit.Test == test);
}
