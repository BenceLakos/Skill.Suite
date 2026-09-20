# Skill.Suite.Marker

`skill-marker` — the post-processing step of a Skill Suite judgement run. It reads the artifacts
`dotnet test`, coverlet and Stryker leave behind, appends the aggregate metric events the platform
displays, and produces the per-aspect yes/no report CIS turns into marks.

```bash
dotnet tool install -g Skill.Suite.Marker
```

## score

```bash
skill-marker score --map /app/marking-map.json --events "$LOG_DIRECTORY/events.jsonl" \
  --trx "$TEST_RESULTS"/*.trx \
  --coverage "$TEST_RESULTS"/*/coverage.cobertura.xml \
  --mutation /app/StrykerOutput/reports/mutation-report.json \
  --fixture-coverage "$TEST_RESULTS/fixtures"
```

Appends `test-summary`, `coverage`, `mutation` and `score` — one set per declared part plus `overall` —
and then a `coverage` and a `mutation` event per **test fixture**.

**It appends; it never truncates.** It runs after the test host, and the platform reads the whole file
once the container exits, so opening it for truncation would erase every test event the run produced and
leave a submission that looks like it executed nothing. A file ending mid-line — what a SIGKILLed test
host leaves — is repaired with a newline first.

Missing inputs are warnings, not errors: a white-box session has no mutation report and still scores. The
warnings are attached to the `overall` summary and echoed to stderr. They are *not* emitted under their
own part name, because the platform turns every part into a fixture and that would litter the UI.

### Per-fixture measurements

Alongside the part rollups, `score` measures **each test class on its own** and emits

```json
{"event":"coverage","value":0.75,"total":8,"covered":6,"fixture":"WidgetTests"}
{"event":"mutation","value":0.6667,"total":3,"covered":3,"killed":1,"survived":1,"timeout":1,"fixture":"WidgetTests"}
```

`fixture` replaces `part`, and never appears beside it. A part is a scoring unit the marking map declares; a
fixture is a class the competitor happened to write, and the platform keeps them in separate name spaces so a
test class called `services` cannot inherit a part's score. **No `score` event is ever emitted for a fixture** —
quality stays a part-level verdict, exactly as before. These two are measurements, for showing a competitor
which of their test classes did the work.

The fixture name is the class's **simple** name, the same string `FixtureScope<T>` puts in `start-fixture`, so
the events join onto fixtures already in the stream. The set of fixtures is the union of the classes named in
the TRX and those named in the event stream.

**Coverage** comes from `--fixture-coverage`, a directory whose immediate subdirectories are class names:

```
fixtures/
  WidgetTests/<guid>/coverage.cobertura.xml
  SmokeTests/<guid>/coverage.cobertura.xml
```

A directory rather than `Name=path` pairs, because the caller is a shell script and coverlet buries its report
under an unpredictable GUID directory. Each report is summed whole, with the same direct-child `<line>` rule
the rollup uses, so the numbers are comparable with each other and with `overall`. A class with no report gets
**no event**, not a zero — nothing was measured, which is not the same as nothing was covered.

**Mutation** comes from the same single Stryker report as the rollup, via `testFiles[*].tests[]` (test id →
name) and each mutant's `coveredBy` / `killedBy`. A class's `covered` is the mutants at least one of its tests
reached; `killed` is those at least one of its tests detected; a `Timeout` counts as a kill, as in the rollup.
`total` equals `covered`: a mutant the class never touched is not its business.

That needs two settings in `stryker-config.json`:

```jsonc
"coverage-analysis": "perTest",   // or there is no coveredBy to attribute by
"disable-bail": true              // or Stryker stops at the first killing test
```

With bail on, `killedBy` names one test and every other class that would have caught the same mutant is
recorded as having missed it. A report without per-test data produces a `no-per-fixture-mutation` warning on
the `overall` summary and **no** fixture mutation events — a page of zeroes would read as a finding about the
submission rather than about the image's configuration.

### The quality formula

```
passRate        = passed / (passed + failed)
scaledCoverage  = clamp((linePct     - coverageFloor) / (coverageCeil - coverageFloor), 0, 1)
mutationScore   = clamp((killPct     - mutationFloor) / (mutationCeil - mutationFloor), 0, 1)
passingCoverage = passRate * scaledCoverage
core            = sqrt(passingCoverage * mutationScore)
quality         = coverageWeight * scaledCoverage + coreWeight * core
```

The score's `value` lands in 0..1 and is the one field the platform reads. The geometric mean means both terms have
to be real: covering everything while detecting nothing, or detecting everything in a sliver of the code,
both score near zero. With `mutationScore` at 0 the ceiling is `coverageWeight` alone.

Every emitted metric is rounded to 4 places with banker's rounding, and the score is computed from the
*unrounded* terms.

## report

```bash
skill-marker report --map /app/marking-map.json --events "$LOG_DIRECTORY/events.jsonl" --out cis-report.csv
```

```csv
aspect_id,result,tests_matched,tests_passed
A1.1,yes,2,2
A1.2,no,1,0
Z9.9,no,0,0
```

Test cases claim their aspect with `[Aspect("A1.1")]` from `Skill.Suite.TestLog.Xunit`; the report groups
by that, so no test-name convention is involved. An aspect is `yes` only when at least one test matched it
and **all** matched tests passed — a skipped or errored test is not a pass. Aspects declared in the map
with no matching test still get a `no` row, so an aspect whose tests were never written shows up as
unearned rather than vanishing. Aspects found in the stream but absent from the map are appended, so a
stale map is visible.

**No marks are computed here.** CIS turns these answers into points. The counts are included so a `no`
caused by "nothing covered this aspect" is distinguishable from "4 of 5 passed".

Verdicts are reconstructed from the stream the same way the platform reconstructs them: a `call` carrying
`threw` errors the test and an `assertion` with `passed:false` fails it, and neither can be undone by a
later outcome claiming success. If this diverged, the CSV and the UI would disagree about the same run.

## marking-map.json

```jsonc
{
  "protocol": 1,
  "parts": ["validator", "demand", "optimizer"],   // [] scores "overall" only
  "scoring": {                                      // all optional; defaults shown
    "coverageFloor": 40, "coverageCeil": 80,
    "mutationFloor": 30, "mutationCeil": 80,
    "coverageWeight": 0.30, "coreWeight": 0.70
  },
  "aspects": [
    { "id": "A1.1", "label": "Validates a well-formed program" },
    { "id": "C2.5" }
  ]
}
```

Comments and trailing commas are allowed — this file is hand-edited per session.

A part is a bare string, where the id doubles as the segment to match, or an object when they differ:

```jsonc
{ "id": "aqi", "segments": ["AirWatch.Aqi.Services", "Aqi"] }
```

Routing matches a **whole segment** of a namespace or path, case-insensitively. Dotted values expand, and
paths split on `.`, `/` and `\` — so a file's own stem is a segment too, and part `program` claims
`Program.cs`. Whole-segment matching is deliberate: substring matching claimed types like
`DisbursementServicesTests` for a `services` part.

`overall` is reserved and cannot be declared as a part.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Processing error — a `marker-error` event is appended first, so the run explains itself |
| 2 | Usage error — usage is printed to stderr |

Keep stderr terse: on a non-zero container exit the platform pastes it into the run's failure reason.

`--trx` and `--coverage` accept several paths and may be repeated, so an expanded shell glob works
directly. `--fixture-coverage` takes exactly one directory, because its subdirectory names are the data. A
missing or corrupt TRX, coverage or mutation report is skipped rather than fatal; only an unusable marking map
or an unwritable events file fails the run.
