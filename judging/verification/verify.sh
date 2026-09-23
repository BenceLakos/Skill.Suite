#!/usr/bin/env bash
# verify.sh <session> [persona]
#
# Runs every persona for a session through its judge image and diffs the result against
# expected/<session>.json. This is the local acceptance gate: it answers "does this image report each
# failure mode distinguishably?" without needing the platform, Postgres or Gitea.
#
#   judging/verification/verify.sh fixture
#   judging/verification/verify.sh fixture broken-code
#
# The platform only ever sees an exit code and an event stream, so those are exactly what is asserted.

set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SESSION=${1:-}
ONLY=${2:-}

if [[ -z "${SESSION}" ]]; then
    printf 'usage: %s <session> [persona]\n' "$(basename "$0")" >&2
    exit 2
fi

EXPECTED="${HERE}/expected/${SESSION}.json"
if [[ ! -f "${EXPECTED}" ]]; then
    printf 'verify: no expectations for session %s (%s)\n' "${SESSION}" "${EXPECTED}" >&2
    exit 2
fi

IMAGE=$(python3 -c "import json;print(json.load(open('${EXPECTED}'))['image'])")
NAMES=$(python3 -c "import json;print(' '.join(p['name'] for p in json.load(open('${EXPECTED}'))['personas']))")

if ! docker image inspect "${IMAGE}" >/dev/null 2>&1; then
    printf 'verify: image %s not found. Build it first.\n' "${IMAGE}" >&2
    exit 2
fi

OUT_ROOT=$(mktemp -d)
PASSED=0
FAILED=0

