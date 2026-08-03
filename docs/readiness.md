# Competition readiness

Tracking what stands between the current state and running a real WorldSkills session. Derived from a
four-lens code audit (59 findings); every item below was verified against the code, not inferred.

**Verdict: not ready.** The three guarantees a marking platform must make are all currently false — a mark can
be silently lost, a mark can be forged with ordinary C#, and the shipped deployment hands administrator access
to anyone on the venue LAN. The shape is right and most loss paths are small, localised fixes; judge-container
isolation is the expensive one. Estimate two to three focused weeks plus a dress rehearsal.

## Done

- **Group 0 — baseline.** All work committed in reviewable slices on `feature/judging-infrastructure`.
  Verified from a *fresh clone*: both solutions build with 0 warnings under `-warnaserror`, 84 platform tests
  and 244 judging tests pass.
- **`judging/.gitignore` excluded the acceptance gate.** `verification/fixtures/*/Dockerfile` and
  `.dockerignore` were ignored — a rule from when they were rendered from `docker/templates/`, which no longer
  exists. A fresh clone could not build `judge-fixture:1`, so `verify.sh fixture` was dead on arrival. Rule
  removed; all seven files the gate needs confirmed present in a clone.
- **B3 — a run was lost when a competitor pushed twice in a row.** `cts.Cancel()` runs callbacks inline, and
  the executing run's callback does `docker stop --time 5`, so up to ten seconds of container shutdown sat on
  the webhook request thread. Gitea's delivery timed out, and the new run — saved under the request's token —
  was never written: old run Cancelled, new run absent, competitor's latest work with no result. Now
  `CancelAsync`, the run is persisted *before* superseding, and all writes use `CancellationToken.None`.
  Supersede excludes the new run so it cannot cancel itself. Exercised via `E2E_SUPERSEDE=1`.
- **B4 (part) — reprocessing a log left a red verdict over correct marks.** `ReprocessTestRunLogHandler`
  rebuilt fixtures but never restored `Status`/`FailureReason`, so the only recovery path produced a run marked
  Failed with "produced no test results" sitting on top of the marks it had just recovered — invisible to every
  query filtering on Status. Now promotes to Completed when fixtures were recovered *and* the recorded failure
  was the absence of results; a compile failure, cancellation or timeout keeps its reason. `MarkCompleted`
  clears `FailureReason`, which it never did.

## Blockers remaining

| | what | why it blocks | size |
|---|---|---|---|
| B1 | No durability, no operator recovery: in-memory `Channel` queue, `Pending` never re-scanned, no re-run/cancel, no `restart:` policy | a redeploy or crash freezes accepted submissions forever with no button to fix them | M |
| B2 | A submission is judged only if its single webhook delivery succeeds; nothing reconciles an unjudged HEAD | one failed delivery loses a mark silently and irrecoverably | M |
| B4 | Remainder: marker's `EventSink.Flush()` truncate-then-rewrite can destroy the event stream on kill/ENOSPC; the stdout fallback can never fire because `LOG_DIRECTORY` is always set | the last line of defence for a lost save | S |
| B5 | Competitor code runs as **root** with `/app` writable; their `.csproj` is built before the hidden suite. Verified: no `USER`, no `--read-only`, `--cap-drop` or `--user` | one MSBuild `Exec` target yields an undetectable clean pass | M |
| B6 | The mark is whatever the container says: the logger shares a process with competitor code, and black-box sums every cobertura under a writable dir | forging a top mark needs ordinary C#, not an exploit | L |
| B7 | `StartSession` rotates the secret before installing the hook, and saves enrolments last — so the documented repair action 400s everyone's pushes, and a mid-way failure leaves Active-with-zero-enrolments | the only operator repair is itself a mark-loss event | M |
| B8 | The edit form can set `Status=Active` with no guard; a session with no secret accepts unsigned pushes | an anonymous judging trigger, and sessions that provision nothing | S |
| B9 | Shipped compose is `Development` with a published admin password, `RequiredLength=1`, no lockout; Production does not boot (no admin, no `GitInternalBaseUrl`, no starter-packages mount, no container limits) | any competitor logs in as administrator | S |
| B10 | No backup or restore for Postgres or the DataProtection key ring | one volume loss loses the competition, with no procedure | M |
| B11 | Container memory/cpu/pids limits are null by default; container output buffered unboundedly into `FailureReason` | one competitor printing to stderr kills the platform and every concurrent run | M |
| B12 | The starter kit a competitor receives has no solution, `nuget.config` or `local-nuget` — verified `dotnet build` fails `MSB1003`. The generator's own check copies it back into the full module, so it prints ok | every competitor's first action fails | M |

## Then

Export marks (`Quality` is on neither overview DTO), enforce the competition window at push time, clone the
recorded commit SHA, admin live view and queue depth, competitor bulk import, a Gitea access runbook, a
concurrency token on `TestRun`, and the authoring correctness pack (template marking-map ramps contradict its
own guide; the `include` parts key is silently ignored; pin `dotnet-stryker`).

## Sequence

0. ~~Baseline: commit, CI green on a clean clone.~~ **Done.**
1. **Stop losing marks** (B1, B2, remainder of B4). *Gate:* a scripted burst of 20 pushes with double-pushes
   and a `kill -9` mid-burst — afterwards every accepted push is terminal or requeued, nothing stuck.
2. **Make session start survivable** (B7, B8). *Gate:* inject a failure at each provisioning step; no
   Active-with-zero-enrolments state, and a push during a re-Start is still judged.
3. **A deployment you can defend** (B9, B10, B11). *Gate:* Production boots, admin credential not in git,
   and a restore drill from last night's dump reproduces every mark.
4. **Judge sandbox** (B5, B6). *Gate:* three attack personas in `judging/verification/personas` — MSBuild
   `Exec`, forged `mutation-report.json`, static-constructor event forgery — each blocked or distinguishably
   failed by `verify.sh`.
5. **Author the real session** (B12, authoring pack). Use `JUDGING_ROOT` mode; publishing is not needed for
   v1. *Gate:* on a machine with no Skill.Suite clone the competitor kit restores, builds and tests offline.
6. **Get the numbers out** (export, window, live view, commit SHA).
7. **Dress rehearsal, one full day, no shortcuts.** Real session, 5-10 fake competitors on separate machines,
   double-pushes, a post-deadline push, an app restart mid-burst, one dispute walked end to end, then Close and
   export. Non-negotiable: the provisioning path that consumes an author's template folder is currently
   exercised by nothing — `run-e2e.sh` inserts sessions with raw SQL. Treat any promised date as provisional
   until this passes.
