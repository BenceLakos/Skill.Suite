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

        using var sink = new EventSink(command.EventsPath);

        var warnings = MissingInputWarnings(command);
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
    }

    /// <summary>
    /// Names the absent inputs rather than failing on them. A white-box session legitimately has no
    /// mutation report — but a silently absent one is indistinguishable from a suite that killed nothing.
    /// </summary>
    private static List<string> MissingInputWarnings(MarkerCommand command)
    {
        var warnings = new List<string>();

        if (!command.Trx.Any(File.Exists))
            warnings.Add("no-trx: no readable TRX file was given, so every pass rate is 0.");

        if (!command.Coverage.Any(File.Exists))
            warnings.Add("no-coverage: no readable coverage file was given, so coverage is 0.");

        if (string.IsNullOrWhiteSpace(command.MutationPath) || !File.Exists(command.MutationPath))
            warnings.Add("no-mutation-report: no readable mutation report was given, so the mutation score is 0.");

        return warnings;
    }
}
