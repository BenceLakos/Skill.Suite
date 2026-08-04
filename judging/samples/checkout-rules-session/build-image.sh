#!/usr/bin/env bash
# build-image.sh [whitebox|blackbox|both] - build this session's judge image(s).
#
#   ./pack-contracts.sh          # once, and again whenever Contracts changes
#   ./build-image.sh             # white-box (competitor implements the services)
#   ./build-image.sh blackbox    # black-box (competitor writes the tests)
#   ./build-image.sh both
#
# Takes no session-specific arguments. Everything the judge needs to know - the solution, which folder gets
# swapped, which project holds the tests - is declared as ENV in the Dockerfiles, because those are facts
# about this module rather than choices the caller makes. Only the environment-dependent values are settable:
#
#   BASE_IMAGE=skill-suite-judge-base:9.0   the locally built base instead of the published one
#   TAG=1.0.1                               overrides the tag
#   IMAGE_NAME=my-session-judge             overrides the derived image name
#   REGISTRY=<host>/<owner>                 prefixes the image name for pushing
#   NO_CACHE=1                              docker build --no-cache
#
# This file is yours once generated. Pinning IMAGE_NAME or TAG by editing the defaults below is expected -
# a module whose image name is referenced elsewhere (an acceptance matrix, a session record) should not have
# it change because the project was renamed.
#
# Session images contain hidden tests (white-box) or the reference implementation in source form (black-box).
# Push them ONLY to the private registry, and never with a mutable tag: the platform runs `docker run` with no
# --pull, so a re-pushed tag leaves it judging with a stale local image.

set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "${DIR}"

readonly SESSION=Checkout.Rules
TAG=${TAG:-1.0.0}
BASE_IMAGE=${BASE_IMAGE:-skill-suite-judge-base:9.0}
REGISTRY=${REGISTRY:-}

# Lowercase: a docker reference must be, and a .NET project name usually is not.
SLUG=$(printf '%s' "${SESSION}" | tr '[:upper:]' '[:lower:]' | tr '.' '-')

# Named independently rather than one derived from the other by suffixing, so pinning either to a name an
# acceptance matrix or a session record already refers to does not distort the other.
IMAGE_NAME=${IMAGE_NAME:-${SLUG}-judge}
BLACKBOX_IMAGE_NAME=${BLACKBOX_IMAGE_NAME:-${SLUG}-blackbox-judge}

name_for() {
    local base=${IMAGE_NAME}
    [[ "$1" == blackbox ]] && base=${BLACKBOX_IMAGE_NAME}
    printf '%s%s:%s' "${REGISTRY:+${REGISTRY}/}" "${base}" "${TAG}"
}

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()  { printf '\033[1;32m ok\033[0m %s\n' "$*"; }
die() { printf '\033[1;31mFAIL\033[0m %s\n' "$*" >&2; exit 1; }

flags=()
[[ "${NO_CACHE:-0}" == "1" ]] && flags+=(--no-cache)

[[ -d local-nuget ]] || die "local-nuget/ is missing. Run ./pack-contracts.sh first, or the offline restore fails NU1101."

build() {
    local kind=$1 dockerfile=$2 image
    image=$(name_for "${kind}")

    log "building ${kind}: ${image}"
    docker build "${flags[@]}" -f "${dockerfile}" -t "${image}" \
        --build-arg BASE_IMAGE="${BASE_IMAGE}" .

    # The Dockerfile already refuses to build if the answer key leaked, but that check runs in the build
    # stage. This one asserts it about the FINAL image - the thing someone can actually pull.
    if [[ "${kind}" == whitebox ]]; then
        local leaked
        leaked=$(docker run --rm --entrypoint sh "${image}" \
            -c "find /app/${SESSION}.Services -name '*.cs' 2>/dev/null" || true)
        [[ -z "${leaked}" ]] || die "${image} ships the reference implementation: ${leaked}"
        ok "answer-key firewall holds in ${image}"
    fi

    ok "${image}  ($(docker image inspect --format '{{.Size}}' "${image}" \
        | awk '{printf "%.0f MB", $1/1024/1024}'))"
}

case "${1:-whitebox}" in
    whitebox) build whitebox Dockerfile ;;
    blackbox) build blackbox Dockerfile.blackbox ;;
    both)     build whitebox Dockerfile; build blackbox Dockerfile.blackbox ;;
    *) printf 'usage: %s [whitebox|blackbox|both]\n' "$(basename "$0")" >&2; exit 2 ;;
esac

printf '\n'
printf 'Run one submission the way the platform does:\n'
printf '  judging/tools/judge-run.sh %s <path-to-submission>\n' "$(name_for "${1:-whitebox}")"
