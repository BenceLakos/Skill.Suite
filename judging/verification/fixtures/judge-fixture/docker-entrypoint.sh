#!/usr/bin/env bash
# Black-box judge pipeline (S4): the competitor writes the tests, the implementation is baked in.
#
# Their *.UnitTests folder replaces the placeholder, then the suite is measured rather than marked:
# how much of the reference implementation it covers, and how many injected faults it catches.

set -euo pipefail

. /usr/local/lib/judge-lib.sh

# Black-box runs the test step as ROOT. White-box drops to an unprivileged account; this pipeline does not, and
# it is a known gap rather than an oversight.
#
# Tried twice. Under the unprivileged account the coverage collector and Stryker produce no TRX, no cobertura and
# no mutation report, so the marker receives nothing and every submission scores 0. The second attempt was made
# after fixing a chown-on-a-volume-subpath bug that looked like the cause; it was not - the collectors still
# produced nothing, and no coverage or mutation event reached the stream at all.
#
# Scoring every competitor 0 is strictly worse than the exposure it would close, so this stays root until
# someone works out what the collectors actually require. The other black-box controls still apply: capabilities
# are dropped to five, no-new-privileges is set, the submission's project file is discarded, and the network is
# off. Tracked in docs/readiness.md.
export JUDGE_DROP_TEST_PRIVILEGES=false

judge_configure
judge_install_traps

judge_require JUDGE_SWAP_SRC JUDGE_SOLUTION JUDGE_TEST_PROJECT JUDGE_MARKING_MAP

# The reference implementation Stryker mutates, and the map that turns coverage and kill rate into a score.
# Sealed while the only code here is the session's own - the submission's tests have not run yet.
export JUDGE_SERVICES_SRC="${JUDGE_SERVICES_SRC:-$(dirname "${JUDGE_TEST_PROJECT}" | sed 's/UnitTests$/Services/')}"
judge_seal_graded_assets

TEST_RESULTS="${JUDGE_APP_DIR}/TestResults"
STRYKER_OUTPUT="${JUDGE_APP_DIR}/StrykerOutput"

swap_dir
restore_offline
build_tests

# Coverage and TRX in one pass. Red tests are still a result here: a suite with a wrong expectation should
# score lower, not fail the run.
run_tests \
    --logger "trx;LogFileName=results.trx" \
    --collect:"XPlat Code Coverage" \
    --results-directory "${TEST_RESULTS}" \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

judge_assert_results

# Verified BEFORE Stryker and the marker run: if the implementation was edited while the submission's tests were
# executing, every number computed after this point is meaningless.
judge_verify_graded_assets

# ---------------------------------------------------------------------------- mutation testing
#
# Stryker re-runs the whole suite once per mutant, and every one of those test hosts inherits LOG_DIRECTORY
# and opens events.jsonl with FileMode.Create. Left alone, the real run's events are destroyed and replaced
# by whatever the last mutant produced.
#
# Two defences, because losing the event stream here silently discards the entire submission:
#   1. `env -u LOG_DIRECTORY` so the mutant test hosts fall back to stdout
#   2. a copy of events.jsonl, restored afterwards if it changed anyway
cp "${JUDGE_EVENT_FILE}" "${JUDGE_WORK}/events.before-mutation.jsonl"

# LOG_DIRECTORY is REDIRECTED to a throwaway directory rather than unset. Unsetting it is not enough in
# practice: Stryker's mutant test hosts still ended up writing to the real events.jsonl, and because it runs
# them concurrently the result was a file interleaved from several truncating writers - a length that looked
# right, filled with NUL bytes, with every real test event gone. Giving them a valid but disposable target
# removes the failure mode instead of relying on the variable staying absent all the way down.
mutation_status=0
LOG_DIRECTORY="${JUDGE_WORK}/mutation-logs" \
    timeout --signal=TERM --kill-after=30s "${JUDGE_MUTATION_TIMEOUT_SECONDS}" \
    dotnet stryker --config-file "${JUDGE_APP_DIR}/stryker-config.json" \
    >"${JUDGE_WORK}/mutation.log" 2>&1 &
mutation_pid=$!
wait "${mutation_pid}" || mutation_status=$?

tail -40 "${JUDGE_WORK}/mutation.log" || true

if ! cmp -s "${JUDGE_EVENT_FILE}" "${JUDGE_WORK}/events.before-mutation.jsonl"; then
    printf 'judge: events.jsonl was modified during mutation testing; restoring the pre-mutation stream\n' >&2
    cp "${JUDGE_WORK}/events.before-mutation.jsonl" "${JUDGE_EVENT_FILE}"
fi

# Stryker writes into a timestamped run directory (StrykerOutput/<timestamp>/reports/), so the path cannot be
# hard-coded. Take the newest match; an absent report is a warning, not a failure.
MUTATION_REPORT=$(find "${STRYKER_OUTPUT}" -name 'mutation-report.json' -type f 2>/dev/null \
    | sort | tail -1)
: "${MUTATION_REPORT:=${STRYKER_OUTPUT}/reports/mutation-report.json}"
printf 'judge: mutation report at %s\n' "${MUTATION_REPORT}"

if (( mutation_status == 124 )); then
    # Not fatal: coverage and pass rate still score. The marker records the absent report as a warning.
    printf 'judge: mutation testing exceeded %ss and was stopped\n' "${JUDGE_MUTATION_TIMEOUT_SECONDS}" >&2
    emit_marker_error "mutation testing exceeded ${JUDGE_MUTATION_TIMEOUT_SECONDS}s; the mutation score is 0 for this run."
elif (( mutation_status != 0 )); then
    printf 'judge: mutation testing failed (exit %s)\n' "${mutation_status}" >&2
    emit_marker_error "mutation testing failed: $(judge_errors "${JUDGE_WORK}/mutation.log")"
fi

# Flush before the marker runs, so the diagnostics are in the file it appends to rather than after it.
flush_events

# ---------------------------------------------------------------------------- scoring
#
# Runs last, and appends. The globs are unquoted on purpose: skill-marker takes several values per flag, so
# an expanded glob works directly. Absent files are warnings, not errors.
# shellcheck disable=SC2046
skill-marker score \
    --map "${JUDGE_MARKING_MAP}" \
    --events "${JUDGE_EVENT_FILE}" \
    --trx $(find "${TEST_RESULTS}" -name '*.trx' 2>/dev/null | tr '\n' ' ') \
    --coverage $(find "${TEST_RESULTS}" -name 'coverage.cobertura.xml' 2>/dev/null | tr '\n' ' ') \
    --mutation "${MUTATION_REPORT}"

# The per-aspect report is only meaningful when the map declares aspects; for a pure measurement session it
# is skipped.
if [[ "${JUDGE_EMIT_ASPECT_REPORT:-false}" == "true" ]]; then
    skill-marker report \
        --map "${JUDGE_MARKING_MAP}" \
        --events "${JUDGE_EVENT_FILE}" \
        --out "${LOG_DIRECTORY}/cis-report.csv"
fi
