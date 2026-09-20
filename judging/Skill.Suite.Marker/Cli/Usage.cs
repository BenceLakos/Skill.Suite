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
                              [--fixture-coverage <per-fixture-coverage-dir>]

          skill-marker report --map <marking-map.json> --events <events.jsonl>
                              --out <cis-report.csv>

          skill-marker help | --help | -h
          skill-marker version | --version

        score
          Reads the given reports and APPENDS test-summary, coverage, mutation and score events to
          the events file, one set per declared part plus "overall". A missing input is named in the
          overall test-summary's warnings rather than failing the run, so a session without mutation
          testing still scores.

          It then appends a coverage and a mutation event per TEST FIXTURE - one per test class in
          the submission, carrying "fixture" instead of "part". These are measurements only: the
          quality score is still computed per part and per "overall", and a fixture never gets one.

          --fixture-coverage names a directory whose IMMEDIATE SUBDIRECTORIES are test class names,
          each holding that class's own coverage run - the shape produced by one filtered
          `dotnet test --collect:"XPlat Code Coverage"` per class:

              fixture-coverage/CalculatorTests/<guid>/coverage.cobertura.xml

          A class with no report there gets no coverage event, rather than a zero. Per-fixture
          mutation comes from the SAME Stryker report as the rollup, via testFiles/coveredBy/killedBy,
          so it needs "coverage-analysis": "perTest" and "disable-bail": true - with bail on, Stryker
          stops at the first killing test and every other class looks as though it missed the mutant.
          A report without that data is reported as a warning instead of as a page of zeroes.

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
