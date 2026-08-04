---
name: authoring-judging-sessions
description: How to author, calibrate, package and publish a Skill Suite judging session — the contracts, reference implementation, hidden graded test suite, competitor starter kit, and the white-box or black-box judge Docker images. Use this skill whenever the user is working on a competition session, judging module, judge image, marking map, competitor starter package, or anything under a *.Contracts / *.Services / *.UnitTests trio in a Skill Suite session repository — and also when they mention skill-starter, skill-marker, judge-base, Stryker mutation scoring, [Aspect] attributes, competitor repositories, or "the test project" for a competition. Reach for it even if the user only says something like "add a new task to the session", "why did this competitor score so low", or "get the image built and pushed", because those all depend on the calibration and answer-key rules documented here.
---

# Authoring a Skill Suite judging session

A **session** is one competition task. It lives in its own repository, separate from the open-source
Skill.Suite platform, because it contains the answer key and must stay secret until the competition ends.

Everything a session needs is generated from one template. Your job is to fill in the domain, calibrate it,
and publish it — in that order, because calibration is what stops a session from marking every competitor
down for the author's own bug.

## First decide which type it is

This is the one decision that changes everything downstream. Get it right before generating anything.

| | **white-box** (S1, S3) | **black-box** (S4) |
|---|---|---|
| the competitor writes | the implementation | the test suite |
| swapped folder at run time | `*.Services` | `*.UnitTests` |
| baked into the image | the hidden graded suite | the reference implementation, **as source** |
| how marks are decided | aspects passed | line coverage × mutation kill rate |
| starter kit handed out | stubbed implementation | wired-but-empty test project |

Both images come out of the same module, so a session can be either — but the *authoring* differs, and so
does what leaks if you get the firewall wrong.

## 1. Create the module

```bash
dotnet new install <path-to-skill-suite>/judging/templates/session
```

```bash
dotnet new skillsuite-session -n GreenWave.Control
```

Every name derives from the one you pass: projects, namespaces, solution, the `ENV` in both Dockerfiles, and
the image names. Add `--judge-base skill-suite-judge-base:9.0` when working against a locally built base
image instead of the published one.

Then, once:

```bash
chmod +x pack-contracts.sh build-image.sh make-competitor-start.sh docker-entrypoint.sh
```

The template cannot set the execute bit itself — a `dotnet new` post-action that could makes the CLI prompt
for `--allow-scripts` and hang outright in CI.

```bash
JUDGING_FEED=<nuget-source> ./pack-contracts.sh
```

This fills `local-nuget/` with your Contracts package plus the three judging packages. **Required before any
build** — the judge images restore offline and fail `NU1101` without it. Inside a checkout of the Skill.Suite
repo, `JUDGING_ROOT` is discovered automatically and the packages are built from source instead; in a separate
session repository you need `JUDGING_FEED`.

## 2. Write the contract first

`*.Contracts/` holds the interface. In a black-box session it is the **entire specification** — the competitor
never sees the implementation and writes tests from these XML docs alone, so an ambiguous rule is not a
stylistic problem, it is a rule they cannot write a passing test for.

State every boundary as a number. `5% from 10 units, 12% from 50, the higher tier wins, never combined` is
testable; "bulk discounts apply" is not.

Two house rules the harness enforces, so document them on the interface:

- **Never throws.** Invalid input returns a documented sentinel. The call-logging proxy turns an escaped
  exception into an errored test, so a throw fails the aspect exactly as a wrong answer does.
- **Nullability is declared honestly.** The proxy reads it off the interface and fails a call that returns null
  from a non-nullable member. That is what stops a competitor stubbing everything to `null` and collecting
  coverage.

Keep `<Nullable>enable</Nullable>` in the Contracts csproj. Without it that check silently disables itself.

## 3. Write the reference implementation

`*.Services/` is the answer key. Correct, straightforward, and matching the contract's boundaries *exactly* —
in a black-box session Stryker mutates this code to generate the faults competitors are scored on catching, so
a mismatch between the documented rule and the implemented one poisons the mutation baseline.

## 4. Write the hidden suite

`*.UnitTests/` is graded in a white-box session and used only for calibration in a black-box one. Either way
it is an answer key.

Three rules, all enforced by the harness rather than by review:

- Derive from `LoggedTest<TSelf>` and take `IClassFixture<FixtureScope<TSelf>>`. Without the fixture there is
  no start/finish-fixture pair and **the platform records nothing at all** for the run.
- Assert through `Log`, never bare `Assert`. A bare assertion still fails the test but emits no event, so the
  result reaches the UI as a red row with nothing explaining it.
