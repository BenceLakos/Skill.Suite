# End-to-end kit

Proves the whole platform chain on one machine: a **git push** reaches the webhook, the submission is cloned,
the judgement container runs, its `events.jsonl` is parsed, and the results land in the database with the
competitor-visible score computed.

Everything under `judging/verification/` up to this point tests judge images in isolation. This is the only
thing that tests the *platform* — and it is the part most likely to be quietly broken, because a judge image
can be perfect while the webhook never fires.

## Run it

```bash
judging/verification/e2e/run-e2e.sh
```
```bash
judging/verification/e2e/run-e2e.sh judge-fixture:1 bob
```

Arguments are the judge image (default `skill-suite-judge-smoke:1`) and the competitor name (default `alice`).
The image must already exist on the host daemon: `docker run` is invoked without `--pull`, and these images are
local rather than published.

Every step is idempotent, so re-running is safe and is the normal way to use it.

To send real submission content instead of a placeholder README:

```bash
E2E_PERSONA=judging/verification/personas/fixture/red judging/verification/e2e/run-e2e.sh judge-fixture:1 bob
```

Start with the smoke image. It ignores the checkout entirely and emits a fixed event stream, so if it does not
produce the expected run the fault is in the platform or the provisioning — never in a session's tests.

## What it provisions

| Step | What happens |
|---|---|
| Infrastructure | `infra/compose.yaml` (Postgres + Gitea), then the app via `compose.yaml` + `compose.override.yaml` |
| Gitea | An admin user with a per-run generated password, a **public** repository named after the competitor, and a push webhook pointing at `http://skill-suite:8080/webhooks/git` |
| Platform records | A `Competitor` row and exactly one Active `Session` carrying the judgement image, inserted with SQL |
| Push | A commit pushed to the repository, which fires the webhook |
| Assertions | Polls for a terminal run, then checks status, fixtures, unit results, aspects and the log download |

The repository name is not cosmetic: `CompetitorResolver` takes the last path segment, strips `.git`, and
matches it against `Competitor.Username`. A mismatch produces a run with no competitor attached — which also
means it can never be superseded.

## Two deliberate shortcuts

**Records are inserted with SQL, not created through the UI.** The application exposes no admin API — only the
webhook, the staff log download and login — so scripting the UI would mean browser automation. Direct inserts
keep this repeatable.

**No credentials are involved, and that is the point.** The Gitea repository is public and the judge image is
local, so `Session.GitCredentialId` and `JudgementImagePullCredentialId` both stay null. A `Credential` row is
DataProtection-encrypted and can *only* be created by the running application, so needing one would have
forced UI automation.

The cost is real and worth stating: **the registry-auth and private-clone paths are not exercised.** Those
matter for the private Gitea registry that session images will live in. Covering them needs a `Credential`
created through the UI once, then referenced by the session — worth doing before the first real competition,
and the reason `RegistryHostExtractor`'s hostname rule is called out in the judge-base README.

## Reading the output

The script prints each fixture with its kind and tallies, and every annotated unit with its aspect and
visibility. That last line is the newest part of the chain — `[Aspect]` on a test method travelling through the
harness, the event stream, the parser and into the database — so it is worth glancing at even when the run
passes.

A `Completed` status with zero fixtures is the failure the platform used to report as success; the run is now
marked `Failed` instead, and the script fails if fixtures are missing.

## Tearing down

```bash
docker compose down && docker compose -f infra/compose.yaml down
```

Add `-v` to drop the volumes as well, which also discards the applied migrations and the Gitea state — the
next run then re-provisions from nothing, which is the more honest test.
