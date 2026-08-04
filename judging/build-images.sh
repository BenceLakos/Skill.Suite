#!/usr/bin/env bash
# build-images.sh [target...] - build every judge image on this machine.
#
#   judging/build-images.sh                    # everything
#   judging/build-images.sh base fibonacci     # just those two
#   judging/build-images.sh list               # what the targets are
#
# The platform runs `docker run` with no --pull, so a judge image has to exist on the host daemon before a
# session can use it. This is the local equivalent of the `images` and `verification` jobs in
# .github/workflows/judging-ci.yml, so a green run here means CI builds the same artifacts.
#
# Session modules are not built here - each one owns a `build-image.sh` that takes no session arguments,
# because its Dockerfile declares the judge contract as ENV. This script only locates them and runs it, so
# there is one way to build a session image and it is the same one CI and a session author use.
#
# Env:
#   NO_CACHE=1      pass --no-cache to every docker build
#   SKIP_TESTS=1    skip the per-image self-tests (faster; you lose the reason to trust the images)

set -euo pipefail

JUDGING="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly JUDGING
readonly BASE_TAG=skill-suite-judge-base:9.0

DOCKER_BUILD_FLAGS=()
[[ "${NO_CACHE:-0}" == "1" ]] && DOCKER_BUILD_FLAGS+=(--no-cache)

log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m ok\033[0m %s\n' "$*"; }
die()  { printf '\033[1;31mFAIL\033[0m %s\n' "$*" >&2; exit 1; }

BUILT=()

# ---------------------------------------------------------------------------- session modules

# build_module <module-dir> [whitebox|blackbox|both]
#
# Delegates to the module's own script. Everything session-specific - the solution, the swapped folder, the
# test project - lives in that module's Dockerfile as ENV, so there is nothing to pass and nothing a caller
# can get wrong.
build_module() {
    local dir=$1 kind=${2:-whitebox}

    [[ -d "${dir}" ]] || die "no such session module: ${dir}"
    [[ -x "${dir}/build-image.sh" ]] || die "${dir} has no executable build-image.sh"

    log "${dir##*/} (${kind})"
    (
        cd "${dir}"
        ./pack-contracts.sh >/dev/null
        BASE_IMAGE="${BASE_TAG}" NO_CACHE="${NO_CACHE:-0}" SKIP_TESTS="${SKIP_TESTS:-0}" \
            ./build-image.sh "${kind}"
    ) | sed 's/^/   /'
}

# ---------------------------------------------------------------------------- targets

target_base() {
    log "building ${BASE_TAG}"
    docker build "${DOCKER_BUILD_FLAGS[@]}" -t "${BASE_TAG}" "${JUDGING}/docker/judge-base" >/dev/null

    # judge-lib.sh carries real logic - JSON escaping without jq, event staging, the never-empty rule - that
    # no xUnit test can reach. This is its test suite, and every other image inherits the bug if it fails.
    if [[ "${SKIP_TESTS:-0}" != "1" ]]; then
        docker run --rm --entrypoint /usr/local/bin/judge-selftest.sh "${BASE_TAG}" >/dev/null \
            || die "judge-base self-test failed; run it directly to see which check broke"
        ok "judge-base self-test passed"
    fi

    BUILT+=("${BASE_TAG}")
}

target_smoke() {
    log "building skill-suite-judge-smoke:1"
    # Context is judging/, not the smoke folder: the image compiles against Skill.Suite.TestLog.
    docker build "${DOCKER_BUILD_FLAGS[@]}" -t skill-suite-judge-smoke:1 \
        -f "${JUDGING}/docker/judge-smoke/Dockerfile" "${JUDGING}" >/dev/null

    BUILT+=(skill-suite-judge-smoke:1)
}

target_fixture() {
    build_module "${JUDGING}/verification/fixtures/judge-fixture" whitebox
    BUILT+=(judge-fixture:1)
}

target_fixture_blackbox() {
    build_module "${JUDGING}/verification/fixtures/judge-fixture" blackbox
    BUILT+=(judge-fixture-blackbox:1)
}

target_fibonacci() {
    build_module "${JUDGING}/samples/fibonacci-session" whitebox
    BUILT+=(fibonacci-judge:1)
}

# ---------------------------------------------------------------------------- dispatch

readonly ALL_TARGETS=(base smoke fixture fixture-blackbox fibonacci)

usage() {
    cat <<'EOF'
usage: build-images.sh [target...]

  base              skill-suite-judge-base:9.0    the shared shell contract, plus its self-test
  smoke             skill-suite-judge-smoke:1     fixed event stream; proves platform wiring only
  fixture           judge-fixture:1               verification kit persona matrix
  fixture-blackbox  judge-fixture-blackbox:1      black-box pipeline (slow: Stryker at run time)
  fibonacci         fibonacci-judge:1             the worked example session

  all               every target above (the default)
  list              print this

A new session module is not added here. Generate it, then run its own build-image.sh:
  cd judging && dotnet new install ./templates/session
  dotnet new skillsuite-session -n My.Session --judge-base skill-suite-judge-base:9.0

Env: NO_CACHE=1  SKIP_TESTS=1
EOF
}

main() {
    local requested=("$@")
    (( ${#requested[@]} == 0 )) && requested=(all)

    case "${requested[0]}" in
        list|-h|--help|help) usage; return 0 ;;
        all) requested=("${ALL_TARGETS[@]}") ;;
    esac

    # Everything is FROM base, so build it first whether or not it was asked for, and never twice.
    local target wants_base=0
    for target in "${requested[@]}"; do
        [[ "${target}" == base ]] && wants_base=1
    done
    if (( wants_base == 0 )); then
        docker image inspect "${BASE_TAG}" >/dev/null 2>&1 \
            || { log "${BASE_TAG} is missing; building it first"; target_base; }
    fi

    for target in "${requested[@]}"; do
        case "${target}" in
            base)             target_base ;;
            smoke)            target_smoke ;;
            fixture)          target_fixture ;;
            fixture-blackbox) target_fixture_blackbox ;;
            fibonacci)        target_fibonacci ;;
            *) printf 'unknown target: %s\n\n' "${target}" >&2; usage >&2; exit 2 ;;
        esac
    done

    printf '\n'
    ok "built ${#BUILT[@]} image(s)"
    local image
    for image in "${BUILT[@]}"; do
        printf '     %-32s %s\n' "${image}" \
            "$(docker image inspect --format '{{.Size}}' "${image}" 2>/dev/null \
                | awk '{printf "%.0f MB", $1/1024/1024}')"
    done
}

main "$@"
