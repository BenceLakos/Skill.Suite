# Publishing

Two kinds of artifact, two destinations, and the split matters more than the commands.

| artifact | contains | goes to |
|---|---|---|
| `Skill.Suite.TestLog`, `.TestLog.Xunit`, `.Marker`, `.StarterKit` | nothing session-specific | nuget.org, from public CI |
| `judge-base`, `judge-smoke` | nothing session-specific | GHCR, from public CI |
| **your session images** | **the hidden suite, or the reference implementation as source** | **private registry only** |

## The generic layer (Skill.Suite repo, public CI)

Tag-driven. From the Skill.Suite repository:

```bash
git tag judging-v1.0.0 && git push origin judging-v1.0.0
```

`.github/workflows/judging-release.yml` builds, tests, packs the packages to nuget.org and pushes `judge-base`
and `judge-smoke` to GHCR. It needs `NUGET_API_KEY`; GHCR uses the built-in `GITHUB_TOKEN`.

`workflow_dispatch` runs it with `dry_run: true` by default — use that to check packing before minting a tag.

Session images are deliberately absent from that workflow. Do not add them.

## Your session images (private, never public CI)

Build locally or on a private runner, then push to the private registry. The registry host is
deployment-specific, so it is a variable rather than something checked in:

```bash
REGISTRY=<gitea-host>/<owner> TAG=1.0.0 ./build-image.sh both
```

```bash
docker login <gitea-host>
docker push <gitea-host>/<owner>/<session>-judge:1.0.0
docker push <gitea-host>/<owner>/<session>-blackbox-judge:1.0.0
```

### Tag immutably, always

The platform runs `docker run` with **no `--pull`**. Re-pushing a tag it has already pulled leaves it judging
with the stale local image, and the symptom is a session that marks against a contract nobody can find. Bump the
version instead: `TAG=1.0.1`.

### One check before every push

```bash
docker run --rm --entrypoint sh <image> -c 'find /app -name "*.cs" | head -40'
```

For a **white-box** image, `<Session>.Services` must contain no `.cs` at all. For a **black-box** image, the
implementation *should* be there — Stryker needs it — but `<Session>.UnitTests` must be empty of sources.
`build-image.sh` asserts the white-box case for you; the black-box case is asserted inside the build.

If an image with the answer key has already been pushed, deleting the tag is not enough — assume anyone who
could pull it did. Rotate to a new session or a new task.

## Registry reference gotcha

The platform reads the registry host off the image reference. A host must contain a `.` or a `:`, or be exactly
`localhost` — otherwise `gitea/owner/img:tag` is parsed as a Docker Hub namespace and the pull fails with a
confusing not-found. Use `gitea.example.com/owner/img:tag` or `gitea:3000/owner/img:tag`.

## Wiring it into a running platform

The judgement-image field is a dropdown fed from `DockerImage` records, so the image has to be registered in
the UI before a session can use it:

1. **Credentials** → a `Gitea` or `Nexus` credential for pulling, secret formatted `username:token`.
2. **Docker images** → source `Pull`, the full immutable reference.
3. **Sessions** → pick that image, set the git credential, and set **template folder** to your generated
   `competitor-start/` directory — **not** the swapped folder inside it. That directory is pushed *as the
   repository root*, and `swap_dir` then looks for `<repo-root>/<SwappedFolder>`; point one level deeper and
   every run exits 3 with "the submission has no <SwappedFolder> folder". The path must also be readable
   *inside the application container* — a host path is invisible to it and produces empty repositories.
4. **Start** the session. It creates the organisation, seeds a template repository from the starter kit, copies
   it into one repository per competitor, and installs the signed push webhook last.

Start is safe to re-run: every git-host step treats "already exists" as success, so a partly-failed
provisioning run retries only the competitors that did not get a repository.
