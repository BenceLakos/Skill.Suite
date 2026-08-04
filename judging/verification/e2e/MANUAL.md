# Manual end-to-end walkthrough

Step-by-step instructions to exercise the platform by hand. `run-e2e.sh` automates most of this; do it manually
when you want to see the UI, or to cover the two things the script deliberately cannot:

- **A private registry / private repository**, which needs a `Credential`. Those rows are
  DataProtection-encrypted, so only the running application can create them.
- **The competitor's own view.** Creating a competitor through the UI also provisions an Identity login with
  the same id, so you can sign in as them and see the score bucket. The script inserts the competitor with
  SQL, which gives no login.

Times are rough: about 20 minutes the first time, most of it the application image build.

---

## 0. Build the judge images

The platform runs `docker run` with no `--pull`, so the image must already exist on the host daemon.

```bash
docker build -t skill-suite-judge-base:9.0 judging/docker/judge-base
```
```bash
docker build -f judging/docker/judge-smoke/Dockerfile -t skill-suite-judge-smoke:1 judging
```

Start with `judge-smoke`. It ignores the checkout and emits a fixed event stream, so if the results are wrong
the fault is in the platform or the configuration — never in a session's tests.

Optionally also build a real white-box judge, which additionally exercises clone → swap → offline restore →
build → test:

```bash
judging/build-images.sh
```

That builds every judge image, including the fixture and the Fibonacci example. For one module on its own, use
its own script — no build arguments, because the judge contract is `ENV` in the module's Dockerfile:

```bash
cd judging/samples/fibonacci-session && ./pack-contracts.sh && BASE_IMAGE=skill-suite-judge-base:9.0 ./build-image.sh
```

## 1. Start Postgres and Gitea

```bash
docker compose -f infra/compose.yaml up -d postgres gitea
```

Wait until both are up:

```bash
docker exec skill-suite-postgres pg_isready -U skillsuite -d skillsuite
```
```bash
curl -sf http://localhost:3000/api/healthz && echo gitea-ok
```

## 2. Give yourself an application admin, then start the app

There is **no admin account by default** — `IdentitySeeder` only creates one if `Seed:Admin` is configured, so
without this you cannot log in. Add it to `compose.override.yaml` under `services.skill.suite.environment`:

```yaml
      Seed__Admin__Username: "admin"
      Seed__Admin__Password: "Admin123!"
      Seed__Admin__FullName: "E2E Admin"
```

The password must satisfy the configured Identity policy (by default upper, lower, digit, symbol, 6+ chars) or
startup fails with a seeding error.

```bash
docker compose up -d --build skill.suite
```

The first build takes several minutes. `compose.override.yaml` drops the `linux/amd64` pin so it builds for
your host architecture; production still uses `compose.yaml` alone.

Then open <http://localhost:8080> and sign in with those credentials.

```bash
docker compose logs -f skill.suite   # watch for "TestRunWorker started with N concurrent slot(s)"
```

## 3. Set up Gitea

1. Open <http://localhost:3000>. If the installation page appears, submit it — the database fields are already
   filled in from the compose environment.
2. Register a user (the first registered Gitea account becomes an administrator), or create one from the CLI:
   ```bash
   docker exec -u git skill-suite-gitea gitea admin user create \
     --admin --username giteaadmin --password 'Gitea123!' \
     --email giteaadmin@example.invalid --must-change-password=false
   ```
3. **Create a repository named exactly after the competitor**, e.g. `alice`. This is not cosmetic: the platform
   attributes a submission by taking the last path segment of the repository URL, stripping `.git`, and
   matching it against `Competitor.Username`. A mismatch produces a run with no competitor attached — which
   also means it can never be superseded.
   - Public is simplest. Choose **private** if you want to exercise the git credential in step 4.
4. In that repository: **Settings → Webhooks → Add Webhook → Gitea**
   - Target URL: `http://skill-suite:8080/webhooks/git`
   - Method: `POST`, content type `application/json`
   - Trigger: **Push events** only
   - Active: yes

   The container name, not `localhost` — Gitea reaches the app over the compose network. The webhook allow-list
   in `infra/compose.yaml` already permits it. Use **Test Delivery** to confirm; a `202 Accepted` means the
   webhook arrived (it will report no active session until step 4).

## 4. Set up Skill Suite (in the UI, as admin)

