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

- **B8 — unsigned pushes accepted, and the edit form could set `Status=Active`.** The endpoint is anonymous by
  necessity, so the HMAC is the only authentication — and a session with no secret accepted anything, making it
  an open judging trigger for anyone who could reach the port. Now fails closed behind
  `Webhook:AllowUnsignedPushes` (default **off**), which only the local harness opts into, because `run-e2e.sh`
  inserts its session with SQL and cannot produce DataProtection ciphertext. Verified both directions live: 202
  with the opt-in, 400 `InvalidSignature` on production defaults. Status is now displayed, not editable —
  picking Active provisioned nothing and picking Closed cancelled nothing.
- **B12 — the starter kit could not be compiled by a competitor.** It shipped a `PackageReference` to the
  session's Contracts package with no `nuget.config` and no feed, so `dotnet build` failed. Worse, the
  generator's own check copied the kit *back into the full module*, so it was re-testing the module and printed
  ok. The kit now bundles a `nuget.config` and the packages it names — safe, because `swap_dir` strips
  `nuget.config` from a submission, so it helps locally and cannot affect judging — and the check builds the
  kit **alone**, the way a competitor will. Verified standalone: restores and builds offline.

- **B11 — one competitor could kill the platform.** The raw stderr copy that becomes `FailureReason` was
  unbounded and competitor code decides what goes into it, so `while(true) Console.Error.WriteLine(...)` grew it
  until the application process died — taking every concurrently judged competitor with it. Now capped at 64 KB,
  head-retained, with an explicit truncation marker. Separately the container resource caps were all null, i.e.
  uncapped: `compose.override.yaml` now sets memory/cpu/pids, and they are documented as **required** in
  production. The arg builder already honoured them and has 9 assertions covering it.

- **B7 (part) — the repair action was itself a mark-loss event.** Start rotated the webhook secret immediately
  but reinstalled the hook carrying it only at the very end, so re-pressing Start on a live session — the
  documented repair — rejected every other competitor's pushes as unsigned for the whole provisioning window.
  The secret is now **reused** when the session already has one, so installed hooks stay valid and Start is
  genuinely idempotent. Enrolment rows are also saved per competitor instead of once at the end: a throw partway
  used to leave an Active session with repositories on the git host and zero rows in the database, so every push
  was rejected as `CompetitorNotFound` while the UI showed the session as live.
  *Verified by inspection and a green build/test/E2E, not by a live re-Start* — the only session carrying a
  secret was Closed, and Closed correctly refuses to start. Re-test this on the next provisioning run.
  **B7 completed:** provisioning errors are now surfaced. Only the per-competitor step returned a `Result` —
  creating the organisation, seeding the template repository and installing the webhook all throw — so an
  exception tore down the Blazor circuit and the operator saw the page die with no message, unable to tell a
  partial Start from a clean one. Now caught and shown, with the grid reloaded so the enrolment state is
  visible. Combined with per-competitor saving, re-running Start after fixing the cause retries only what did
  not get through.

- **B9 — the shipped deployment handed admin to the venue LAN, and Production did not boot.** `compose.yaml`
  set `ASPNETCORE_ENVIRONMENT: Development`, which seeded the published `admin`/`Admin1234!` pair, with
  `RequiredLength = 1`, every character class off, and `lockoutOnFailure: false` so brute force was free.
  Now: the Identity policy is bound to configuration with a hard floor of 10 characters that configuration
  cannot lower, production defaults are 12 characters with all classes and 5-attempt lockout, and sign-in counts
  failures. `compose.production.yaml` is a real overlay — Production env, first-boot admin from `.env` (which is
  gitignored, with `.env.example` committed), `GitInternalBaseUrl`, `PublicWebhookUrl`, the starter-packages
  mount, judge memory/cpu/pids caps, `restart: unless-stopped`, a healthcheck, log rotation, and 8080 only.
  It refuses to start without the admin secret, and it must be named explicitly so `compose.override.yaml`'s
  local weakening cannot leak in.
  *Verified live:* Production booted, seeded its admin from the environment, returned 200 on the login page,
  and rejected an unsigned push with 400 `InvalidSignature`.
  **Operational gotcha found while testing:** the Development `admin` user survives in the database, because
  seeding is first-boot-only and the volume persists. A production deployment must start from a **fresh
  database**, or delete that account — otherwise the published dev credential still works.

- **B10 — no backup or restore.** One volume loss lost the competition with no procedure. Added
  `scripts/backup.sh` and `scripts/restore.sh`, covering the three things that are useless without each other:
  Postgres (the marks), the DataProtection key ring (without it every credential is unwrappable and every
  session's webhook secret undecryptable, so a database-only restore gives you a competition where no push can
  be verified), and optionally the workdir (dispute evidence). Restore refuses to run with the app up, demands
  the database name typed out, and empties the key-ring volume before extracting — a leftover key is worse than
  a missing one, because it decrypts some rows and not others.
  *Verified by a real drill:* backed up, deleted all 6 test runs, restored, and got 6 runs / 5 fixtures /
  3 enrolments back with the app booting and **zero key-ring errors**.
  Two things the drill itself found: the helper container used `alpine:3`, which is not present locally — the
  pull hung on a credential helper, so the backup script's first act was a network round trip it did not need,
  and a backup tool that requires the internet is no use on a competition machine. It now uses the Postgres
  image already running. And the workdir volume is opt-in, because nothing prunes it: archiving every clone ever
  made on a 15-minute timer is not viable.

