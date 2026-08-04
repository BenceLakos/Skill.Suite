namespace Skill.Suite.Marker.Cli;

/// <summary>The usage text, printed to stderr on a usage error and to stdout for <c>help</c>.</summary>
public static class Usage
{
    /// <summary>The full usage text.</summary>
    public const string Text = """
        skill-marker - post-processing marker for Skill Suite judgement runs

        Usage:
          skill-marker score  --map <marking-map.json> --events <events.jsonl>
                              [--trx <results.trx>...] [--coverage <cobertura.xml>...]
                              [--mutation <mutation-report.json>]

          skill-marker report --map <marking-map.json> --events <events.jsonl>
                              --out <cis-report.csv>

          skill-marker help | --help | -h
          skill-marker version | --version

        score
          Reads the given reports and APPENDS test-summary, coverage, mutation and score events to
          the events file, one set per declared part plus "overall". A missing input is named in the
          overall test-summary's warnings rather than failing the run, so a session without mutation
          testing still scores.

        report
          Groups finished unit tests by their [Aspect] id and writes:
              aspect_id,result,tests_matched,tests_passed
          An aspect is "yes" only when at least one test matched it and all matched tests passed.
          Aspects declared in the map with no matching test are reported "no". No marks are
          computed here - CIS does that.

        Notes
          --trx and --coverage accept several paths, so a shell glob can be passed directly.
          Exit codes: 0 success, 1 processing error (a marker-error event is emitted first),
          2 usage error.
        """;
}