1. **Credential** — only needed for a private repository or a private registry. Skip for a public repo and a
   local image.
   - Kind `Gitea` for cloning; kind `Gitea` or `Nexus` for pulling the judgement image (both appear in the
     session's pull-credential list).
   - The secret format is `username:token` — a Gitea personal access token with repository read scope.
2. **Docker image** — required even for a local image, because the session's judgement-image field is a
   dropdown fed from these records.
   - Source: `Pull`
   - Image name: `skill-suite-judge-smoke:1` (exactly as tagged locally)
3. **Competitor**
   - Username: exactly the repository name from step 3, e.g. `alice`
   - Set a password you will use in step 6 — creating a competitor here also provisions a login for them.
4. **Session**
   - Judgement image: pick the record from step 2
   - Git credential / judgement-image pull credential: only if you created one
   - Starts at: in the past. Ends at: in the future
   - Status: **Active**

   Make sure it is the **only** Active session. The webhook picks the Active session with the earliest start
   date, so a leftover from previous work will silently judge with the wrong image.

## 5. Push a submission

```bash
git clone http://localhost:3000/giteaadmin/alice.git /tmp/alice && cd /tmp/alice
echo "# submission" > README.md
git add -A && git commit -m "first submission"
git push origin main
```

Pushing needs authentication even to a public repository. For a real white-box judge, put the competitor's
solution folder in the repository instead of a README, for example:

```bash
cp -R judging/verification/personas/fixture/red/. /tmp/alice/
```

## 6. Observe the result

**As admin**, open the test runs list. The run should move `Cloning` → `Running` → `Completed` and refresh
live. Open it and check:

- Two fixtures, three unit tests for `judge-smoke`, with one passed, one failed and one errored
- The failed test shows its assertion; the errored one shows its call and `threw`
- The `overall` metric fixture shows quality `0.5`
- The **Aspect** column is populated (`S1.1`, `S1.2`, `S1.3`)
- No **Judge diagnostics** panel — it only appears when the judge emitted a `marker-error`

**As the competitor**, sign out and sign in with the credentials from step 4.3, then open your test runs:

- `SmokeChecks` shows a score bucket of **Solid** (1 of 2 visible aspects passed → 50%)
- `SmokeErrors` shows **Needs work** (0 of 1)
- No raw judge output anywhere — a failed run shows only a generic category, never the container's stderr

## 7. Optional extra checks

**Staff log download** — from the run details page, download the raw `events.jsonl` and confirm it matches
`judging/docker/judge-smoke/expected-events.jsonl` (timestamps aside).

**Reprocess** — the run details page has a reprocess action. It deletes and rebuilds all fixtures from the
stored log, so the results should be identical afterwards. This is the only part of the chain with no automated
coverage, because it needs an authenticated staff session.

**Supersede** — push twice in quick succession. The first run should end `Cancelled` with
"Superseded by a newer submission…". Use a slow judge to make the race reliable:

```bash
cp -R judging/verification/personas/fixture/slow/. /tmp/alice/
git -C /tmp/alice add -A && git -C /tmp/alice commit -m one && git -C /tmp/alice push
git -C /tmp/alice commit --allow-empty -m two && git -C /tmp/alice push
```

**Timeout** — that same `slow` persona hits the judge's own wall clock and should end `Failed` with a reason
mentioning the timeout, rather than hanging forever.

**Parallel judging** — with `Webhook__MaxConcurrentRuns` at 2 or more, push from two *different* competitor
repositories at once and confirm both runs are `Running` simultaneously.

## 8. Tear down

```bash
docker compose down && docker compose -f infra/compose.yaml down
```

Add `-v` to drop the volumes too, discarding the database and Gitea state so the next run provisions from
nothing — the more honest test.

---

## Things that will bite you

| Symptom | Cause |
|---|---|
| Webhook returns 409, no run created | No Active session, or more than one and the earliest is not yours |
| Run created but no competitor attached | Repository name does not match `Competitor.Username` exactly |
| Run `Failed`, "container exited with code 125/127" | Judge image not on the host daemon — `docker run` never pulls |
| Image pull fails against a private registry | The registry host in the image reference must contain a `.` or a `:`, or be exactly `localhost`. A bare `gitea/owner/img:tag` is read as a Docker Hub namespace |
| Webhook never arrives | Target URL used `localhost` instead of the `skill-suite` container name |
| Cannot log in at all | `Seed:Admin` not configured, so no admin was ever created |
| Run `Completed` with zero fixtures | Should no longer happen — it is now reported `Failed` with a "produced no test results" reason, which usually means the submission does not compile |
| App fails at startup with a key-ring error | The data-protection keys volume was removed but the database still holds secrets encrypted with the old key |
