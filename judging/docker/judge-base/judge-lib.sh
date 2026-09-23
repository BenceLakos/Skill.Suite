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
#   * There is NO timeout of the platform's own, and the worker is serial - one hung container stalls every
#     other competitor. Hence the wall clocks here.
#   * --memory, --cpus and --pids-limit ARE applied when configured, and the shipped compose configures them
#     (4g / 2 / 512). A step can therefore be killed by something other than its own wall clock.
#   * The container runs `--cap-drop ALL` with SETUID, SETGID, CHOWN, DAC_OVERRIDE, FOWNER and KILL added
#     back. KILL is there because the test step drops to an unprivileged account and root's own `timeout`
#     needs it to signal across a uid boundary - it was once missing, and the wall clock silently stopped
#     working. A wall clock can still expire without the step dying for other reasons; see
#     judge_step_timed_out, which is what classifies that correctly.
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
    # Per-fixture coverage runs the suite once per TEST CLASS, filtered. Each run is a fraction of the whole
    # suite, so the budget is per class and small; one class that hangs is skipped rather than fatal.
    : "${JUDGE_FIXTURE_COVERAGE_TIMEOUT_SECONDS:=120}"
    # The only JUDGE_* variable read INSIDE the test host: the xUnit harness runs each call into the
    # submission under this budget, so an endless loop fails the one test case it hangs in and the rest of
    # the suite still gets marked. Exported rather than merely defaulted, because a value assigned here is a
    # shell variable and the test process would never see it. 0 disables it and leaves every hang to the
    # suite wall clock above.
    : "${JUDGE_CALL_TIMEOUT_SECONDS:=10}"
    export JUDGE_CALL_TIMEOUT_SECONDS
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
    JUDGE_TEST_MANIFEST="${JUDGE_WORK}/test-manifest"
    JUDGE_ASSET_SEAL="${JUDGE_WORK}/graded-assets.sha256"
    : >"${JUDGE_STAGED_EVENTS}"

    readonly JUDGE_WORK JUDGE_STAGED_EVENTS JUDGE_EVENT_FILE JUDGE_TEST_MANIFEST JUDGE_ASSET_SEAL

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

# Wall clock bookkeeping for the step that ran last, read by judge_step_timed_out. An exit status on its own
# cannot tell a wall clock apart from a kill that came from somewhere else, so the elapsed time is kept too.
JUDGE_STEP_BUDGET=0
JUDGE_STEP_ELAPSED=0

# judge_run_step <name> <timeout-seconds> <command...>
# Streams combined output to stdout and to $JUDGE_WORK/<name>.log, enforces a wall clock, and returns the
# command's status. Ask judge_step_timed_out about that status rather than comparing it to 124 by hand.
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
    local started=${SECONDS}

    timeout --signal=TERM --kill-after=10s "${budget}" "$@" >"${log}" 2>&1 &
    local child=$!

    # `wait` on a specific pid returns that pid's status, and is interruptible by traps.
    #
    # It must be `wait || status=$?`, never `if ! wait; then status=$?; fi`: inside that if-body $? is the
    # status of the *negation*, which is always 0, so every step failure would be silently swallowed and the
    # judge would report success for a submission that did not even compile.
    wait "${child}" || status=$?

    judge_record_step_clock "${budget}" "${started}"

    cat "${log}"
    return "${status}"
}

# judge_record_step_clock <budget-seconds> <started-SECONDS> - the same bookkeeping for a caller that drives
# `timeout` itself. Per-fixture coverage and the mutation step both do, because each needs its own log
# handling and its own kill-after grace, and both still have to classify a wall clock correctly.
judge_record_step_clock() {
    JUDGE_STEP_BUDGET=$1
    JUDGE_STEP_ELAPSED=$(( SECONDS - $2 ))
}

