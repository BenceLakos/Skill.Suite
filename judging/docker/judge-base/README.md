# judge-base

The shared foundation every Skill.Suite judgement image builds on. It contributes the SDK, the shell library
that implements the platform's runtime contract, and a default white-box pipeline.

```bash
docker build -t ghcr.io/bencelakos/skill-suite-judge-base:9.0 judging/docker/judge-base
docker run --rm --entrypoint /usr/local/bin/judge-selftest.sh ghcr.io/bencelakos/skill-suite-judge-base:9.0
```

Publish both a moving `:9.0` tag for `FROM` and an immutable `:9.0-<release>` tag for session images to pin.
`docker run` is invoked with no `--pull`, so a re-pushed mutable tag would keep judging with whatever stale
image the host already has.

## Environment contract

| Variable | Default | Meaning |
|---|---|---|
| `COMPETITOR_DIRECTORY` | `/workspace` | The competitor's checkout. Read-only; injected by the platform. |
| `LOG_DIRECTORY` | `/var/log/skill-suite` | Writable. `events.jsonl` goes here. Injected by the platform. |
| `JUDGE_APP_DIR` | `/app` | Where the baked solution lives. |
| `JUDGE_SWAP_SRC` | — **required** | Folder inside the submission to graft in, e.g. `CivicCharge.Billing.Services`. |
| `JUDGE_SWAP_DEST` | `$JUDGE_APP_DIR/$JUDGE_SWAP_SRC` | The placeholder it replaces. |
| `JUDGE_SOLUTION` | — **required** | Solution file, for the offline restore. |
| `JUDGE_TEST_PROJECT` | — **required** | Test csproj to build and run. |
| `JUDGE_TIMEOUT_SECONDS` | `300` | Wall clock per step. |
| `JUDGE_MUTATION_TIMEOUT_SECONDS` | `900` | Wall clock for mutation testing, which re-runs the suite per mutant. |
| `JUDGE_FAIL_ON_RED` | `false` | Whether failing tests make the run fail. Keep `false` for partial credit. |

The platform injects only the first two. Everything else is baked into the session image as `ENV`, which is
what lets one entrypoint serve every session.

## Exit codes

| Code | Meaning | Platform status |
|---|---|---|
| 0 | Tests ran (they may have failed) | Completed |
| 2 | Image misconfigured — a required variable is unset | Failed |
| 3 | The submission's swap folder is missing or empty | Failed |
| 4 | Offline restore failed — usually an added package reference | Failed |
| 5 | A step exceeded its wall clock | Failed |
| 6 | The submission does not compile | Failed |

Every non-zero path emits a `marker-error` event first, so the UI can say *why* rather than showing a bare
red status.

## Pipeline

`swap_dir` → `restore_offline` → `build_tests` → `run_tests` → `judge_assert_results`

Four design decisions in there are not obvious:

**Build is a separate step from test.** `dotnet test` returns 1 for *both* red tests and a compile error.
With `JUDGE_FAIL_ON_RED=false` — which white-box sessions need — the two become indistinguishable, so a
submission that does not compile exits 0 and is recorded as Completed with no results. Splitting the build
out is the only way a non-compiling submission is reported as failed.

**`swap_dir` validates before it deletes**, and copies sources only. Excluded deliberately: `bin/` and
`obj/` (stale output, and a route for smuggling in prebuilt assemblies), `nuget.config` (would re-open
nuget.org and defeat the offline restore entirely), `Directory.Build.*` (can switch off the nullability
contract the harness relies on), and `global.json` (can pin an SDK the image does not have).

**Long-running steps run in the background and are awaited with `wait`.** Bash defers trap handlers until
the current *foreground* command returns, and supersede is `docker stop --time 5` — so with a foreground
child the five-second grace would expire before the SIGTERM trap ever ran.

**The warmed NuGet cache is part of the offline boundary — and a hole in it.** `restore_offline` allows
`local-nuget` plus `/root/.nuget/packages`, and that cache contains everything the image build pulled
*transitively*. `Newtonsoft.Json` ends up there via the test SDK, for example, so a submission can reference
it and restore quite legitimately. Anything a session must actually block has to be absent from both. Check
what a built image exposes before trusting the boundary:

```bash
docker run --rm --entrypoint sh <image> -c 'ls /root/.nuget/packages'
```

**Events are staged, not written directly.** The xUnit harness opens `events.jsonl` truncating when the test
host starts, so anything the shell wrote first would be destroyed. `flush_events` merges the stage after the
test host is gone — and never creates an *empty* `events.jsonl`, because the platform prefers the file
whenever it exists and an empty one reports zero results while also suppressing the stdout fallback.

## What the shell library gives you

`emit_event`, `emit_marker_error`, `flush_events`, `judge_fatal <code> <stage> <detail>`, `judge_require`,
`swap_dir`, `restore_offline`, `build_tests`, `run_tests`, `judge_assert_results`, `judge_run_step`,
`json_escape`.

`json_escape` is hand-rolled parameter expansion because the SDK image ships neither `jq` nor `python3`, and
a raw MSBuild error full of quotes and newlines has to survive into a JSON string field. It truncates before
escaping, so a cut can never land inside an escape sequence.

`judge_fatal` takes the exit code and the stage; the spec's single-argument `emit_fatal` is kept as a shim,
but it cannot express the exit codes the contract requires.

## Self-test

The shell layer carries real logic that no xUnit test can reach, so it has its own checks — JSON escaping
including a realistic compiler error, event staging, the never-empty rule, appending after a truncated final
line, and every `swap_dir` exclusion. Run it in CI right after the image builds:

```bash
docker run --rm --entrypoint /usr/local/bin/judge-selftest.sh ghcr.io/bencelakos/skill-suite-judge-base:9.0
```