- Resolve with `ServiceResolver.Resolve<T>().WithCallLogging()`. The proxy records every call with its
  arguments and return value, which is what makes a disputed mark reviewable after the fact.

**One test case is one aspect, and an aspect counts as satisfied only when every test claiming it passes.** A
`[Theory]` with five rows is therefore all-or-nothing — split it when the rows should score separately.
`[Aspect("A1.1", CompetitorVisible = true)]` lets an aspect move the coarse bar the competitor sees; leave the
flag off to grade without revealing anything.

## 5. Calibrate — do not skip this

```bash
dotnet test <Session>.sln -c Release
```

The hidden suite must be **100% green against the reference implementation**. A session whose own answer key
does not pass is not ready: every competitor loses the same marks for your bug, and you will not find out until
someone disputes a result.

For a **black-box** session there is a second calibration, and it needs measurement rather than judgement.
Write two throwaway submissions — one thorough, one that calls every method and asserts nothing meaningful —
run both, and tune `marking-map.json` until they are far apart. See
[references/calibration.md](references/calibration.md); getting this wrong is the most common way a black-box
session ends up unable to distinguish real work from coverage theatre.

## 6. Generate the competitor starter kit

Never hand-write this. It is derived, so it cannot drift out of step with the contract:

```bash
./make-competitor-start.sh            # white-box: strips the implementation
./make-competitor-start.sh blackbox   # black-box: empties the reference suite
```

The two modes are inverses because the two session types are. White-box keeps every public member's exact
declaration with a `NotImplementedException` body and removes non-public members and non-const fields — a
private helper's name gives away the decomposition, and a lookup table is the answer in data form. Black-box
removes every method (the tests *are* the answer) while keeping the harness wiring: the private service field,
the fixture constructor, the base list. Strip those and the delivered project cannot resolve the service under
test.

The script recreates the output directory rather than merging, so a stub for a member you have since deleted
cannot survive into what competitors receive — and it compiles the result before finishing, because a starter
kit that does not build costs every competitor the same confused ten minutes.

Re-run it whenever the contract or the reference changes.

## 7. Build and verify the images

```bash
./build-image.sh            # white-box
./build-image.sh blackbox
```

No session arguments: the solution, the swapped folder and the test project are `ENV` in the module's own
Dockerfiles, because they are facts about the module rather than choices a caller makes. Only environment
values are settable — `BASE_IMAGE`, `TAG`, `IMAGE_NAME`, `REGISTRY`, `NO_CACHE`.

Then run a submission the way the platform will, before trusting anything:

```bash
<path-to-skill-suite>/judging/tools/judge-run.sh <image> ./some-submission
```

A submission is a directory containing **only the swapped folder**. This mirrors the platform's invocation
exactly — read-only mount at `/workspace`, `-w /workspace`, two env vars — and prints the exit code and the
resulting events.

In a white-box session, **red tests are a result, not a failed run**: expect exit 0 with failures recorded.
Partial credit depends on that. A black-box run takes minutes rather than seconds, because Stryker re-runs the
whole suite once per mutant.

## 8. Publish

Session artifacts and generic artifacts go to different places for a reason. See
[references/publishing.md](references/publishing.md) for the commands, tags and secrets.

The short version: **session images go only to the private registry, never to public CI.** A white-box image
contains the hidden suite; a black-box image contains the reference implementation in source form. Tag them
immutably — the platform runs `docker run` with no `--pull`, so a re-pushed `:latest` leaves it judging with a
stale local image.

## Traps worth knowing before they bite

**The answer-key firewall is not decoration.** Both Dockerfiles fail the build if the wrong sources reach the
image, and `build-image.sh` re-checks the *final* white-box image. If a check fires, fix the `.dockerignore` —
do not loosen it. A competitor can `docker pull` any image they are told to run, and the mistake is not
recoverable once pushed.

**`.dockerignore` patterns are root-anchored.** `Foo.UnitTests/**` does not cover
`competitor-start/Foo.UnitTests/`. Check what actually landed:
`docker run --rm --entrypoint sh <image> -c 'find /app -name "*.cs"'`.

**Stryker rejects unknown config keys** and fails the whole run, so `stryker-config.json` carries no
explanatory comments. The reasoning lives in the module README instead.

**`events.jsonl` has one truncating writer.** The test host opens it with `FileMode.Create`; the shell stages
its events to a side file and merges afterwards; the marker appends and runs last. An existing-but-empty
`events.jsonl` yields zero results *and* suppresses the stdout fallback, so it must end up absent or non-empty.

**The judging subtree is net9.0 and pinned.** Competitors work on .NET 9 and the judge images are `sdk:9.0`.
Run `dotnet` from the module directory or from `judging/`, or you get the wrong SDK.
