# judge-smoke

A judgement image that ignores the competitor's code and emits a fixed event stream, then exits 0.

Its only job is to answer "is the platform wired up correctly?" without involving any session's code. Point
a session at this image and push to a competitor repo: if the results below appear in the UI, then the
webhook, the clone, the volume mounts, the container invocation, the event parser, the score bucket and the
live refresh all work. Only then is it worth debugging a real judge image.

## Build and run

```bash
docker build -f judging/docker/judge-smoke/Dockerfile -t ghcr.io/bencelakos/skill-suite-judge-smoke:1 judging
```
```bash
judging/tools/judge-run.sh ghcr.io/bencelakos/skill-suite-judge-smoke:1 /any/directory
```

The build context is `judging/` because the image compiles `Skill.Suite.TestLog` from source — this is also
the dogfooding check that the producer library still emits a stream the platform can parse.

## What it emits

| Fixture | Test | Verdict | Aspect |
|---|---|---|---|
| `SmokeChecks` | `Smoke_PassingCase_Passes` | passed | `S1.1`, visible |
| `SmokeChecks` | `Smoke_FailingCase_Fails` | failed, via an assertion with `passed:false` | `S1.2`, visible |
| `SmokeErrors` | `Smoke_ErroredCase_Throws` | errored, via a `call` carrying `threw` | `S1.3`, visible |

Plus a `test-summary` and a `score` with `quality: 0.5`, and exit code 0.

All three verdict paths are covered because each reaches the UI by a different route — a failure promoted
from an assertion and one promoted from a thrown call are separate code paths in the parser.

## What to check in the UI

- Run status **Completed** (exit 0).
- Two fixtures, three unit tests, with exactly the verdicts above.
- The failing test shows its assertion; the errored test shows its call and `threw`.
- `SmokeChecks` reports **1 of 2 aspects passed**, `SmokeErrors` **0 of 1**.
- The metric fixture `overall` shows quality 0.5, bucketing to **High**.
- The staff log download returns the same `events.jsonl`, and `ReprocessTestRunLog` reproduces the run
  unchanged.

The score's `value` is fixed at 0.5 so the expected bucket never drifts. If any of this differs, the problem is in
the platform or the session configuration, not in a session's tests.

## Operator setup

The judgement-image field is a dropdown fed from registered Docker image records, so the image has to be
registered before it can be selected:

1. Create a `Credential` (kind `Nexus`, secret `username:token`) if the registry is private.
2. Create a `DockerImage` with `Source = Pull` and `ImageName` set to the full reference.
3. On the session: set `JudgementImage` to that reference, set `JudgementImagePullCredentialId` and
   `GitCredentialId`, and set the session `Active`.

The registry host in the reference must contain a dot or a colon, or be exactly `localhost` — a bare
`gitea/owner/image:tag` is read as a Docker Hub namespace and the pull silently goes to docker.io.
