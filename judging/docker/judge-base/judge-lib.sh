#!/usr/bin/env bash
# judge-lib.sh - shared primitives for every Skill.Suite judgement image.
# Source it, never execute it:   . /usr/local/lib/judge-lib.sh
#
# The platform contract this encodes (verified against ProcessContainerRunner and
# ExecuteTestRunHandler, not assumed):
#
#   * Exactly TWO env vars are injected: COMPETITOR_DIRECTORY and LOG_DIRECTORY.
#   * No arguments follow the image, so the ENTRYPOINT *is* the judge.
#   * The container runs with `-w $COMPETITOR_DIRECTORY`, which is mounted READ-ONLY and overrides the
#     image's WORKDIR. Always cd to $JUDGE_APP_DIR yourself; never rely on the working directory.
#   * There is NO timeout, NO --memory/--cpus/--pids-limit, and the worker is serial - one hung
#     container stalls every other competitor. Hence the wall clocks here.
#   * Supersede is `docker stop --time 5`: five seconds of SIGTERM grace, then SIGKILL.
#   * Exit 0 => Completed, non-zero => Failed, and the whole of stderr is pasted into the run's failure
#     reason. Keep stderr to one line per fatal.
#   * The platform prefers $LOG_DIRECTORY/events.jsonl whenever it EXISTS. An existing but empty file
#     yields zero results AND suppresses the stdout fallback, so the file must end up either absent or
#     non-empty. Never leave it empty.

set -euo pipefail

# ---------------------------------------------------------------------------- exit codes

readonly JUDGE_EXIT_OK=0
readonly JUDGE_EXIT_CONFIG=2       # a required env var is missing or contradictory
readonly JUDGE_EXIT_SWAP=3         # the competitor's folder is absent or unusable
readonly JUDGE_EXIT_RESTORE=4      # offline restore failed - usually an added package reference
readonly JUDGE_EXIT_TIMEOUT=5      # a step exceeded its wall clock
readonly JUDGE_EXIT_BUILD=6        # the submission does not compile
# The reported results contradict the test framework's own output, i.e. the event stream was fabricated inside
# the test process. Distinct from a plain failure because it is an integrity finding, not a bad submission: the
# platform surfaces the reason, and an expert has to look before any mark from this run is used.
readonly JUDGE_EXIT_INTEGRITY=7

# ---------------------------------------------------------------------------- configuration

judge_configure() {
    : "${COMPETITOR_DIRECTORY:=/workspace}"
    : "${LOG_DIRECTORY:=/var/log/skill-suite}"
    : "${JUDGE_APP_DIR:=/app}"
    : "${JUDGE_TIMEOUT_SECONDS:=300}"
    # Stryker re-runs the whole suite once per mutant, so it needs its own, much larger budget.
    : "${JUDGE_MUTATION_TIMEOUT_SECONDS:=900}"
    : "${JUDGE_FAIL_ON_RED:=false}"
    : "${JUDGE_LOCAL_FEED:=${JUDGE_APP_DIR}/local-nuget}"
    : "${JUDGE_NUGET_CACHE:=/root/.nuget/packages}"
    # Unprivileged account the test step runs as. Overridable so an image can name its own.
    : "${JUDGE_TEST_USER:=competitor}"
    # Drop to JUDGE_TEST_USER for the test step. On for white-box, which is the common session type and where
    # the hidden suite sits next to the code being run.
    #
    # OFF for black-box, and this is a measured limitation rather than an oversight: that pipeline also runs the
    # coverage collector, Stryker and two globally-installed dotnet tools, and under the unprivileged account the
    # collectors produced no TRX or cobertura output at all - the marker then received empty --trx/--coverage
    # globs and printed its usage text, scoring every submission 0. A hardening change that silently zeroes
    # everyone's mark is worse than the exposure it closes, so black-box keeps running the test step as root
    # until the collector's requirements are worked out. Tracked in docs/readiness.md.
    : "${JUDGE_DROP_TEST_PRIVILEGES:=true}"

    JUDGE_WORK="$(mktemp -d)"
    JUDGE_STAGED_EVENTS="${JUDGE_WORK}/staged-events.jsonl"
    JUDGE_EVENT_FILE="${LOG_DIRECTORY}/events.jsonl"
    : >"${JUDGE_STAGED_EVENTS}"

    readonly JUDGE_WORK JUDGE_STAGED_EVENTS JUDGE_EVENT_FILE

    mkdir -p "${LOG_DIRECTORY}"
}

