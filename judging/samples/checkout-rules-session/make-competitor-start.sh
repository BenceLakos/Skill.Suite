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
# private service field, the fixture constructor - is kept, and nothing is added in their place. Either way
# the type's own doc comment is replaced with one addressed to the competitor.
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
    whitebox) SWAPPED="Checkout.Rules.Services" ;;
    blackbox) SWAPPED="Checkout.Rules.UnitTests" ;;
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

# ---------------------------------------------------------------------------- make it self-contained
#
# The generated project carries a PackageReference to this session's Contracts package, which a competitor
# cannot resolve from a bare folder. Ship the feed with it: a nuget.config and the packages it names.
#
# Safe to include, because swap_dir excludes nuget.config and Directory.Build.* when it copies a submission
# into the image — so these help the competitor locally and cannot influence how they are judged.
KIT="${DIR}/competitor-start"

[[ -d "${DIR}/local-nuget" ]] || { printf 'make-competitor-start: run ./pack-contracts.sh first.\n' >&2; exit 1; }

mkdir -p "${KIT}/local-nuget"
cp "${DIR}/local-nuget"/*.nupkg "${KIT}/local-nuget/" 2>/dev/null || true

cat >"${KIT}/nuget.config" <<'NUGET'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!-- Everything this project needs is in local-nuget/, so it restores with no network. -->
    <clear />
    <add key="session-local" value="./local-nuget" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
NUGET

# ---------------------------------------------------------------------------- verify
#
# Built WHERE THE COMPETITOR WILL BUILD IT — a copy of the kit alone, nothing else. The previous version copied
# the kit back into the full module, so it was really re-testing the module and printed ok for a kit that
# failed NU1101/MSB1003 in a competitor's hands. Verifying the wrong artifact is worse than not verifying.
printf '\nverifying the starter kit compiles the way a competitor will\n'
WORK=$(mktemp -d)
trap 'rm -rf "${WORK}"' EXIT
cp -R "${KIT}/." "${WORK}/"
find "${WORK}" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true

if dotnet build "${WORK}/${SWAPPED}" -c Release --nologo >"${WORK}/build.log" 2>&1; then
    printf 'ok - a competitor can restore and build this offline\n'
else
    printf 'FAIL - the starter kit does not compile in isolation:\n\n' >&2
    grep -E 'error|Error' "${WORK}/build.log" | head -10 >&2 || tail -20 "${WORK}/build.log" >&2
    printf '\nfull log: %s/build.log\n' "${WORK}" >&2
    trap - EXIT
    exit 1
fi
