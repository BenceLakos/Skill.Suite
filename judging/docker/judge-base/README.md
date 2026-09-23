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
| `JUDGE_CALL_TIMEOUT_SECONDS` | `10` | Budget for one call into the submission, enforced *inside* the test host. `0` disables it. |
| `JUDGE_FAIL_ON_RED` | `false` | Whether failing tests make the run fail. Keep `false` for partial credit. |

The platform injects only the first two. Everything else is baked into the session image as `ENV`, which is
what lets one entrypoint serve every session.

`JUDGE_CALL_TIMEOUT_SECONDS` is the one variable the shell does not act on itself: `judge_configure` defaults
and **exports** it, and the xUnit harness reads it per call. Fractional values are accepted, an unparseable
one falls back to the default, and `0` or less runs every call unguarded — which is what a persona that exists
to prove the suite wall clock still fires needs. `setpriv` is invoked without `--reset-env`, so the test step
inherits it along with `LOG_DIRECTORY`; adding that flag would silently switch the guard off.

## Exit codes

| Code | Meaning | Platform status |
|---|---|---|
| 0 | Tests ran (they may have failed) | Completed |
| 2 | Image misconfigured — a required variable is unset | Failed |
| 3 | The submission's swap folder is missing or empty | Failed |
| 4 | Offline restore failed — usually an added package reference | Failed |
| 5 | A step exceeded its wall clock (`timeout` reported 124 *or* 137 — see below) | Failed |
| 6 | The submission does not compile | Failed |

Every non-zero path emits a `marker-error` event first, so the UI can say *why* rather than showing a bare
red status.

## Pipeline

`swap_dir` → `restore_offline` → `build_tests` → `run_tests` → `judge_assert_results`

Several design decisions in there are not obvious:

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

**A step that hit its wall clock does not necessarily report 124.** `timeout` returns 124 only when the
child died from the SIGTERM it sent. `run_tests` hands the test step to an unprivileged account, and root
can only signal across that uid boundary while it holds CAP_KILL — which the platform's cap list did not
grant. `--cap-drop ALL` added back SETUID, SETGID, CHOWN, DAC_OVERRIDE and FOWNER, so root's own `timeout`
got `EPERM`; after `--kill-after` it SIGKILLed its process group, of which it is a member, and the only
process it succeeded in killing was itself. `wait` reported **137** and the test host ran on until the
container was torn down.

This was not theoretical: while the check was `== 124`, no white-box submission on the real platform could
be cut off. An endless loop exited 0, was recorded as **Completed** with the partial results collected
before the hang, and carried a `no TRX file was produced … treat this run's results as unverified`
diagnostic — which is true, and points at the harness rather than at the loop.

`DockerRunArguments.Build` now adds **KILL** to the list, so the SIGTERM reaches the test step and the
ordinary status is 124 again. The competitor's own code never holds the capability — it runs after the
privilege drop, under `no-new-privileges` — and CAP_KILL reaches no further than the container's own pid
namespace. Ask `judge_step_timed_out <status>` all the same rather than comparing to 124 by hand: a child
can outlive a SIGTERM for other reasons, and the helper also refuses to call a 137 that arrived well inside
the budget a timeout, because a test host the OOM killer took reports the same status. The `slow-hardened`
persona in `judging/verification/` runs the matrix under the platform's real flag set; `judge-selftest.sh`
covers the 137 classification directly.

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
`judge_step_timed_out <status>`, `judge_record_step_clock <budget> <started>`, `json_escape`.

`json_escape` is hand-rolled parameter expansion because the SDK image ships neither `jq` nor `python3`, and
a raw MSBuild error full of quotes and newlines has to survive into a JSON string field. It truncates before
escaping, so a cut can never land inside an escape sequence.

`judge_fatal` takes the exit code and the stage; the spec's single-argument `emit_fatal` is kept as a shim,
but it cannot express the exit codes the contract requires.

## Self-test

The shell layer carries real logic that no xUnit test can reach, so it has its own checks — JSON escaping
including a realistic compiler error, event staging, the never-empty rule, appending after a truncated final
line, every `swap_dir` exclusion, and the wall-clock classification (a step that dies on the SIGTERM reports
124, one that outlives it reports 137, and a 137 well inside the budget is not a timeout). Run it in CI
right after the image builds:

```bash
docker run --rm --entrypoint /usr/local/bin/judge-selftest.sh ghcr.io/bencelakos/skill-suite-judge-base:9.0
```