# judge_step_timed_out <status> - did the step that just finished hit its wall clock?
#
# `timeout` returns 124 only when the child died from the SIGTERM it sent. That is NOT the status when the
# signal never arrives, and for a while in this container it never did: the platform's cap list left out
# CAP_KILL while run_tests hands the test step to an unprivileged account, so root's own `timeout` got EPERM
# signalling it. After --kill-after, `timeout` SIGKILLs its process group, of which it is itself a member,
# and the only process it managed to kill was itself. `wait` then reported 137 while the test host ran on
# until the container was torn down.
#
# Testing only for 124 let an endless loop through as a completed run: the timeout diagnostic and
# JUDGE_EXIT_TIMEOUT were skipped, JUDGE_FAIL_ON_RED=false made the step look successful, and the run was
# left carrying "no TRX file was produced ... treat this run's results as unverified" - true, but a symptom,
# and it pointed whoever read it at the harness instead of at the submission's own loop.
#
# DockerRunArguments.Build now adds KILL back, so the signal lands and 124 is the ordinary status again.
# This check stays as defence in depth rather than being reverted: a signal can fail to reach a child for
# reasons other than that one capability, and it costs nothing to keep recognising the shape.
#
# A signal status alone is not enough to conclude a timeout, because a child the OOM killer picked also
# reports 137. The budget is what separates them: a step reaches its wall clock only after using all of it.
judge_step_timed_out() {
    local status=$1

    if (( status == 124 )); then
        return 0
    fi

    # 128+SIGKILL and 128+SIGTERM, i.e. the step was signalled rather than choosing its own exit status.
    if (( status == 137 || status == 143 )) && (( JUDGE_STEP_ELAPSED >= JUDGE_STEP_BUDGET )); then
        return 0
    fi

    return 1
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

    if judge_step_timed_out "${status}"; then
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

    if judge_step_timed_out "${status}"; then
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

    # chmod as well as chown, because LOG_DIRECTORY and TestResults are not always plain directories in the
    # image: the platform mounts the log directory as a subpath of a docker volume, where a chown does not take.
    # The local judge-run.sh uses a host bind mount, which is permissive - so this failed only under the real
    # platform, with the tests running but their events unwritable. Exactly the kind of difference a local
    # harness hides.
    chmod 0777 "${LOG_DIRECTORY}" "${JUDGE_APP_DIR}/TestResults" 2>/dev/null || true
}

# judge_test_user_can_write - confirm the unprivileged account can actually write what the run depends on.
#
# The guard exists because the failure it prevents is the worst kind: the tests run, the harness cannot write
# events.jsonl, and the platform reports a submission that "produced no events" - a competitor scored zero by a
# hardening step. Better to run the tests privileged and say so than to silently lose a mark.
judge_test_user_can_write() {
    local probe="${LOG_DIRECTORY}/.write-probe"

    if setpriv --reuid "${JUDGE_TEST_USER}" --regid "${JUDGE_TEST_USER}" --init-groups \
        sh -c "touch '${probe}' 2>/dev/null"; then
        rm -f "${probe}"
        return 0
    fi

    return 1
}