- **B4 (remainder) — a kill mid-write destroyed the whole event stream.** The marker's `EventSink.Flush()` read
  the file, appended, then overwrote it — and between opening the destination and finishing the write the file is
  truncated. A SIGKILL from the run's wall clock, or an `ENOSPC` on a workdir that nothing prunes, lands in that
  window and takes out *every* test result the submission produced, not just the metrics being appended. The
  platform then sees an existing-but-empty `events.jsonl`, which yields zero results and also suppresses its
  stdout fallback. Now written to a sibling `.tmp` and renamed into place: a same-directory rename is atomic, so
  a reader sees either the old complete file or the new one. Flushed to disk before the rename, because a durable
  rename over non-durable data is how you get a file of the right length full of NULs — the exact symptom this
  sink already existed to prevent.

- **B1 (durability) — a restart froze accepted submissions forever.** The queue is in-process, so a redeploy,
  OOM kill or crash discarded it and the rows it pointed at sat on Pending or Running with nothing that would
  ever judge them and no operator action available. `TestRunWorker` now re-enqueues unfinished runs on startup —
  Running rows too, not just Pending, because a Running row has no container behind it once the process that
  started it is gone. Re-judging is idempotent: the handler rebuilds the run's fixtures from scratch. Recovery
  failure cannot block startup, since refusing to start would turn a recoverable situation into a total outage.
  `compose.yaml` gains `restart: unless-stopped`, without which nothing restarts the process to run the pass.
  *Verified live:* planted a Pending run, restarted the app, saw
  `Found 1 run(s) left unfinished by a previous process; re-enqueueing them`, and the run re-judged to
  **Completed**.
  **B1 completed:** added `CancelTestRun` and `RequeueTestRun`. Cancel is a filtered `ExecuteUpdateAsync`, so
  pressing it on a run that finished between render and click leaves the real result alone, and it signals the
  container *after* the row is terminal so the executing handler cannot race back over it.
  `IActiveTestRunRegistry.CancelForRun` resolves through the competitor index so a stale run id cannot kill the
  submission that superseded it. Re-judge creates a **new** run rather than resetting the old one, because an
  expert settling a dispute needs to see what happened the first time. This is also **B2's recovery mechanism**:
  Gitea does not retry a failed delivery, so without it a submission whose one delivery timed out was never
  judged and no operator action existed.
  *Verified:* build + 86 tests green, and the buttons render correctly — "Judge again" present, Cancel correctly
  absent on a Completed run. **Not verified:** the click end to end. The page did not navigate, which matches the
  dead-Blazor-circuit-after-rebuild issue rather than a handler fault, but it is unproven either way — exercise it
  on the next live run before relying on it.

- **B5 (the exploitable half) — a submission could execute code as root inside the judge.** `swap_dir` copied the
  competitor's `.csproj` verbatim, and `build_tests` compiles the swapped folder *before* the hidden suite. One
  `<Target BeforeTargets="Build"><Exec/></Target>` therefore ran arbitrary commands as root with the hidden tests
  writable next to it — a clean-looking full pass nothing server-side could distinguish from real work. The
  exclusion list was a denylist that had grown one entry per discovered vector; it is now an allowlist: a
  `.csproj`, `.props` or `.targets` from a submission is discarded and the session's own project file restored,
  because a build file is executable code rather than configuration.
  *Verified adversarially:* a new `msbuild-exec` persona whose project file tries to overwrite the hidden suite.
  Before: it would have passed everything. After: no `PWNED` marker, the wrong implementation fails on its merits,
  and the white-box matrix is **7/7**. Two new judge-base self-test checks pin the substitution, so a regression
  fails the build rather than a competition.
  Two things this changed, both recorded in the expectations: `adds-package` no longer reaches NuGet at all
  (stronger than the old exit 4), and because discarding silently would trade a clear diagnostic for a confusing
  unresolved type, a marker-error now tells the competitor their project file was ignored.
  **Still open in B5/B6:** the container still runs as root with `/app` writable, and the event stream is still
  written by a process the competitor controls — so a *white-box* mark remains self-reported. See below.

## Blockers remaining

| | what | why it blocks | size |
|---|---|---|---|
| B2 | Remainder: no reconciliation VIEW listing each competitor's latest judged commit, so "never judged" is invisible until someone looks | recovery now exists (re-judge), but nothing surfaces which runs need it | S |
| B5 | Remainder: the container still runs as root with `/app` writable — no `USER`, no `--read-only`, no `--cap-drop`. The MSBuild vector is closed, but any code the competitor's tests execute still runs privileged | a competitor who finds another execution path still has the hidden suite writable | M |
| B6 | The mark is whatever the container says: the logger shares a process with competitor code, and black-box sums every cobertura under a writable dir | forging a top mark needs ordinary C#, not an exploit | L |

## Audit findings that did not survive verification

- **"The stdout fallback can never fire because `LOG_DIRECTORY` is always set."** Wrong, and acting on it would
  have deleted a live safety net. Setting `LOG_DIRECTORY` tells the judge *where* to write; it does not mean the
  file was written. The fallback has three reachable branches — the file was never created (a third-party image
  that ignores the convention, or a judge that died before writing), the file exists but is empty (exactly what
  the historical NUL-fill incident produced), and the file is unreadable. `stdoutFallback` is populated because
  `ProcessContainerRunner` streams both stdout and stderr through the line handler. Left in place.

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
