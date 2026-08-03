#!/usr/bin/env bash
# make-competitor-start.sh - regenerate the folder competitors are handed, from the reference implementation.
#
#   ./make-competitor-start.sh              # white-box: strip the reference implementation
#   ./make-competitor-start.sh blackbox     # black-box: empty the reference test suite
#
# Run it whenever the contract or the reference implementation changes, and before handing anything out. The
# output is DERIVED, never edited: a hand-maintained starter kit drifts the moment the implementation moves,
# and the drift stays invisible until a competitor cannot compile what they were given.
#
# The two modes are inverses, because the two session types are. White-box: the competitor implements the
# services, so the .csproj is copied verbatim and every public member keeps its exact declaration with a
# NotImplementedException body, while non-public members and non-const fields are removed. Black-box: the
# competitor writes the tests, so the reference suite's methods are removed while the harness wiring - the
# private service field, the fixture constructor - is kept, plus a commented-out example. Either way the type's
# own doc comment is replaced with one addressed to the competitor.
#
# The generator is the `skill-starter` tool. Like pack-contracts.sh, it is resolved from a local judging
# checkout when there is one, or from a feed when this module lives in its own repository:
#
#   JUDGING_ROOT=/path/to/judging   run it straight from source
#   JUDGING_FEED=<nuget source>     install it as a dotnet tool from a feed

set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
KIND=${1:-whitebox}
case "${KIND}" in
    whitebox) SWAPPED="Fibonacci.Services" ;;
    blackbox) SWAPPED="Fibonacci.UnitTests" ;;
    *) printf 'usage: %s [whitebox|blackbox]\n' "$(basename "$0")" >&2; exit 2 ;;
esac
readonly KIND SWAPPED

# Whichever folder the judge swaps at run time is the one the competitor has to be handed.
readonly SOURCE="${DIR}/${SWAPPED}"
readonly OUTPUT="${DIR}/competitor-start/${SWAPPED}"

discover_judging_root() {
    local candidate="${DIR}"
    for _ in 1 2 3 4 5 6; do
        if [[ -f "${candidate}/global.json" && -d "${candidate}/Skill.Suite.StarterKit" ]]; then
            printf '%s' "${candidate}"
            return 0
        fi
        candidate="$(dirname "${candidate}")"
    done
    return 1
}

JUDGING_ROOT=${JUDGING_ROOT:-$(discover_judging_root || true)}

if [[ -n "${JUDGING_ROOT}" ]]; then
    # -v quiet so the tool's own report is the only output.
    (cd "${JUDGING_ROOT}" && dotnet run --project Skill.Suite.StarterKit -c Release -v quiet -- \
        "${SOURCE}" "${OUTPUT}" --kind "${KIND}")
elif [[ -n "${JUDGING_FEED:-}" ]]; then
    TOOLS="${DIR}/.tools"
    if [[ ! -x "${TOOLS}/skill-starter" ]]; then
        dotnet tool install --tool-path "${TOOLS}" --add-source "${JUDGING_FEED}" \
            Skill.Suite.StarterKit --version 1.0.0 >/dev/null
    fi
    "${TOOLS}/skill-starter" "${SOURCE}" "${OUTPUT}" --kind "${KIND}"
else
    printf 'make-competitor-start: cannot find skill-starter.\n' >&2
    printf '  Set JUDGING_ROOT=/path/to/judging, or JUDGING_FEED=<nuget source>.\n' >&2
    exit 1
fi

# The starter kit is what a competitor compiles first, so it has to compile. A skeleton that does not build is
# worse than no skeleton: it costs every competitor the same confused ten minutes.
printf '\nverifying the starter kit compiles\n'
WORK=$(mktemp -d)
trap 'rm -rf "${WORK}"' EXIT
cp -R "${DIR}/." "${WORK}/"
rm -rf "${WORK}/${SWAPPED}"
cp -R "${OUTPUT}" "${WORK}/${SWAPPED}"
# Build output from the original location is copied along with everything else, and its absolute paths no
# longer resolve here. Clearing it is cheaper than reasoning about which stale artifact bit.
find "${WORK}" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true

if dotnet build "${WORK}/Fibonacci.sln" -c Release --nologo >"${WORK}/build.log" 2>&1; then
    printf 'ok - the generated starter kit builds against the current contract\n'
else
    printf 'FAIL - the generated starter kit does not compile:\n\n' >&2
    grep -E 'error|Error' "${WORK}/build.log" | head -10 >&2 || tail -20 "${WORK}/build.log" >&2
    printf '\nfull log: %s/build.log\n' "${WORK}" >&2
    trap - EXIT
    exit 1
fi