for name in ${NAMES}; do
    if [[ -n "${ONLY}" && "${ONLY}" != "${name}" ]]; then
        continue
    fi

    repo="${HERE}/personas/${SESSION}/${name}"
    if [[ ! -d "${repo}" ]]; then
        printf 'FAIL %-16s persona directory missing: %s\n' "${name}" "${repo}"
        FAILED=$((FAILED + 1))
        continue
    fi

    out="${OUT_ROOT}/${name}"
    mkdir -p "${out}"

    # Persona-specific environment, e.g. a shorter wall clock for the endless-loop case.
    mapfile -t env_args < <(python3 -c "
import json
personas = {p['name']: p for p in json.load(open('${EXPECTED}'))['personas']}
for key, value in (personas['${name}'].get('env') or {}).items():
    print('-e')
    print(f'{key}={value}')
")

    # Extra `docker run` flags, for a persona that has to be judged under the container hardening the
    # platform applies rather than under `docker run`'s defaults. `slow-hardened` is the reason this exists:
    # the cap list once omitted CAP_KILL, the judge could not signal the unprivileged account its own test
    # step runs as, and the wall clock reported 137 instead of 124 - a fault a matrix run with default flags
    # could not reproduce at all. The flags are copied from DockerRunArguments.Build; keep them in step.
    mapfile -t docker_args < <(python3 -c "
import json
personas = {p['name']: p for p in json.load(open('${EXPECTED}'))['personas']}
for arg in (personas['${name}'].get('dockerArgs') or []):
    print(arg)
")

    printf '.... %-16s running\n' "${name}"

    actual_exit=0
    docker run --rm \
        -v "${repo}:/workspace:ro" \
        -v "${out}:/var/log/skill-suite" \
        -w /workspace \
        -e COMPETITOR_DIRECTORY=/workspace \
        -e LOG_DIRECTORY=/var/log/skill-suite \
        "${env_args[@]}" \
        "${docker_args[@]}" \
        "${IMAGE}" >"${out}/stdout.log" 2>"${out}/stderr.log" || actual_exit=$?

    report=$(python3 - "${EXPECTED}" "${name}" "${actual_exit}" "${out}/events.jsonl" <<'PY'
import json, os, sys

expected_path, name, actual_exit, events_path = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4]
spec = {p['name']: p for p in json.load(open(expected_path))['personas']}[name]

events = []
if os.path.exists(events_path):
    for line in open(events_path):
        line = line.strip()
        if not line:
            continue
        try:
            events.append(json.loads(line))
        except json.JSONDecodeError:
            problems = ['events.jsonl contains a line that is not valid JSON']
            print(json.dumps({'problems': problems}))
            sys.exit(0)

problems = []

if actual_exit != spec['exit']:
    problems.append(f"exit code {actual_exit}, expected {spec['exit']}")

# An empty-but-present events.jsonl is its own bug: the platform prefers the file whenever it exists, so it
# would report zero results and also suppress the stdout fallback.
if os.path.exists(events_path) and os.path.getsize(events_path) == 0:
    problems.append('events.jsonl exists but is empty')

finishes = [e for e in events if e.get('event') == 'finish-unit-test']
outcomes = {}
for e in finishes:
    outcomes[e.get('outcome')] = outcomes.get(e.get('outcome'), 0) + 1

for key, outcome in (('passed', 'passed'), ('failed', 'failed'), ('errored', 'errored')):
    if key in spec and outcomes.get(outcome, 0) != spec[key]:
        problems.append(f"{key}={outcomes.get(outcome, 0)}, expected {spec[key]}")

if 'units' in spec and len(finishes) != spec['units']:
    problems.append(f"units={len(finishes)}, expected {spec['units']}")

diagnostics = [e.get('detail', '') for e in events if e.get('event') == 'marker-error']

if spec.get('markerError') and not diagnostics:
    problems.append('expected a marker-error event, found none')
if spec.get('markerError') is False and diagnostics:
    problems.append(f'unexpected marker-error: {diagnostics[0][:120]}')

needle = spec.get('markerErrorContains')
if needle and not any(needle in d for d in diagnostics):
    found = diagnostics[0][:120] if diagnostics else '<none>'
    problems.append(f'marker-error should mention {needle!r}; got {found!r}')

# Black-box sessions are scored, not checked, so their expectations are quality bands. Deliberately a band
# and not a fixed value: mutation testing has some run-to-run variance, and a brittle equality assertion
# would train everyone to ignore this suite.
scores = [e for e in events if e.get('event') == 'score']
overall = next((e for e in scores if e.get('part', 'overall') == 'overall'), None)

if 'qualityMin' in spec or 'qualityMax' in spec:
    if overall is None:
        problems.append('expected a score event for part "overall", found none')
    else:
        quality = overall.get('value')
        if not isinstance(quality, (int, float)):
            problems.append(f'score value must be a number, got {quality!r}')
        else:
            low, high = spec.get('qualityMin', 0.0), spec.get('qualityMax', 1.0)
            if not (low <= quality <= high):
                problems.append(f'quality {quality} outside the expected band {low}..{high}')

# Coverage is asserted separately from quality for the "weak suite" case, where the whole point is that
# coverage matches a good suite while the score does not.
overall_coverage = next((e for e in events if e.get('event') == 'coverage'
                         and not e.get('fixture') and e.get('part', 'overall') == 'overall'), None)

if 'coverageMin' in spec:
    rate = (overall_coverage or {}).get('value')
    if not isinstance(rate, (int, float)) or rate < spec['coverageMin']:
        problems.append(f'line coverage {rate} below the expected minimum {spec["coverageMin"]}')

# The one exact assertion in a file of bands, and it earns the exception: a line count is a property of the
# implementation under test, so it is the same on every run - unlike a kill rate. It exists because the
# judge used to glob the same cobertura report twice (coverlet's own copy and the TRX attachment) and report
# double the real totals, with the rate - and so every band above - entirely unaffected.
for key, field in (('coverageTotal', 'total'), ('coverageCovered', 'covered')):
    if key in spec:
        actual = (overall_coverage or {}).get(field)
        if actual != spec[key]:
            problems.append(f'overall coverage {field}={actual}, expected exactly {spec[key]}')

# Per-test-class measurements. Asserted as "every class the harness opened has both, with a real number in
# range" rather than against fixed values: the point is that the plumbing reaches every fixture, and exact
# per-class numbers are as run-to-run variable as the rollup's.
#
# The fixture set comes from start-fixture, i.e. from the runner's own view of which classes existed, so a
# feature that silently measured only the first class cannot pass this.
if spec.get('fixtureMetrics'):
    classes = sorted({e.get('fixture') for e in events
                      if e.get('event') == 'start-fixture' and e.get('fixture')})

    if len(classes) < spec.get('fixtureMetricsMin', 1):
        problems.append(f'expected at least {spec.get("fixtureMetricsMin", 1)} test classes, found {classes}')

    for kind in ('coverage', 'mutation'):
        measured = {e.get('fixture'): e.get('value') for e in events
                    if e.get('event') == kind and e.get('fixture')}

        for name in classes:
            if name not in measured:
                problems.append(f'no per-fixture {kind} event for {name}')
            elif not isinstance(measured[name], (int, float)):
                problems.append(f'per-fixture {kind} for {name} is {measured[name]!r}, not a number')
            elif not (0.0 <= measured[name] <= 1.0):
                problems.append(f'per-fixture {kind} for {name} is {measured[name]}, outside 0..1')

    # A fixture measures a slice of the submission, so it can never have reached more mutants than the whole
    # suite did. This is the cheap check that catches an attribution that double-counts.
    total_covered = next((e.get('covered') for e in events if e.get('event') == 'mutation'
                          and not e.get('fixture') and e.get('part', 'overall') == 'overall'), None)
    if isinstance(total_covered, int):
        for e in events:
            if e.get('event') == 'mutation' and e.get('fixture') and isinstance(e.get('covered'), int):
                if e['covered'] > total_covered:
                    problems.append(
                        f'{e["fixture"]} covered {e["covered"]} mutants, more than the whole suite\'s {total_covered}')

    # Quality stays a part-level verdict. A score event naming a fixture would mean a competitor's own test
    # class had been given a mark the marking map never authorised.
    if any(e.get('event') == 'score' and e.get('fixture') for e in events):
        problems.append('a score event carried a fixture; quality must stay part-scoped')

print(json.dumps({'problems': problems}))
PY
)

    problems=$(printf '%s' "${report}" | python3 -c "import json,sys;print('\n'.join(json.load(sys.stdin)['problems']))")

    if [[ -z "${problems}" ]]; then
        printf '\033[1Aok   %-16s exit %-3s as expected\n' "${name}" "${actual_exit}"
        PASSED=$((PASSED + 1))
    else
        printf '\033[1AFAIL %-16s\n' "${name}"
        printf '%s\n' "${problems}" | sed 's/^/       /'
        printf '       logs: %s\n' "${out}"
        FAILED=$((FAILED + 1))
    fi
done

printf '\n%s: %d passed, %d failed\n' "${SESSION}" "${PASSED}" "${FAILED}"

if (( FAILED > 0 )); then
    printf 'artifacts kept in %s\n' "${OUT_ROOT}"
    exit 1
fi

rm -rf "${OUT_ROOT}"
