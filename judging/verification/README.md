# Verification kit

The local acceptance gate for judge images. It answers one question — *does this image report every failure
mode distinguishably?* — without needing the platform, Postgres or Gitea.

That question matters because the platform sees almost nothing: an exit code, and whatever landed in
`events.jsonl`. If a broken submission and a working one produce the same pair, nobody finds out until an
expert is explaining a wrong mark to a competitor.

## Run it

```bash
judging/verification/verify.sh fixture
```
```bash
judging/verification/verify.sh fixture broken-code
```

One image, one persona directory each, results diffed against `expected/<session>.json`. On failure the
container's logs and event stream are kept and the path is printed.

To run a single persona by hand, mirroring the platform's exact invocation:

```bash
judging/tools/judge-run.sh judge-fixture:1 judging/verification/personas/fixture/red
```

## The fixture module

`fixtures/judge-fixture/` is a complete three-project session module in miniature: Contracts (packed to
`local-nuget/`), a reference Services implementation, and a hidden xUnit suite using the real harness with
`[Aspect]` annotations. It exists so `judge-base` and the white-box template can be exercised end to end
against something with no session secrets in it.

```bash
cd judging/verification/fixtures/judge-fixture && ./pack-contracts.sh && ./build-image.sh
```

Add `blackbox` for the other image. No build arguments: the module's Dockerfiles declare the judge contract as
`ENV`, and its `build-image.sh` pins the image names `judge-fixture` / `judge-fixture-blackbox` that
`expected/*.json` refers to.

It was regenerated from `judging/templates/session`, so it is exactly what `dotnet new skillsuite-session`
produces — which makes the persona matrix a test of the template as much as of the images.

It carries its own empty `Directory.Build.props` / `.targets` / `Directory.Packages.props`. Those are load
bearing: MSBuild walks upward and stops at the first one it finds, so they sever the module from the
`judging/` subtree's properties and central package management. A real session module is its own repository
with nothing above it, and a fixture that built under conditions no session ever sees would not be a
rehearsal of anything.

## The persona matrix

| Persona | What it is | Exit | Platform status |
|---|---|---|---|
| `happy` | Reference implementation | 0 | Completed, 5/5 |
| `red` | Compiles, two aspects wrong | 0 | Completed, 3/5 — red tests are a result, not a failed run |
| `broken-code` | Syntax error | 6 | Failed, with a diagnostic naming the compile error |
| `missing-folder` | No `*.Services` folder | 3 | Failed |
| `adds-package` | References a package outside the offline feed | 4 | Failed |
| `slow` | Endless loop | 5 | Failed, cut off by the image's wall clock |

A persona is just a directory shaped like a competitor checkout — the swapped folder and nothing else.
Adding one means creating the directory and a row in `expected/<session>.json`.

Two subtleties this matrix already caught, both worth keeping in mind when adding personas:

- **`red` must exit 0.** A white-box session needs partial credit, so failing tests cannot fail the run. That
  is exactly why `broken-code` needs its own exit code: `dotnet test` returns the same status for a compile
  error as for a failing test, so without a separate build step the two are indistinguishable and a
  non-compiling submission is recorded as a clean pass.
- **`adds-package` must pick a package that is in neither `local-nuget` nor the warmed NuGet cache.** The
  cache holds everything the image build pulled transitively, so `Newtonsoft.Json` (via the test SDK)
  restores quite legitimately. It uses `Dapper` for that reason.

## The black-box matrix

The same fixture module also builds a black-box image, so both templates are exercised against identical
code. There the reference implementation is baked in and the *test suite* is swapped, so nothing is checked
against fixed expectations — the suite is measured, and the expectations are quality bands.

```bash
judging/verification/verify.sh fixture-blackbox
```

| Persona | Coverage | Kill rate | Quality |
|---|---|---|---|
| `reference` | 1.00 | 0.83 | ~0.97 |
| `weak` | **1.00** | 0.33 | ~0.61 |
| `empty` | 0 | 0 | 0 |

`weak` is the row that justifies mutation testing existing at all. It calls every method but asserts nothing
meaningful, so its **line coverage is identical to the reference suite's** — against coverage alone the two
are indistinguishable. Only the mutation score separates "ran the code" from "checked the code". If `weak`
ever scores close to `reference`, the mutation ramp in `marking-map.json` has stopped doing any work.

Bands rather than exact values, because mutation testing has some run-to-run variance and a brittle equality
assertion would train everyone to ignore this suite.

This matrix runs Stryker three times, so it is far slower than the white-box one.

## Two bugs this kit caught that nothing else would

Worth recording, because both were invisible to unit tests and to a casual `docker run`:

**Every step failure was silently swallowed.** `judge-lib.sh` captured a step's status with
`if ! wait "$child"; then status=$?`, where `$?` is the status of the *negation* — always 0. A submission that
did not compile sailed through the build step and was reported as a successful run. Only asserting the exit
code per persona surfaced it.

**The marker destroyed the test events it was appending to.** The judge sets `LOG_DIRECTORY`, and the marker
referenced `TestLogger.EventJsonOptions` for its serializer settings. Merely touching that type runs
`TestLogger`'s static initializer, which opens `$LOG_DIRECTORY/events.jsonl` with `FileMode.Create` — so the
whole test run was truncated before the marker wrote a byte, leaving a file of exactly the right length full
of NUL bytes. The wire settings now live in `EventJson`, a type with no side effects, and
`Skill.Suite.Marker.Tests/ToolProcessTests.cs` runs the real tool as a process with `LOG_DIRECTORY` set to
keep it that way.

## Adding a session

1. Build the session image from the white-box or black-box template.
2. Create `personas/<session>/<name>/` directories containing just the swapped folder.
3. Write `expected/<session>.json` with the image reference and a row per persona.
4. `verify.sh <session>`.

Per-persona `env` in the expectations file is passed through to `docker run` — `slow` uses it to shorten
`JUDGE_TIMEOUT_SECONDS` so the matrix does not sit for the full default budget.
