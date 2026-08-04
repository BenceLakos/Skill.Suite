#!/usr/bin/env bash
# judge-run.sh <image> <repo-dir> [out-dir]
#
# Runs a judgement image the way the platform runs it, without needing the platform. Prints the exit code
# and the resulting events.
#
#   judging/tools/judge-run.sh ghcr.io/bencelakos/skill-suite-judge-smoke:1 ./verification/personas/fixture/happy
#
# The flags below mirror ProcessContainerRunner exactly, and two of them matter more than they look:
#
#   -w /workspace   the platform sets the working directory to the competitor checkout, overriding the
#                   image's own WORKDIR
#   :ro             ...and that directory is read-only
#
# Any judge step that writes to the working directory before cd-ing elsewhere works fine in a casual
# `docker run` and fails only in production. Reproducing both here is the point of this script.

set -euo pipefail

if (( $# < 2 )); then
    printf 'usage: %s <image> <repo-dir> [out-dir]\n' "$(basename "$0")" >&2
    exit 2
fi

IMAGE=$1
REPO_DIR=$(cd "$2" && pwd)
OUT_DIR=${3:-$(mktemp -d)}

mkdir -p "${OUT_DIR}"
# Start clean, or a previous run's events would be mistaken for this one's.
rm -f "${OUT_DIR}/events.jsonl"

CONTAINER="judge-run-$$"

printf 'image:  %s\n' "${IMAGE}"
printf 'repo:   %s\n' "${REPO_DIR}"
printf 'logs:   %s\n\n' "${OUT_DIR}"

STATUS=0
docker run --rm --name "${CONTAINER}" \
    -v "${REPO_DIR}:/workspace:ro" \
    -v "${OUT_DIR}:/var/log/skill-suite" \
    -w /workspace \
    -e COMPETITOR_DIRECTORY=/workspace \
    -e LOG_DIRECTORY=/var/log/skill-suite \
    "${@:4}" \
    "${IMAGE}" || STATUS=$?

printf '\n---------------------------------------------------------------\n'
printf 'exit code: %d  ' "${STATUS}"

case "${STATUS}" in
    0) printf '(platform would record: Completed)\n' ;;
    2) printf '(image misconfigured)\n' ;;
    3) printf '(competitor folder missing or unusable)\n' ;;
    4) printf '(offline restore failed)\n' ;;
    5) printf '(step exceeded its wall clock)\n' ;;
    6) printf '(submission does not compile)\n' ;;
    *) printf '(platform would record: Failed)\n' ;;
esac

EVENTS="${OUT_DIR}/events.jsonl"

if [[ ! -e "${EVENTS}" ]]; then
    printf '\nNo events.jsonl was produced. The platform would fall back to parsing stdout.\n'
    exit "${STATUS}"
fi

if [[ ! -s "${EVENTS}" ]]; then
    printf '\nevents.jsonl exists but is EMPTY. The platform prefers the file whenever it exists, so this\n'
    printf 'run would show zero results AND the stdout fallback would be suppressed. Treat as a bug.\n'
    exit "${STATUS}"
fi

printf '\nevents.jsonl (%s lines)\n' "$(wc -l <"${EVENTS}" | tr -d ' ')"
printf '%s\n' '---------------------------------------------------------------'

# Summarise rather than dumping: a real suite produces hundreds of lines and the interesting part is the
# verdicts and the diagnostics.
awk '
    /"event":"start-fixture"/   { fixtures++ }
    /"event":"finish-unit-test"/{ units++ }
    /"outcome":"passed"/        { passed++ }
    /"outcome":"failed"/        { failed++ }
    /"outcome":"errored"/       { errored++ }
    /"event":"marker-error"/    { errors++ }
    END {
        printf "fixtures: %d  units: %d  passed: %d  failed: %d  errored: %d\n",
               fixtures, units, passed, failed, errored
        if (errors) printf "marker-error events: %d\n", errors
    }
' "${EVENTS}"

if grep -q '"event":"marker-error"' "${EVENTS}"; then
    printf '\ndiagnostics:\n'
    grep '"event":"marker-error"' "${EVENTS}" | sed 's/^/  /'
fi

if grep -q '"event":"score"' "${EVENTS}"; then
    printf '\nscores:\n'
    grep '"event":"score"' "${EVENTS}" | sed 's/^/  /'
fi

printf '\nlast 5 events:\n'
tail -5 "${EVENTS}" | sed 's/^/  /'

exit "${STATUS}"
