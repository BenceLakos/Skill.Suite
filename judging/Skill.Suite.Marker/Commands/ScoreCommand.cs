using Skill.Suite.TestLog.Protocol;
using Skill.Suite.Marker.Cli;
using Skill.Suite.Marker.Map;
using Skill.Suite.Marker.Model;
using Skill.Suite.Marker.Output;
using Skill.Suite.Marker.Readers;
using Skill.Suite.Marker.Scoring;

namespace Skill.Suite.Marker.Commands;

/// <summary>
/// The <c>score</c> subcommand: read the reports, append the metric events.
/// </summary>
public static class ScoreCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="command">The parsed command line.</param>
    /// <param name="map">The validated marking map.</param>
    public static void Run(MarkerCommand command, MarkingMap map)
    {
        var router = new PartRouter(map.Parts);

        var counts = TrxReader.Read(command.Trx, router);
        var coverage = CoberturaReader.Read(command.Coverage, router);
        var mutation = StrykerReader.Read(command.MutationPath, router);

        var fixtureCoverage = FixtureCoverageReader.Read(command.FixtureCoveragePath);
        var fixtureMutation = StrykerReader.ReadByFixture(command.MutationPath);

        using var sink = new EventSink(command.EventsPath);

        var warnings = MissingInputWarnings(command, fixtureMutation);
        foreach (var warning in warnings)
            Console.Error.WriteLine($"skill-marker: {warning}");

        var scorer = new Scorer(map.Scoring);

        foreach (var part in map.ScoredParts())
        {
            var partCounts = counts.GetValueOrDefault(part);
            var partCoverage = coverage.GetValueOrDefault(part);
            var partMutation = mutation.GetValueOrDefault(part);
            var score = scorer.Score(part, partCounts, partCoverage, partMutation);

            // Warnings ride on the rollup rather than becoming their own event: the platform turns every part
            // name into a fixture, so a warning on a distinct part would materialise as a junk fixture.
            var partWarnings = warnings.Count > 0 && part == MarkingMap.OverallPart ? warnings : null;

            sink.Write(new TestSummaryEvent(
                Part: part,
                Value: Scorer.Round(partCounts.PassRate),
                Total: partCounts.Total,
                Passed: partCounts.Passed,
                Failed: partCounts.Failed,
                Skipped: partCounts.Skipped,
                Warnings: partWarnings));

            sink.Write(new CoverageEvent(
                Part: part,
                Value: Scorer.Round(partCoverage.Rate),
                Total: partCoverage.LinesTotal,
                Covered: partCoverage.LinesCovered));

            sink.Write(new MutationEvent(
                Part: part,
                Value: Scorer.Round(partMutation.KillRate),
                Total: partMutation.Total,
                Covered: partMutation.Covered,
                Killed: partMutation.Killed,
                Survived: partMutation.Survived,
                Timeout: partMutation.Timeout,
                NoCoverage: partMutation.NoCoverage,
                Other: partMutation.Other));

            // The composite. Its value is the one number the platform reads; the ratios it came from are
            // nested rather than flattened, so a reader cannot confuse a component with the verdict.
            sink.Write(new ScoreEvent(
                Part: part,
                Value: score.Quality,
                Inputs: new ScoreInputs(
                    PassRate: Scorer.Round(partCounts.PassRate),
                    LineCoverage: Scorer.Round(partCoverage.Rate),
                    ScaledCoverage: score.ScaledCoverage,
                    PassingCoverage: score.PassingCoverage,
                    MutationKillRate: Scorer.Round(partMutation.KillRate),
                    MutationScore: score.MutationScore,
                    Core: score.Core)));
        }

        WriteFixtureMetrics(command, sink, fixtureCoverage, fixtureMutation);
    }

    /// <summary>
    /// Appends the per-test-class measurements, after every part has been scored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Strictly additive, and deliberately carries no score. A part is a scoring unit declared in the marking
    /// map; a fixture is a class the competitor happened to write. Scoring one would invent marks the map never
    /// authorised, and letting a fixture name reach the <c>part</c> field would collide the two name spaces in
    /// the platform's fixture table — which is the whole reason these events carry <c>fixture</c> instead.
    /// </para>
    /// <para>
    /// Absence is reported as absence: a class with no coverage report gets no coverage event, rather than a
    /// zero that reads as "this class covered nothing".
    /// </para>
    /// </remarks>
    private static void WriteFixtureMetrics(
        MarkerCommand command,
        EventSink sink,
        IReadOnlyDictionary<string, CoverageStats> fixtureCoverage,
        FixtureMutation fixtureMutation)
    {
        foreach (var fixture in Fixtures(command))
        {
            if (fixtureCoverage.TryGetValue(fixture, out var lines))
            {
                sink.Write(new CoverageEvent(
                    Part: null,
                    Value: Scorer.Round(lines.Rate),
                    Total: lines.LinesTotal,
                    Covered: lines.LinesCovered,
                    Fixture: fixture));
            }

            if (fixtureMutation.ByFixture.TryGetValue(fixture, out var mutants))
            {
                sink.Write(new MutationEvent(
                    Part: null,
                    Value: Scorer.Round(mutants.KillRate),
                    Total: mutants.Total,
                    Covered: mutants.Covered,
                    Killed: mutants.Killed,
                    Survived: mutants.Survived,
                    Timeout: mutants.Timeout,
                    NoCoverage: mutants.NoCoverage,
                    Other: mutants.Other,
                    Fixture: fixture));
            }
        }
    }

    /// <summary>
    /// Every test class the run knows about, from both sources that know any of them.
    /// </summary>
    /// <remarks>
    /// Sorted so a diff of two runs of the same submission is about the numbers rather than about enumeration
    /// order.
    /// </remarks>
    private static IEnumerable<string> Fixtures(MarkerCommand command)
    {
        var fixtures = new SortedSet<string>(StringComparer.Ordinal);
        fixtures.UnionWith(TrxReader.ReadFixtureNames(command.Trx));
        fixtures.UnionWith(EventReplay.ReadFixtureNames(command.EventsPath));
        return fixtures;
    }

    /// <summary>
    /// Names the absent inputs rather than failing on them. A white-box session legitimately has no
    /// mutation report — but a silently absent one is indistinguishable from a suite that killed nothing.
    /// </summary>
    private static List<string> MissingInputWarnings(MarkerCommand command, FixtureMutation fixtureMutation)
    {
        var warnings = new List<string>();
        var hasMutationReport =
            !string.IsNullOrWhiteSpace(command.MutationPath) && File.Exists(command.MutationPath);

        if (!command.Trx.Any(File.Exists))
            warnings.Add("no-trx: no readable TRX file was given, so every pass rate is 0.");

        if (!command.Coverage.Any(File.Exists))
            warnings.Add("no-coverage: no readable coverage file was given, so coverage is 0.");

        if (!hasMutationReport)
            warnings.Add("no-mutation-report: no readable mutation report was given, so the mutation score is 0.");

        // Only worth saying when there was a report to read. Said at all because the alternative — a mutation
        // event reading 0 for every test class — looks like a finding about the submission rather than about
        // how the judge image was configured.
        else if (!fixtureMutation.PerTest)
        {
            warnings.Add(
                "no-per-fixture-mutation: the mutation report carries no per-test data, so no per-fixture "
                + "mutation was computed. Set \"coverage-analysis\": \"perTest\" and \"disable-bail\": true in "
                + "stryker-config.json.");
        }

        return warnings;
    }
}
