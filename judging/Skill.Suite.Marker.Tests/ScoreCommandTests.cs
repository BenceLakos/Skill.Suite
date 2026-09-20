using Skill.Suite.TestLog.Protocol;
using System.Text.Json;
using Skill.Suite.Marker.Cli;
using Skill.Suite.Marker.Commands;
using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Output;
using Skill.Suite.Marker.Tests.Support;
using Xunit;

namespace Skill.Suite.Marker.Tests;

/// <summary>
/// End-to-end behaviour of <c>score</c>, with the append contract as the headline assertion.
/// </summary>
public sealed class ScoreCommandTests
{
    [Fact]
    public void Score_AppendsToTheEventsFileAndKeepsTheTestEvents()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.CopyFixture(Fixture.EventsWithAspects, "events.jsonl");
        var before = File.ReadAllLines(events).Length;

        Run(workspace, events, Fixture.MapTwoParts);

        var lines = File.ReadAllLines(events);

        // The whole point: the original opened this file with FileMode.Create, erasing every test event the
        // run had just produced and leaving a submission that looked like it executed nothing.
        Assert.True(lines.Length > before, "score must append, never truncate");
        Assert.Contains(lines, line => line.Contains("\"event\":\"start-unit-test\"", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Validate_WellFormed_Passes", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_EmitsFourEventsForEveryPartPlusOverall()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        Run(workspace, events, Fixture.MapTwoParts);

        var appended = ParseEvents(events);

        foreach (var part in new[] { "services", "program", "overall" })
        {
            foreach (var kind in new[] { "test-summary", "coverage", "mutation", "score" })
            {
                Assert.Single(appended, e => e.Event == kind && e.Part == part);
            }
        }

        Assert.Equal(12, appended.Count);
    }

    [Fact]
    public void Score_WritesQualityAsANumberInRange()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        Run(workspace, events, Fixture.MapTwoParts);

        var score = ParseEvents(events).Single(e => e.Event == "score" && e.Part == "services");
        var quality = score.Root.GetProperty("value");

        // A string here is silently ignored by the platform, so the competitor would see no score at all.
        Assert.Equal(JsonValueKind.Number, quality.ValueKind);
        Assert.Equal(0.795, quality.GetDouble(), 4);
    }

    [Fact]
    public void Score_CarriesTheHandComputedCoverageThrough()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        Run(workspace, events, Fixture.MapTwoParts);
        var appended = ParseEvents(events);

        var overall = appended.Single(e => e.Event == "coverage" && e.Part == "overall");
        Assert.Equal(300, overall.Root.GetProperty("total").GetInt32());
        Assert.Equal(269, overall.Root.GetProperty("covered").GetInt32());

