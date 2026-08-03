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
  Still open in B7: surfacing provisioning errors in the Blazor UI.

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

## Blockers remaining

| | what | why it blocks | size |
|---|---|---|---|
| B1 | No durability, no operator recovery: in-memory `Channel` queue, `Pending` never re-scanned, no re-run/cancel, no `restart:` policy | a redeploy or crash freezes accepted submissions forever with no button to fix them | M |
| B2 | A submission is judged only if its single webhook delivery succeeds; nothing reconciles an unjudged HEAD | one failed delivery loses a mark silently and irrecoverably | M |
| B4 | Remainder: marker's `EventSink.Flush()` truncate-then-rewrite can destroy the event stream on kill/ENOSPC; the stdout fallback can never fire because `LOG_DIRECTORY` is always set | the last line of defence for a lost save | S |
| B5 | Competitor code runs as **root** with `/app` writable; their `.csproj` is built before the hidden suite. Verified: no `USER`, no `--read-only`, `--cap-drop` or `--user` | one MSBuild `Exec` target yields an undetectable clean pass | M |
| B6 | The mark is whatever the container says: the logger shares a process with competitor code, and black-box sums every cobertura under a writable dir | forging a top mark needs ordinary C#, not an exploit | L |
| B7 | Remainder: provisioning errors are swallowed by the Blazor page instead of shown | an operator cannot tell a partial Start from a clean one | S |

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