# judge_require <VAR_NAME>... - fail fast on missing configuration rather than producing a confusing
# downstream error (a missing JUDGE_SWAP_SRC would otherwise read as "the competitor deleted the folder").
judge_require() {
    local missing=()
    local name
    for name in "$@"; do
        if [[ -z "${!name:-}" ]]; then
            missing+=("${name}")
        fi
    done

    if (( ${#missing[@]} > 0 )); then
        judge_fatal "${JUDGE_EXIT_CONFIG}" "config" \
            "image is misconfigured: ${missing[*]} not set. This is an image bug, not a submission problem."
    fi
}

# ---------------------------------------------------------------------------- JSON

# json_escape <string> - the sdk:9.0 image ships neither jq nor python3, so this is parameter expansion
# plus coreutils only. Truncate BEFORE escaping so a cut can never land inside an escape sequence.
json_escape() {
    local raw=${1:-}
    local limit=${2:-1500}

    if (( ${#raw} > limit )); then
        raw="${raw:0:limit}..."
    fi

    raw=${raw//\\/\\\\}      # backslashes first, or later escapes get double-escaped
    raw=${raw//\"/\\\"}
    raw=${raw//$'\t'/\\t}
    raw=${raw//$'\r'/}
    raw=${raw//$'\n'/\\n}
    # Strip the remaining C0 controls, which JSON forbids unescaped.
    printf '%s' "${raw}" | tr -d '\000-\010\013\014\016-\037'
}

judge_timestamp() {
    date -u '+%Y-%m-%dT%H:%M:%S.000Z'
}

# ---------------------------------------------------------------------------- events

# emit_event <json-line> - stage one event.
#
# Events are STAGED, never written straight into events.jsonl. The xUnit harness opens that file with
# FileMode.Create when the test host starts, so anything written beforehand would be destroyed.
# flush_events merges the stage once the test host is gone.
emit_event() {
    printf '%s\n' "$1" >>"${JUDGE_STAGED_EVENTS}"
}

# emit_marker_error <detail> - the protocol diagnostic that tells the UI *why* a run failed instead of
# leaving only a red status and an exit code.
emit_marker_error() {
    emit_event "{\"timestamp\":\"$(judge_timestamp)\",\"event\":\"marker-error\",\"detail\":\"$(json_escape "$1")\"}"
}

# flush_events - merge staged events into events.jsonl. Must run after the test host has exited.
#
# Never creates an empty events.jsonl: the platform prefers the file whenever it exists, so an empty one
# would report zero results and also suppress the stdout fallback.
flush_events() {
    if [[ ! -s "${JUDGE_STAGED_EVENTS}" ]]; then
        return 0
    fi

    if [[ -s "${JUDGE_EVENT_FILE}" ]]; then
        # A SIGKILLed test host can leave a partial final line; don't glue onto it.
        if [[ -n "$(tail -c 1 "${JUDGE_EVENT_FILE}")" ]]; then
            printf '\n' >>"${JUDGE_EVENT_FILE}"
        fi
        cat "${JUDGE_STAGED_EVENTS}" >>"${JUDGE_EVENT_FILE}"
    else
        cat "${JUDGE_STAGED_EVENTS}" >"${JUDGE_EVENT_FILE}"
    fi

    : >"${JUDGE_STAGED_EVENTS}"
}

# judge_fatal <exit-code> <stage> <detail...> - the only way out of a failing path.
#
# The spec's single-argument emit_fatal cannot carry the exit codes the spec itself mandates, so the
# signature is widened. Always emits the diagnostic, always flushes, and keeps stderr to one line
# because the platform pastes all of it into the run's failure reason.
judge_fatal() {
    local code=$1
    local stage=$2
    shift 2
    local detail="$*"

    emit_marker_error "[${stage}] ${detail}"
    flush_events
    printf 'judge: %s failed: %s\n' "${stage}" "${detail}" >&2
    exit "${code}"
}

# Compatibility shim for the one-argument form in the spec.
emit_fatal() {
    judge_fatal 1 "judge" "$*"
}

# ---------------------------------------------------------------------------- signals

judge_install_traps() {
    trap judge_on_term TERM INT
    trap judge_on_exit EXIT
}

# The platform gives five seconds between SIGTERM and SIGKILL on supersede. Get the diagnostic on disk
# inside that window; anything already written by the test host is preserved.
judge_on_term() {
    emit_marker_error "run was stopped before it finished (superseded or cancelled)."
    flush_events
    exit 143
}

judge_on_exit() {
    local code=$?
    flush_events
    [[ -n "${JUDGE_WORK:-}" && -d "${JUDGE_WORK}" ]] && rm -rf "${JUDGE_WORK}"
    return "${code}"
}

# ---------------------------------------------------------------------------- steps

# judge_run_step <name> <timeout-seconds> <command...>
# Streams combined output to stdout and to $JUDGE_WORK/<name>.log, enforces a wall clock, and returns the
# command's status (124 on timeout).
#
# The child runs in the BACKGROUND and is awaited with `wait` on purpose: bash defers trap handlers until
# the current *foreground* command returns, so with a foreground child the five-second SIGTERM grace would
# expire before the trap ever ran.
judge_run_step() {
    local name=$1
    local budget=$2
    shift 2

    local log="${JUDGE_WORK}/${name}.log"
    local status=0

    timeout --signal=TERM --kill-after=10s "${budget}" "$@" >"${log}" 2>&1 &
    local child=$!

    # `wait` on a specific pid returns that pid's status, and is interruptible by traps.
    #
    # It must be `wait || status=$?`, never `if ! wait; then status=$?; fi`: inside that if-body $? is the
    # status of the *negation*, which is always 0, so every step failure would be silently swallowed and the
    # judge would report success for a submission that did not even compile.
    wait "${child}" || status=$?

    cat "${log}"
    return "${status}"
}

# judge_errors <logfile> - the interesting lines out of a restore/build/test log, for the diagnostic.
judge_errors() {
    grep -E 'error [A-Z]+[0-9]+|error :|: error|Unable to find package|NU[0-9]{4}' "$1" 2>/dev/null \
        | head -5 \
        | tr '\n' ' ' \
        || true
}

# ---------------------------------------------------------------------------- pipeline

# swap_dir - graft the competitor's folder over the placeholder in the image.
swap_dir() {
    judge_require JUDGE_SWAP_SRC
    : "${JUDGE_SWAP_DEST:=${JUDGE_APP_DIR}/${JUDGE_SWAP_SRC}}"

    local source="${COMPETITOR_DIRECTORY}/${JUDGE_SWAP_SRC}"

    # Validate BEFORE deleting anything: the spec's order wipes the destination first, so a missing source
    # leaves the image in a state where nothing can run and the error is about the wrong thing.
    if [[ ! -d "${source}" ]]; then
        judge_fatal "${JUDGE_EXIT_SWAP}" "swap" \
            "the submission has no ${JUDGE_SWAP_SRC} folder. Keep the folder name exactly as shipped."
    fi

    if [[ -z "$(find "${source}" -name '*.cs' -print -quit 2>/dev/null)" ]]; then
        judge_fatal "${JUDGE_EXIT_SWAP}" "swap" \
            "${JUDGE_SWAP_SRC} contains no .cs files."
    fi

    mkdir -p "${JUDGE_SWAP_DEST}"

    # Stashed BEFORE the placeholder is emptied, which is the only moment it still exists. Restored after the
    # submission is extracted, because a build file is executable code: MSBuild runs whatever a <Target> says.
    local authored_project stash=""
    authored_project=$(find "${JUDGE_SWAP_DEST}" -maxdepth 1 -name '*.csproj' -print -quit)
    if [[ -n "${authored_project}" ]]; then
        stash="${JUDGE_WORK}/authored-$(basename "${authored_project}")"
        cp "${authored_project}" "${stash}"
    else
        # Nothing to restore means nothing to protect, and the build would fail confusingly later. Fail here,
        # where the message can say what is actually wrong with the image.
        judge_fatal "${JUDGE_EXIT_CONFIG}" "swap" \
            "the image has no ${JUDGE_SWAP_SRC}/*.csproj to build against. This is an image bug: .dockerignore must keep the placeholder project file."
    fi

    find "${JUDGE_SWAP_DEST}" -mindepth 1 -delete

    # Copy sources only. Excluded on purpose:
    #   bin/ obj/            - stale build output, and a route to smuggling in prebuilt assemblies
    #   nuget.config         - would re-open nuget.org and defeat the offline restore entirely
    #   Directory.Build.*    - can rewrite compiler settings and disable the nullability contract checks
    #   global.json          - can pin a different SDK than the image provides
    #   *.csproj *.props     - see below; these are code, not configuration
    #   *.targets
    tar -C "${source}" -cf - \
        --exclude='bin' --exclude='obj' \
        --exclude='nuget.config' --exclude='NuGet.config' --exclude='NuGet.Config' \
        --exclude='Directory.Build.props' --exclude='Directory.Build.targets' \
        --exclude='Directory.Packages.props' --exclude='global.json' \
        --exclude='*.csproj' --exclude='*.props' --exclude='*.targets' \
        . | tar -C "${JUDGE_SWAP_DEST}" -xf -

    # Put the author's project file back, so the build graph is the one the session declared.
    #
    # This is the difference between a denylist and an allowlist, and it matters because a .csproj is not
    # configuration — it is executable code. MSBuild runs whatever a target says:
    #
    #   <Target Name="X" BeforeTargets="Build"><Exec Command="..."/></Target>
    #
    # and build_tests compiles the swapped folder BEFORE the hidden suite, as root. That gave a submission
    # arbitrary code execution inside the judge with the hidden tests writable next to it — a clean-looking
    # full pass that nothing server-side could distinguish from real work. Excluding by filename was never
    # going to hold: the previous list grew one entry at a time as each new vector was noticed.
    #
    # A competitor therefore cannot change their project file, which is already what they are told: "do not add
    # package references - the judge restores offline and a new reference fails the run."
    if [[ -n "${stash}" ]]; then
        # Tell the competitor their project file was ignored, rather than letting them discover it as an
        # unresolved type. Before the substitution, adding a package reference failed the run with exit 4 and a
        # message naming the cause; discarding it silently would trade a clear diagnostic for a confusing
        # compile error, so the diagnostic is emitted explicitly.
        local submitted
        submitted=$(find "${source}" -maxdepth 1 -name '*.csproj' -print -quit)
        if [[ -n "${submitted}" ]] && ! cmp -s "${submitted}" "${stash}"; then
            emit_marker_error "your $(basename "${submitted}") was ignored and the session's own project file was used instead. A project file is executable code, so the judge does not run yours - which also means added package references have no effect, because the restore is offline."
        fi

        rm -f "${JUDGE_SWAP_DEST}"/*.csproj
        cp "${stash}" "${JUDGE_SWAP_DEST}/$(basename "${authored_project}")"
    fi
}

# restore_offline - restore only from the baked feed and the warmed cache. This is what stops a submission
# from pulling a new package to do the work for it.
restore_offline() {
    judge_require JUDGE_SOLUTION

    cd "${JUDGE_APP_DIR}"

    local status=0
    judge_run_step restore "${JUDGE_TIMEOUT_SECONDS}" \
        dotnet restore "${JUDGE_SOLUTION}" \
        --source "${JUDGE_LOCAL_FEED}" \
        --source "${JUDGE_NUGET_CACHE}" || status=$?

    if (( status == 124 )); then
        judge_fatal "${JUDGE_EXIT_TIMEOUT}" "restore" \
            "restore exceeded ${JUDGE_TIMEOUT_SECONDS}s."
    fi

    if (( status != 0 )); then
        judge_fatal "${JUDGE_EXIT_RESTORE}" "restore" \
            "offline restore failed - a package reference that is not available in the image was probably added. $(judge_errors "${JUDGE_WORK}/restore.log")"
    fi
}

# build_tests - compile before running.
#
# This step exists because `dotnet test` returns 1 for BOTH red tests and a compile error. With
# JUDGE_FAIL_ON_RED=false the two are indistinguishable, so a submission that does not compile exits 0 and
# is recorded as Completed with no results. Separating the build is the only way a non-compiling
# submission can be reported as failed.
build_tests() {
    judge_require JUDGE_TEST_PROJECT

    cd "${JUDGE_APP_DIR}"

    local status=0
    judge_run_step build "${JUDGE_TIMEOUT_SECONDS}" \
        dotnet build "${JUDGE_TEST_PROJECT}" -c Release --no-restore || status=$?

    if (( status == 124 )); then
        judge_fatal "${JUDGE_EXIT_TIMEOUT}" "build" \
            "build exceeded ${JUDGE_TIMEOUT_SECONDS}s."
    fi

    if (( status != 0 )); then
        judge_fatal "${JUDGE_EXIT_BUILD}" "build" \
            "the submission does not compile. $(judge_errors "${JUDGE_WORK}/build.log")"
    fi
}

# run_tests [extra dotnet-test args...] - run the suite. Red tests are a result, not an error, unless
# JUDGE_FAIL_ON_RED=true: a white-box session wants partial credit for a partially working submission.
# judge_grant_test_paths - hand the competitor account write access to exactly what the test host needs.
#
# Everything else under /app - above all the hidden suite's sources and the compiled contracts - stays owned by
# root and read-only to them. That is the whole point: the step that runs their code must not be able to edit
# the tests grading it.
#
# LOG_DIRECTORY is included because the harness writes events.jsonl from inside their test process. That is a
# known limit rather than an oversight: a white-box mark is self-reported until the event stream moves out of
# their process, and no file permission fixes that.
judge_grant_test_paths() {
    local test_dir="${JUDGE_APP_DIR}/$(dirname "${JUDGE_TEST_PROJECT}")"

    mkdir -p "${JUDGE_APP_DIR}/TestResults" "${LOG_DIRECTORY}"
    chown -R "${JUDGE_TEST_USER}:${JUDGE_TEST_USER}" \
        "${JUDGE_APP_DIR}/TestResults" \
        "${LOG_DIRECTORY}" \
        "${test_dir}/bin" "${test_dir}/obj" 2>/dev/null || true

    # The swapped folder's build output too: the test host may touch it, and it is the competitor's own code.
    chown -R "${JUDGE_TEST_USER}:${JUDGE_TEST_USER}" \
        "${JUDGE_APP_DIR}/${JUDGE_SWAP_SRC}/bin" \
        "${JUDGE_APP_DIR}/${JUDGE_SWAP_SRC}/obj" 2>/dev/null || true

    # A writable NuGet cache and HOME, or the test host fails trying to create them under a root-owned home.
    export DOTNET_CLI_HOME="/home/${JUDGE_TEST_USER}"
    chown -R "${JUDGE_TEST_USER}:${JUDGE_TEST_USER}" "/home/${JUDGE_TEST_USER}" 2>/dev/null || true
}

# judge_verify_events_against_trx - cross-check the event stream against the test framework's own output.
#
# This is the answer to a hole that cannot be closed by permissions. The harness that writes events.jsonl runs
# INSIDE the competitor's test process, so anything it can write, they can write - including a full set of
# "passed" events for tests that never ran. Any key it held to sign them would be readable by them too. Self
# reporting from inside an untrusted process is unforgeable only if something outside corroborates it.
#
# The TRX is that something: VSTest writes it, one entry per test, independently of the harness. Forging a pass
# now means forging BOTH consistently. It is not a proof - a determined competitor owns the process and can
# attack the TRX as well - but it turns a silent fabrication into a detectable one, and it catches every
# careless attempt outright. An expert investigating a disputed mark has two independent records to compare.
judge_verify_events_against_trx() {
    local trx
    trx=$(find "${JUDGE_APP_DIR}" -name '*.trx' -newer "${JUDGE_STAGED_EVENTS}" 2>/dev/null | head -1)
    [[ -z "${trx}" ]] && trx=$(find "${JUDGE_APP_DIR}" -name '*.trx' 2>/dev/null | head -1)

    if [[ -z "${trx}" ]]; then
        emit_marker_error "no TRX file was produced, so the event stream could not be corroborated against the test framework's own output. Treat this run's results as unverified."
        return 0
    fi

    # Attributes on <Counters>, which VSTest writes from its own accounting rather than from the harness.
    local trx_total trx_passed trx_failed
    trx_total=$(grep -o 'total="[0-9]*"' "${trx}" | head -1 | grep -o '[0-9]*')
    trx_passed=$(grep -o 'passed="[0-9]*"' "${trx}" | head -1 | grep -o '[0-9]*')
    trx_failed=$(grep -o 'failed="[0-9]*"' "${trx}" | head -1 | grep -o '[0-9]*')

    local ev_passed ev_failed
    ev_passed=$(grep -c '"event":"finish-unit-test"[^}]*"outcome":"passed"' "${JUDGE_EVENT_FILE}" 2>/dev/null || true)
    ev_failed=$(grep -c '"event":"finish-unit-test"[^}]*"outcome":"failed"' "${JUDGE_EVENT_FILE}" 2>/dev/null || true)

    printf 'judge: corroboration - trx(total=%s passed=%s failed=%s) events(passed=%s failed=%s)\n' \
        "${trx_total:-?}" "${trx_passed:-?}" "${trx_failed:-?}" "${ev_passed:-0}" "${ev_failed:-0}"

    # Compared on the pass count, which is the number a forgery would inflate. An event stream reporting MORE
    # passes than VSTest counted is the signature of fabricated results.
    if [[ -n "${trx_passed}" ]] && (( ev_passed > trx_passed )); then
        emit_marker_error "RESULT INTEGRITY: the event stream reports ${ev_passed} passing tests but the test framework counted ${trx_passed}. These must agree; a higher event count means results were fabricated inside the test process. This run must be reviewed by an expert before its marks are used."
        printf 'judge: EVENT STREAM DOES NOT MATCH THE TRX - possible fabricated results\n' >&2

        if [[ "${JUDGE_FAIL_ON_INTEGRITY:-true}" == "true" ]]; then
            judge_fatal "${JUDGE_EXIT_INTEGRITY}" "verify" \
                "the event stream does not match the test framework's own results; refusing to report marks."
        fi
    fi

    return 0
}

run_tests() {
    judge_require JUDGE_TEST_PROJECT

    cd "${JUDGE_APP_DIR}"

    local status=0
    local -a runner=()

    # Dropped to an unprivileged account when one exists, which is every image built on judge-base. The guard
    # keeps a hand-rolled image that lacks the account working rather than failing confusingly.
    if [[ "${JUDGE_DROP_TEST_PRIVILEGES}" == "true" ]] \
        && id "${JUDGE_TEST_USER}" >/dev/null 2>&1 && [[ "$(id -u)" == "0" ]]; then
        judge_grant_test_paths
        runner=(setpriv --reuid "${JUDGE_TEST_USER}" --regid "${JUDGE_TEST_USER}" --init-groups)
        printf 'judge: running tests as %s\n' "${JUDGE_TEST_USER}"
    else
        printf 'judge: WARNING running tests as the container user; the hidden suite is writable by the submission\n' >&2
    fi

    judge_run_step test "${JUDGE_TIMEOUT_SECONDS}" \
        "${runner[@]}" dotnet test "${JUDGE_TEST_PROJECT}" -c Release --no-restore --no-build \
        --logger "console;verbosity=detailed" "$@" || status=$?

    if (( status == 124 )); then
        # The suite is already built and restored, so this is the submission's own fault - most often an
        # infinite loop in the code under test.
        judge_fatal "${JUDGE_EXIT_TIMEOUT}" "test" \
            "the test run exceeded ${JUDGE_TIMEOUT_SECONDS}s and was stopped - most likely an endless loop."
    fi

    if (( status != 0 )) && [[ "${JUDGE_FAIL_ON_RED}" == "true" ]]; then
        judge_fatal 1 "test" "tests failed and JUDGE_FAIL_ON_RED is true."
    fi

    return 0
}

# judge_assert_results - a cheap net for the failure class exit codes cannot catch: the run "succeeded" but
# produced no events at all (zero tests discovered, the harness never initialised, LOG_DIRECTORY ignored).
judge_assert_results() {
    if [[ ! -s "${JUDGE_EVENT_FILE}" ]]; then
        judge_fatal 1 "results" \
            "the test run produced no events. The suite may have discovered no tests, or the harness was not initialised."
    fi
}