        var services = appended.Single(e => e.Event == "coverage" && e.Part == "services");
        Assert.Equal(289, services.Root.GetProperty("total").GetInt32());
    }

    [Fact]
    public void Score_WithNoInputs_WarnsOnTheRollupInsteadOfInventingFixtures()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        ScoreCommand.Run(
            new MarkerCommand(MarkerVerb.Score, Fixture.Path(Fixture.MapTwoParts), events),
            MarkingMapLoader.Load(Fixture.Path(Fixture.MapTwoParts)));

        var appended = ParseEvents(events);
        var overall = appended.Single(e => e.Event == "test-summary" && e.Part == "overall");
        var warnings = overall.Root.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToArray();

        Assert.Equal(3, warnings.Length);
        Assert.Contains(warnings, w => w.StartsWith("no-trx", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.StartsWith("no-coverage", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.StartsWith("no-mutation-report", StringComparison.Ordinal));

        // Warnings must not create their own parts: the platform turns every part name into a fixture, so a
        // warning part would show up as a junk fixture in the UI.
        Assert.All(appended, e => Assert.Contains(e.Part, new[] { "services", "program", "overall" }));

        // Every score is still emitted, so the run reports zero rather than nothing.
        Assert.Equal(3, appended.Count(e => e.Event == "score"));
    }

    [Fact]
    public void Score_OnAFileEndingMidLine_DoesNotGlueOntoThePartialLine()
    {
        using var workspace = new TempWorkspace();

        // A SIGKILLed test host leaves exactly this.
        var events = workspace.WriteFile("events.jsonl",
            "{\"timestamp\":\"2026-07-30T10:00:00.000Z\",\"event\":\"start-fixture\",\"fixture\":\"F\"}\n{\"partial\":tru");

        Run(workspace, events, Fixture.MapTwoParts);

        var lines = File.ReadAllLines(events);
        Assert.Equal("{\"partial\":tru", lines[1]);
        Assert.StartsWith("{\"event\"", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Score_WithOverallOnlyMap_EmitsOneSetOfEvents()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        Run(workspace, events, Fixture.MapOverallOnly);

        var appended = ParseEvents(events);
        Assert.Equal(4, appended.Count);
        Assert.All(appended, e => Assert.Equal("overall", e.Part));
    }

    [Fact]
    public void Score_EmitsCoverageAndMutationPerTestClass()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        RunWithFixtureMetrics(events, Fixture.MapTwoParts);

        var fixtures = ParseFixtureEvents(events);

        foreach (var name in new[] { "WidgetTests", "SmokeTests" })
        {
            Assert.Single(fixtures, e => e.Event == "coverage" && e.Fixture == name);
            Assert.Single(fixtures, e => e.Event == "mutation" && e.Fixture == name);
        }

        // MiscTests ran, so it is mutated; it just has no coverage report of its own.
        Assert.Single(fixtures, e => e.Event == "mutation" && e.Fixture == "MiscTests");
        Assert.DoesNotContain(fixtures, e => e.Event == "coverage" && e.Fixture == "MiscTests");
    }

    [Fact]
    public void Score_CarriesTheHandComputedFixtureNumbersThrough()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        RunWithFixtureMetrics(events, Fixture.MapTwoParts);
        var fixtures = ParseFixtureEvents(events);

        var coverage = fixtures.Single(e => e.Event == "coverage" && e.Fixture == "WidgetTests");
        Assert.Equal(8, coverage.Root.GetProperty("total").GetInt32());
        Assert.Equal(6, coverage.Root.GetProperty("covered").GetInt32());
        Assert.Equal(0.75, coverage.Root.GetProperty("value").GetDouble());

        var mutation = fixtures.Single(e => e.Event == "mutation" && e.Fixture == "SmokeTests");
        Assert.Equal(3, mutation.Root.GetProperty("covered").GetInt32());
        Assert.Equal(1, mutation.Root.GetProperty("killed").GetInt32());
        Assert.Equal(1, mutation.Root.GetProperty("survived").GetInt32());
    }

    [Fact]
    public void Score_NeverScoresAFixtureAndNeverNamesOneAsAPart()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        RunWithFixtureMetrics(events, Fixture.MapTwoParts);
        var appended = ParseEvents(events);

        // The contract the platform's fixture table depends on: quality is a part-level verdict, and a test
        // class is not a part. Mixing the two name spaces would attach a score to a competitor's own class.
        Assert.All(appended.Where(e => e.Event is "score" or "test-summary"),
            e => Assert.False(e.Root.TryGetProperty("fixture", out _)));

        Assert.Equal(3, appended.Count(e => e.Event == "score"));
        Assert.All(appended.Where(e => e.Root.TryGetProperty("fixture", out _)),
            e => Assert.False(e.Root.TryGetProperty("part", out var part) && part.ValueKind != JsonValueKind.Null));
    }

    [Fact]
    public void Score_WithoutPerTestMutationData_WarnsInsteadOfPublishingZeroes()
    {
        using var workspace = new TempWorkspace();
        var events = workspace.WriteFile("events.jsonl", "");

        // The rollup report: valid, and simply unable to say which class killed what.
        Run(workspace, events, Fixture.MapTwoParts);

        var appended = ParseEvents(events);
        var overall = appended.Single(e => e.Event == "test-summary" && e.Part == "overall");
        var warnings = overall.Root.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToArray();

        Assert.Contains(warnings, w => w.StartsWith("no-per-fixture-mutation", StringComparison.Ordinal));
        Assert.DoesNotContain(appended, e => e.Root.TryGetProperty("fixture", out _));
    }

    [Fact]
    public void EventSink_CreatesTheFileWhenItIsAbsent()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.PathTo("fresh.jsonl");

        using (var sink = new EventSink(path))
        {
            sink.Write(new ScoreEvent(MetricEvent.OverallPart, 0.5));
        }

        Assert.Single(File.ReadAllLines(path));
    }

    private static void Run(TempWorkspace workspace, string events, string mapName)
    {
        _ = workspace;
        var mapPath = Fixture.Path(mapName);

        ScoreCommand.Run(
            new MarkerCommand(
                MarkerVerb.Score, mapPath, events,
                TrxPaths: [Fixture.Path(Fixture.Trx)],
                CoveragePaths: [Fixture.Path(Fixture.Cobertura)],
                MutationPath: Fixture.Path(Fixture.Stryker)),
            MarkingMapLoader.Load(mapPath));
    }

    /// <summary>The same run with the per-fixture inputs a black-box judge image supplies.</summary>
    private static void RunWithFixtureMetrics(string events, string mapName)
    {
        var mapPath = Fixture.Path(mapName);

        ScoreCommand.Run(
            new MarkerCommand(
                MarkerVerb.Score, mapPath, events,
                TrxPaths: [Fixture.Path(Fixture.Trx)],
                CoveragePaths: [Fixture.Path(Fixture.Cobertura)],
                MutationPath: Fixture.Path(Fixture.StrykerPerTest),
                FixtureCoveragePath: Fixture.Path(Fixture.FixtureCoverage)),
            MarkingMapLoader.Load(mapPath));
    }

    private static List<AppendedEvent> ParseFixtureEvents(string path) =>
        [.. ParseEvents(path).Where(e => e.Fixture is not null)];

    /// <summary>
    /// Reads the metric events back through the shared reader rather than probing JSON by hand.
    /// </summary>
    /// <remarks>
    /// The raw <see cref="JsonElement"/> is kept alongside the typed record because a few assertions here are
    /// deliberately about the wire shape - that a value really is a JSON number, for instance - which the typed
    /// view would hide.
    /// </remarks>
    private static List<AppendedEvent> ParseEvents(string path)
    {
        var events = new List<AppendedEvent>();

        foreach (var line in File.ReadAllLines(path))
        {
            if (TestLogEventReader.Read(line) is not MetricEvent metric) continue;

            using var document = JsonDocument.Parse(line);
            var fixture = metric switch
            {
                CoverageEvent e => e.Fixture,
                MutationEvent e => e.Fixture,
                _ => null,
            };

            events.Add(new AppendedEvent(
                metric.Event, metric.PartOrOverall, document.RootElement.Clone(), metric, fixture));
        }

        return events;
    }

    private sealed record AppendedEvent(
        string Event, string Part, JsonElement Root, MetricEvent Typed, string? Fixture = null);
}