# judge_seal_graded_assets / judge_verify_graded_assets - detect tampering with what the run is measured against.
#
# This is the black-box counterpart to running the test step unprivileged, and it works where that did not. In a
# black-box session the competitor supplies the TESTS and the reference implementation is baked in, in source
# form, because Stryker has to mutate it. Their tests therefore run in the same container as the very code their
# coverage and kill rate are computed from - and as the marking map that turns those into a score.
#
# Editing either is a complete break: make the implementation trivial and every mutant dies; widen the ramps in
# marking-map.json and a shallow suite scores like a thorough one. Both are far cheaper attacks than forging an
# event stream, and neither leaves a trace in the results.
#
# Sealed before the test step and verified after, by root, from a digest held in the root-only work directory.
judge_seal_graded_assets() {
    local -a targets=()
    [[ -n "${JUDGE_SERVICES_SRC:-}" && -d "${JUDGE_APP_DIR}/${JUDGE_SERVICES_SRC}" ]] \
        && targets+=("${JUDGE_APP_DIR}/${JUDGE_SERVICES_SRC}")
    [[ -n "${JUDGE_MARKING_MAP:-}" && -f "${JUDGE_MARKING_MAP}" ]] && targets+=("${JUDGE_MARKING_MAP}")

    (( ${#targets[@]} == 0 )) && return 0

    # bin/ and obj/ excluded: restore and build legitimately rewrite generated sources and project.assets.json
    # in there, so including them made every honest run fail the seal. Only authored files are sealed.
    find "${targets[@]}" -type f \
        -not -path '*/obj/*' -not -path '*/bin/*' \
        \( -name '*.cs' -o -name '*.json' \) -exec sha256sum {} + 2>/dev/null \
        | sort >"${JUDGE_ASSET_SEAL}" || true
    chmod 0600 "${JUDGE_ASSET_SEAL}" 2>/dev/null || true

    printf 'judge: sealed %s graded asset file(s) before the submission ran\n' \
        "$(wc -l <"${JUDGE_ASSET_SEAL}" | tr -d ' ')"
}

judge_verify_graded_assets() {
    [[ -s "${JUDGE_ASSET_SEAL}" ]] || return 0

    if sha256sum --quiet --check "${JUDGE_ASSET_SEAL}" >/dev/null 2>&1; then
        printf 'judge: graded assets unchanged\n'
        return 0
    fi

    local changed
    changed=$(sha256sum --check "${JUDGE_ASSET_SEAL}" 2>/dev/null | grep -v ': OK$' | cut -d: -f1 | tr '\n' ' ')

    emit_marker_error "RESULT INTEGRITY: the code or marking configuration this submission is measured against was modified while its tests ran: ${changed}. The resulting score is meaningless and must not be used."
    printf 'judge: GRADED ASSETS WERE MODIFIED: %s\n' "${changed}" >&2

    if [[ "${JUDGE_FAIL_ON_INTEGRITY:-true}" == "true" ]]; then
        judge_fatal "${JUDGE_EXIT_INTEGRITY}" "verify" \
            "the code being measured was modified during the run."
    fi

    return 0
}

# judge_capture_test_manifest - record which tests legitimately exist, before any competitor code is present.
#
# This is the one piece of corroboration a competitor genuinely cannot reach. It is taken from the hidden suite's
# own sources, inside the image, BEFORE swap_dir puts their folder anywhere near it — so at the moment it is
# written there is no competitor code in the container at all. It lands in JUDGE_WORK, a root-owned mktemp
# directory, and the test step runs as an unprivileged account that cannot write there.
#
# Counting alone could never close the invented-test vector: a forged stream can claim any number of passes for
# tests that never existed, and comparing totals only catches it when the numbers happen to disagree. A manifest
# of real test names catches it structurally.
judge_capture_test_manifest() {
    local test_dir="${JUDGE_APP_DIR}/$(dirname "${JUDGE_TEST_PROJECT}")"

    # Method names of xunit test cases. Deliberately a plain grep rather than a compiled reflection pass: this
    # runs before restore, must not execute a line of the suite, and only needs the identifiers.
    # Deliberately simple: every `public ... Name(` on a line, reduced to Name. An earlier version anchored the
    # identifier to end-of-line and silently matched nothing, leaving the check inert and passing - which is the
    # failure mode to guard against here, because an integrity check that matches nothing looks identical to one
    # that found nothing wrong.
    grep -rh 'public' "${test_dir}" --include='*.cs' 2>/dev/null \
        | sed -nE 's/.*[[:space:]]([A-Za-z0-9_]+)[[:space:]]*\(.*/\1/p' \
        | sort -u >"${JUDGE_TEST_MANIFEST}" || true

    chmod 0600 "${JUDGE_TEST_MANIFEST}" 2>/dev/null || true
    printf 'judge: recorded %s candidate test name(s) before the submission was introduced\n' \
        "$(wc -l <"${JUDGE_TEST_MANIFEST}" | tr -d ' ')"
}

# judge_verify_events_against_manifest - reject results attributed to tests that do not exist.
judge_verify_events_against_manifest() {
    [[ -s "${JUDGE_TEST_MANIFEST}" ]] || return 0

    # Compared on the METHOD NAME only. A [Theory] case reports as `Name(index: 10, expected: 55)` - xunit puts
    # the InlineData arguments in the display name - so matching the whole string flagged every parameterised
    # test as invented and failed honest submissions as fraudulent. The fixture suite is all [Fact], so the
    # persona matrix passed 8/8 and gave a false all-clear; only the Fibonacci sample, which uses [Theory],
    # exposed it. An integrity check that rejects real work is far worse than one that misses a forgery.
    local invented
    invented=$(grep -oE '"event":"finish-unit-test","fixture":"[^"]*","test":"[^"]*"' "${JUDGE_EVENT_FILE}" 2>/dev/null \
        | grep -oE '"test":"[^"]*"' | cut -d'"' -f4 \
        | sed 's/(.*//' \
        | sort -u \
        | grep -vxFf "${JUDGE_TEST_MANIFEST}" || true)

    [[ -z "${invented}" ]] && return 0

    local names
    names=$(printf '%s' "${invented}" | tr '\n' ' ')
    emit_marker_error "RESULT INTEGRITY: the event stream reports results for test(s) that do not exist in this session's suite: ${names}. Those results were fabricated inside the test process."
    printf 'judge: EVENT STREAM NAMES TESTS THAT DO NOT EXIST: %s\n' "${names}" >&2

    if [[ "${JUDGE_FAIL_ON_INTEGRITY:-true}" == "true" ]]; then
        judge_fatal "${JUDGE_EXIT_INTEGRITY}" "verify" \
            "results were reported for tests that do not exist in this session's suite."
    fi

    return 0
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
    #
    # `|| true` on every one of these, and it is load-bearing rather than defensive. Under `set -e` a plain
    # assignment whose command substitution fails terminates the shell, and with `pipefail` a grep that matches
    # nothing fails the whole pipeline - so an unparseable TRX killed the judge HERE, before the guard below
    # could emit its diagnostic. The run then exited 1 with no marker-error and the platform recorded a
    # submission whose tests had all passed as Failed. An empty value has to reach the guard to be reported.
    local trx_total trx_passed trx_failed
    trx_total=$(grep -o 'total="[0-9]*"' "${trx}" | head -1 | grep -o '[0-9]*') || true
    trx_passed=$(grep -o 'passed="[0-9]*"' "${trx}" | head -1 | grep -o '[0-9]*') || true
    trx_failed=$(grep -o 'failed="[0-9]*"' "${trx}" | head -1 | grep -o '[0-9]*') || true

    local ev_passed ev_failed
    ev_passed=$(grep -c '"event":"finish-unit-test"[^}]*"outcome":"passed"' "${JUDGE_EVENT_FILE}" 2>/dev/null || true)
    ev_failed=$(grep -c '"event":"finish-unit-test"[^}]*"outcome":"failed"' "${JUDGE_EVENT_FILE}" 2>/dev/null || true)

    printf 'judge: corroboration - trx(total=%s passed=%s failed=%s) events(passed=%s failed=%s)\n' \
        "${trx_total:-?}" "${trx_passed:-?}" "${trx_failed:-?}" "${ev_passed:-0}" "${ev_failed:-0}"

    # Fail loudly when a TRX exists but its counters do not parse - a truncated file, a collector that wrote no
    # <Counters>, or a future SDK that changes the shape. The condition below is guarded on trx_passed being
    # non-empty, so without this branch an unparseable TRX made the corroboration silently approve the run while
    # printing `passed=?`. That is the third check in this harness that could quietly stop checking; a run whose
    # results nothing corroborated must say so rather than read as verified.
    if [[ -z "${trx_passed}" ]]; then
        emit_marker_error "the test framework's TRX output was found but its result counters could not be read, so the event stream could not be corroborated against it. Treat this run's results as unverified."
        printf 'judge: TRX FOUND BUT COUNTERS UNREADABLE - results not corroborated\n' >&2
        return 0
    fi

    # Compared on the pass count, which is the number a forgery would inflate. An event stream reporting MORE
    # passes than VSTest counted is the signature of fabricated results.
    if (( ev_passed > trx_passed )); then
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

        if judge_test_user_can_write; then
            # No --reset-env, deliberately: setpriv passes the environment straight through, which is how
            # LOG_DIRECTORY and JUDGE_CALL_TIMEOUT_SECONDS reach the test host. Adding it would leave the
            # harness writing to stdout and running every call unguarded, with nothing saying so.
            runner=(setpriv --reuid "${JUDGE_TEST_USER}" --regid "${JUDGE_TEST_USER}" --init-groups)
            printf 'judge: running tests as %s\n' "${JUDGE_TEST_USER}"
        else
            printf 'judge: WARNING %s cannot write %s, so tests run privileged. The hidden suite is writable by the submission for this run.\n' \
                "${JUDGE_TEST_USER}" "${LOG_DIRECTORY}" >&2
            emit_marker_error "the judge could not drop privileges for the test step because ${LOG_DIRECTORY} is not writable by the unprivileged account. Results are still produced, but this run had weaker isolation than intended - worth an operator's attention."
        fi
    else
        printf 'judge: WARNING running tests as the container user; the hidden suite is writable by the submission\n' >&2
    fi

    judge_run_step test "${JUDGE_TIMEOUT_SECONDS}" \
        "${runner[@]}" dotnet test "${JUDGE_TEST_PROJECT}" -c Release --no-restore --no-build \
        --logger "console;verbosity=detailed" "$@" || status=$?

    if judge_step_timed_out "${status}"; then
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

# judge_test_classes <trx-file>... - the distinct fully-qualified test classes a run executed.
#
# grep and sed rather than a parser, for the same reason json_escape is parameter expansion: the sdk:9.0 image
# ships neither jq, python3 nor xmllint, and adding one to the base image to read a single attribute would be a
# large dependency for a small job.
#
# The TRX is the right source rather than the event stream: it is written by the runner, outside the
# submission's process, so a suite cannot name classes here that it did not actually run. The trailing
# `s/,.*$//` strips the assembly qualification some runners append ("Ns.Type, Assembly, Version=...").
judge_test_classes() {
    grep -ho 'className="[^"]*"' "$@" 2>/dev/null \
        | sed -e 's/^className="//' -e 's/"$//' -e 's/,.*$//' -e 's/[[:space:]]*$//' \
        | grep -v '^$' \
        | sort -u
}

# judge_fixture_coverage <destination-dir> <trx-file>... - one filtered coverage run per test class.
#
# What it produces is exactly the layout `skill-marker score --fixture-coverage` reads: a directory per test
# class, named by the class's SIMPLE name so it matches the fixture names already in the event stream, holding
# that class's own cobertura report.
#
#     <destination-dir>/CalculatorTests/<guid>/coverage.cobertura.xml
#
# Three things here are load bearing:
#
#   1. LOG_DIRECTORY is REDIRECTED to a throwaway directory for the whole loop, exactly as the mutation step
#      does it. Every one of these test hosts starts the xUnit harness, which opens $LOG_DIRECTORY/events.jsonl
#      with FileMode.Create - so left alone, the first filtered run truncates the real event stream and the
#      submission looks as though it executed the tests of one class and nothing else. The stream is copied and
#      compared afterwards as well, because that failure mode has already survived one round of defences.
#
#   2. The filter carries a TRAILING DOT: `FullyQualifiedName~Ns.Class.` matches that class's test methods and
#      not `Ns.ClassExtra`, which a bare contains-match would also claim.
#
#   3. A failing class run is logged and skipped. These are measurements shown beside a submission, never the
#      submission's verdict, so one class whose run times out must not cost the competitor their mark. `dotnet
#      test` also exits non-zero merely because tests failed, which here is not even an error.
judge_fixture_coverage() {
    judge_require JUDGE_TEST_PROJECT

    local dest=$1
    shift

    # Without this, judge_test_classes would call grep with no file arguments and it would sit reading stdin
    # forever - a hung container, from a step that is only ever decoration.
    if (( $# == 0 )); then
        printf 'judge: no TRX to read test classes from; skipping per-fixture coverage\n' >&2
        return 0
    fi

    local classes
    classes=$(judge_test_classes "$@")

    if [[ -z "${classes}" ]]; then
        printf 'judge: no test classes found in the TRX; skipping per-fixture coverage\n' >&2
        return 0
    fi

    cd "${JUDGE_APP_DIR}"
    mkdir -p "${dest}"

    cp "${JUDGE_EVENT_FILE}" "${JUDGE_WORK}/events.before-fixture-coverage.jsonl" 2>/dev/null || true

    local real_log_directory="${LOG_DIRECTORY}"
    export LOG_DIRECTORY="${JUDGE_WORK}/fixture-coverage-logs"
    mkdir -p "${LOG_DIRECTORY}"

    local measured=0
    local skipped=0
    local class simple out log status child started

    while IFS= read -r class; do
        [[ -n "${class}" ]] || continue

        # The simple name, which is what FixtureScope<T> puts in start-fixture and therefore what the marker
        # joins on. A nested class contributes its innermost name, again matching typeof(T).Name.
        simple="${class##*.}"
        simple="${simple##*+}"

        out="${dest}/${simple}"
        log="${JUDGE_WORK}/fixture-coverage-${simple}.log"
        mkdir -p "${out}"

        status=0
        started=${SECONDS}
        timeout --signal=TERM --kill-after=10s "${JUDGE_FIXTURE_COVERAGE_TIMEOUT_SECONDS}" \
            dotnet test "${JUDGE_TEST_PROJECT}" -c Release --no-restore --no-build \
            --filter "FullyQualifiedName~${class}." \
            --collect:"XPlat Code Coverage" \
            --results-directory "${out}" \
            -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura \
            >"${log}" 2>&1 &
        child=$!
        wait "${child}" || status=$?
        judge_record_step_clock "${JUDGE_FIXTURE_COVERAGE_TIMEOUT_SECONDS}" "${started}"

        if find "${out}" -name '*.cobertura.xml' -type f -print -quit 2>/dev/null | grep -q .; then
            measured=$((measured + 1))
        else
            skipped=$((skipped + 1))
            if judge_step_timed_out "${status}"; then
                printf 'judge: per-fixture coverage for %s exceeded %ss; skipped\n' \
                    "${simple}" "${JUDGE_FIXTURE_COVERAGE_TIMEOUT_SECONDS}" >&2
            else
                printf 'judge: per-fixture coverage for %s produced no report (exit %s): %s\n' \
                    "${simple}" "${status}" "$(judge_errors "${log}")" >&2
            fi
        fi
    done <<<"${classes}"

    export LOG_DIRECTORY="${real_log_directory}"

    if [[ -f "${JUDGE_WORK}/events.before-fixture-coverage.jsonl" ]] \
        && ! cmp -s "${JUDGE_EVENT_FILE}" "${JUDGE_WORK}/events.before-fixture-coverage.jsonl"; then
        printf 'judge: events.jsonl was modified during per-fixture coverage; restoring the pre-step stream\n' >&2
        cp "${JUDGE_WORK}/events.before-fixture-coverage.jsonl" "${JUDGE_EVENT_FILE}"
    fi

    printf 'judge: per-fixture coverage measured %s class(es), skipped %s\n' "${measured}" "${skipped}"
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
