# Skill Suite Judging

Reusable building blocks for Skill.Suite judgement containers: the `events.jsonl` producer, the xUnit
harness competitors' graded suites are written against, the post-processing marker, and the layered
Docker images session images are built from.

This subtree targets **net9.0** — competitors work on .NET 9 and the judge images are
`mcr.microsoft.com/dotnet/sdk:9.0`. The Skill.Suite platform stays on .NET 10; the only coupling
between them is the Docker contract and the event protocol.

## Layout

| Path | What |
|---|---|
| `Skill.Suite.TestLog/` | The event producer. No test-framework dependency — usable from a plain console app. |
| `Skill.Suite.TestLog.Xunit/` | `LoggedTest`, `FixtureScope`, `TestLog` assert wrappers, `CallLoggingProxy`, `ServiceResolver`. |
| `Skill.Suite.Marker/` | `skill-marker` dotnet tool: coverage/mutation scoring and the CIS aspect report. |
| `Skill.Suite.StarterKit/` | `skill-starter` dotnet tool: derives the competitor starter kit — stubs a white-box implementation, empties a black-box suite. |
| `docker/judge-base/` | Base image + `judge-lib.sh` shell contract every session image builds on. |
| `templates/session/` | **`dotnet new` template for a whole session module.** Start a new session here. |
| `docker/judge-smoke/` | Fixed-output image that proves the platform wiring with zero session dependencies. |
| `tools/`, `verification/` | Local judge runner and the persona matrix. |
| `samples/console-quickstart/` | The smallest thing that emits an event stream: `TestLogger` from a plain console app. |
| `samples/fibonacci-session/` | **A complete worked white-box session.** Start here when authoring one. |
| `samples/checkout-rules-session/` | **A complete worked black-box session**, where the competitor writes the tests. |

`samples/fibonacci-session/` is the reference answer to "what do I actually have to write?" — contracts
package, hidden `[Aspect]` suite, answer-key firewall, marking map, two example submissions, and the
commands to take it from `dotnet pack` to a judged run visible in the UI.

## Starting a new session

A session module lives in **its own private repository** — it holds the answer key and stays secret until the
competition ends. The authoring workflow travels there as a Claude Code plugin rather than as documentation
nobody reads: see [`../plugins/README.md`](../plugins/README.md) for the two commands that install it.

```bash
cd judging && dotnet new install ./templates/session
```
```bash
dotnet new skillsuite-session -n GreenWave.Control --judge-base skill-suite-judge-base:9.0
```

That generates the whole module — solution, contracts, reference implementation, hidden suite, competitor
starter kit, marking map, and **self-contained white-box and black-box Dockerfiles** — with every name derived
from the one you passed. Then `chmod +x *.sh`, `./pack-contracts.sh`, `./build-image.sh`.

The generated Dockerfiles declare the judge contract as `ENV` rather than taking build args, so
`./build-image.sh` needs no session-specific arguments and there is nothing for a caller to get wrong. Only
`BASE_IMAGE`, `TAG` and `REGISTRY` remain settable, because those are about the environment rather than the
session.

`templates/session/` is the single source of truth for what a session module looks like. The two modules that
predate it — `verification/fixtures/judge-fixture` and `samples/fibonacci-session` — were regenerated from it
and now own their Dockerfiles outright, so `build-images.sh` no longer renders anything: it locates a module
and runs that module's `build-image.sh`. The `__TOKEN__` templates and `tools/render-template.sh` they used to
need are gone.

## Commands

`global.json` is resolved from the **current directory**, not the project — so always run from
`judging/`, or the repo's .NET 10 SDK is used instead of the pinned 9.0.x.

```bash
cd judging && dotnet build -c Release
```
```bash
cd judging && dotnet test -c Release
```
```bash
cd judging && dotnet pack Skill.Suite.TestLog -c Release -o artifacts/nuget
```

## The event protocol is a shared contract, not a convention

`Skill.Suite.TestLog/Protocol/` holds one definition of the wire format: a record per event kind, an outcome
enum, and a reader and writer for them.

**Nothing about the wire is declared twice.**

- *Event kinds* come from the `[JsonDerivedType]` list on `TestLogEvent`. That list is the whole registry: the
  serializer writes the discriminator from it, `TestLogEventTypes` projects it for the reader, and adding a kind
  is one line in one place.
- *Field names* come from the camelCase policy on `EventJson.Options`, so `TestsRun` is `testsRun` and the
  record declaration is the only place the field exists. The reader derives its probes from the same policy over
  `nameof`, so renaming a property carries to the reader through the compiler.

**Those sources are compiled into the platform too**, via a `Compile Include` glob in
`Skill.Suite.Application.csproj`, so the producer and the consumer cannot disagree about a name. They were
previously independent string literals on each side, which meant a rename compiled cleanly on both and silently
produced runs with missing results.

Three things about it are deliberate and easy to undo by accident:

- **Shared source, not a package.** `judging/` is excluded from the platform's Docker build context for secret
  hygiene, so a `ProjectReference` would fail restore inside the image. `.dockerignore` carries a narrow
  exception for `Protocol/` alone — widen it and hidden test suites start reaching a published image; remove it
  and the image build fails with a confusing `CS0246`, because a glob matching nothing is not an error.
- **The reader is hand-written.** `JsonSerializer.Deserialize<T>` would throw on a competitor writing
  `"durationMs":"fast"` and lose the whole event, including the verdict. Field-level tolerance is the point.
- **These files must compile as C# 13.** The judging solution builds them under the SDK 9 pin even though the
  platform compiles the same files as C# 14.

Metric events share one vocabulary — `value`, `total`, `covered` — with the event kind fixing what each means.
`value` is always the headline ratio: a pass rate, a coverage ratio, a kill rate, or a composite quality. Only
`ScoreEvent.Value` is functionally consumed; it becomes the fixture's quality and drives the competitor's score
bucket.

A metric event is scoped either to a **part** — a scoring unit the marking map declares, plus the reserved
`overall` rollup — or, for `coverage` and `mutation` only, to a **fixture**: one of the competitor's own test
classes, measured on its own. The two never appear on the same event, and the platform keeps them in separate
rows, because a part and a test class can share a name and mean entirely different things. Only parts are
scored; a fixture-scoped event is a measurement shown beside the class, never a mark.

## The events.jsonl write-ordering contract

Three different writers touch the same file during a black-box run, and only one of them truncates.
Getting this wrong silently destroys a whole test run, so it is stated once, here:

| Order | Writer | Mode |
|---|---|---|
| 1 | `judge-lib.sh` (shell) | **staged to a side file** — never writes `events.jsonl` directly |
| 2 | `TestLogger` (the test host) | **truncates** on first write, then appends within the process |
| 3 | `skill-marker` | **appends**, runs last |

Consequences that are easy to get wrong:

- Anything the shell wrote to `events.jsonl` *before* `dotnet test` would be destroyed by the test
  host's truncating open. That is why shell events stage and are merged afterwards.
- Stryker re-runs the suite once per mutant, and every one of those test hosts inherits
  `LOG_DIRECTORY` and truncates. Unsetting the variable proved insufficient — the hosts still found the real
  file, and running concurrently produced one interleaved from several truncating writers: the right length,
  full of NUL bytes, every real event gone. The entrypoint **redirects** it to a throwaway directory instead.
- The platform reads the file *after* the container exits and prefers it whenever it **exists**, so an
  existing-but-empty `events.jsonl` yields zero results *and* suppresses the stdout fallback. Never
  leave the file empty.

## Conventions, and where they deviate from the repo's CLAUDE.md

The repo convention is namespace-first, then imports. **`Skill.Suite.TestLog.Xunit` must put `using`
directives above the file-scoped namespace.** Inside `namespace Skill.Suite.TestLog.Xunit`, a
`using Xunit.Sdk;` resolves `Xunit` against the enclosing namespace `Skill.Suite.TestLog`, finds the
member namespace `Xunit`, fails to find `Sdk`, and errors CS0234. Implicit usings
(`<Using Include="Xunit" />`) are unaffected — the SDK emits `global using global::Xunit;`.

Related trap, same cause: **the type name `TestLog` is shadowed inside any `Skill.Suite.*` namespace**,
where the simple name binds to the namespace `Skill.Suite.TestLog` instead of the harness type. Consumers
normally never name the type (they use the inherited `Log` property); code that must, aliases it —
`using HarnessTestLog = Skill.Suite.TestLog.Xunit.TestLog;`. Competitor suites live under their own
namespace roots and never see this.

Ported files (`TestLogger`, the harness, the Marker) are **verbatim ports** of proven code. Their
quirks are load-bearing and pinned by golden-file tests:

- **Property order is not controlled and nothing may depend on it.** Polymorphic serialization guarantees the
  `event` discriminator is written first; everything else, `timestamp` included, falls where System.Text.Json
  puts it, which is derived members before base ones. The reader is name-based, so this is cosmetic.
- The `assertion` event is emitted *after* the xUnit assert runs, not before — `passed` cannot be
  known any earlier.
- `Math.Round(x, 4)` in the Marker is banker's rounding. Keep it; choose test fixtures away from
  midpoints.

## Projects deliberately outside `Skill.Suite.Judging.sln`

- `samples/fibonacci-session/` and `verification/fixtures/judge-fixture/` — session modules. Each restores
  its Contracts package from its own `local-nuget/`, which does not exist until `pack-contracts.sh` has run,
  so a solution-wide restore would fail `NU1101` on a clean checkout. They also carry empty
  `Directory.Build.props`/`.targets` on purpose, to sever the walk-up into this subtree: a real session module
  is its own repository with nothing above it, and inheriting these properties would make them build under
  conditions no session ever sees.
- `Skill.Suite.TestLog.Conformance.Tests` — targets net10.0 so it can reference the platform's real
  `TestLogParser`; unbuildable under this subtree's SDK 9 pin. It lives in `Skill.Suite.sln` instead.

Do not "fix" any of them by adding it to the solution.
