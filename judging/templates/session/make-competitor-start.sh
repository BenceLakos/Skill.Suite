#!/usr/bin/env bash
# make-competitor-start.sh - regenerate competitor-start/, the repository competitors are handed.
#
#   ./make-competitor-start.sh              # derive both projects from this module
#   ./make-competitor-start.sh maintenance  # keep the authored competitor-start/SkillSuite.Session.Services sources
#
# ---------------------------------------------------------------------------------------------------
# WHAT THE KIT IS
#
# A complete, openable solution - not a bare project folder:
#
#   .gitignore                        build output and editor state; local-nuget/ deliberately NOT ignored
#   SkillSuite.Session.sln            the two projects below; Contracts arrives as a package, not a project
#   SkillSuite.Session.Services/      csproj verbatim, every public member stubbed to throw
#   SkillSuite.Session.UnitTests/     csproj verbatim, the reference suite emptied to its harness wiring
#   local-nuget/ + nuget.config       the offline feed, so the whole thing restores with no network
#
# ONE kit serves BOTH session types, deliberately. The judge copies only the folder it swaps out of a
# submission - SkillSuite.Session.Services for white-box, SkillSuite.Session.UnitTests for black-box - and
# ignores everything else in the repository, including the other project, the solution and the csproj files.
# So the extra project cannot affect a mark, while a competitor who receives only half a solution cannot
# open it, cannot build it, and in an implementation session has nowhere to put the tests they write to
# check their own work. Handing out both is free and strictly better.
#
# The kit is DERIVED, never edited: a hand-maintained starter kit drifts the moment the implementation moves,
# and the drift stays invisible until a competitor cannot compile what they were given. Anything you edit
# inside competitor-start/ is lost on the next run - except in maintenance mode, below.
#
# MAINTENANCE MODE
#
# A maintenance session does not start from nothing: what competitors receive is the system they inherited -
# an authored implementation carrying the defects the brief files as tickets. That artifact cannot be derived
# from the reference, so in maintenance mode the sources under competitor-start/SkillSuite.Session.Services/
# are AUTHORED and this script leaves them alone. Everything around them is still regenerated - the Services
# csproj (it must stay identical to the one the judge builds against), the test project, the solution, the
# .gitignore and the feed - and the kit is still compiled in isolation before this script reports success.
#
# Always run ./pack-contracts.sh first: this script copies local-nuget/*.nupkg without re-packing them, so an
# unpacked contract change ships stale.
#
# The generator is the `skill-starter` tool. Like pack-contracts.sh, it is resolved from a local judging
# checkout when there is one, or from a feed when this module lives in its own repository:
#
#   JUDGING_ROOT=/path/to/judging   run it straight from source
#   JUDGING_FEED=<nuget source>     install it as a dotnet tool from a feed
# ---------------------------------------------------------------------------------------------------

set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"

readonly SOLUTION="SkillSuite.Session"
readonly SERVICES="SkillSuite.Session.Services"
readonly UNITTESTS="SkillSuite.Session.UnitTests"
readonly KIT="${DIR}/competitor-start"

MODE=${1:-default}
case "${MODE}" in
    default | maintenance) ;;
    whitebox | blackbox)
        # Accepted so an older invocation - a script, a habit, a stale README - does not fail outright.
        printf '%s is no longer a mode: one kit serves both session types. Generating it.\n' "${MODE}"
        MODE=default
        ;;
    *)
        printf 'usage: %s [maintenance]\n' "$(basename "$0")" >&2
        exit 2
        ;;
esac
readonly MODE

# ---------------------------------------------------------------------------- resolve skill-starter

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
STARTER_TOOL=""

if [[ -z "${JUDGING_ROOT}" ]]; then
    if [[ -n "${JUDGING_FEED:-}" ]]; then
        TOOLS="${DIR}/.tools"
        if [[ ! -x "${TOOLS}/skill-starter" ]]; then
            # Through a config file rather than --add-source: a plain-http feed (a Gitea on the venue LAN) needs
            # allowInsecureConnections on the source, or NuGet on .NET 9 refuses it with NU1302.
            FEED_CONFIG=$(mktemp)
            INSECURE=""
            [[ "${JUDGING_FEED}" == http://* ]] && INSECURE=' allowInsecureConnections="true"'
            cat >"${FEED_CONFIG}" <<NUGET
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="judging" value="${JUDGING_FEED}"${INSECURE} />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
NUGET
            dotnet tool install --tool-path "${TOOLS}" --configfile "${FEED_CONFIG}" \
                Skill.Suite.StarterKit --version 1.0.0 >/dev/null
            rm -f "${FEED_CONFIG}"
        fi
        STARTER_TOOL="${TOOLS}/skill-starter"
    else
        printf 'make-competitor-start: cannot find skill-starter.\n' >&2
        printf '  Set JUDGING_ROOT=/path/to/judging, or JUDGING_FEED=<nuget source>.\n' >&2
        exit 1
    fi
fi

# skill_starter <source-project-dir> <output-dir> <whitebox|blackbox>
skill_starter() {
    if [[ -n "${STARTER_TOOL}" ]]; then
        "${STARTER_TOOL}" "$1" "$2" --kind "$3"
    else
        # -v quiet so the tool's own report is the only output.
        (cd "${JUDGING_ROOT}" && dotnet run --project Skill.Suite.StarterKit -c Release -v quiet -- \
            "$1" "$2" --kind "$3")
    fi
}

# ---------------------------------------------------------------------------- build the kit, then swap it in
#
# Staged in a temporary directory and moved into place at the end, so a failure halfway through leaves the
# previous kit intact rather than a half-written one - which in maintenance mode would mean authored sources
# deleted by a run that then failed.

STAGE=$(mktemp -d)
trap 'rm -rf "${STAGE}"' EXIT

if [[ "${MODE}" == "maintenance" ]]; then
    # The authored baseline is the deliverable. Refuse to run rather than silently produce an empty kit.
    AUTHORED=$(find "${KIT}/${SERVICES}" -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' 2>/dev/null | wc -l | tr -d ' ')
    if [[ "${AUTHORED}" == "0" ]]; then
        printf 'make-competitor-start: competitor-start/%s holds no sources.\n' "${SERVICES}" >&2
        printf '  In maintenance mode the inherited implementation is authored there, not generated,\n' >&2
        printf '  so there is nothing to keep. Author it first, or run without "maintenance".\n' >&2
        exit 1
    fi
    printf 'maintenance mode: keeping the %s authored source file(s) in competitor-start/%s\n' \
        "${AUTHORED}" "${SERVICES}"

    mkdir -p "${STAGE}/${SERVICES}"
    cp -R "${KIT}/${SERVICES}/." "${STAGE}/${SERVICES}/"
    find "${STAGE}/${SERVICES}" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true
    # The csproj IS derived - it must stay identical to the one the judge builds the swapped folder with.
    rm -f "${STAGE}/${SERVICES}"/*.csproj
    cp "${DIR}/${SERVICES}/${SERVICES}.csproj" "${STAGE}/${SERVICES}/"
else
    skill_starter "${DIR}/${SERVICES}" "${STAGE}/${SERVICES}" whitebox
fi

skill_starter "${DIR}/${UNITTESTS}" "${STAGE}/${UNITTESTS}" blackbox

# ---------------------------------------------------------------------------- .gitignore
#
# Competitors work in a git repository seeded from this folder, and without one every `git status` is buried
# under build output. local-nuget/ is deliberately absent from the list: those packages are how the kit
# restores offline, and a competitor who never commits them loses the feed the moment they clone elsewhere.
cat >"${STAGE}/.gitignore" <<'GITIGNORE'
bin/
obj/
TestResults/
.vs/
.idea/
.vscode/
*.user
.DS_Store
GITIGNORE

# ---------------------------------------------------------------------------- solution
#
# Generated with the SDK rather than written out here: a hand-written .sln means hand-written project GUIDs,
# and one duplicated GUID is a solution that opens with a project missing and no useful error.
dotnet new sln -n "${SOLUTION}" -o "${STAGE}" --force >/dev/null
dotnet sln "${STAGE}/${SOLUTION}.sln" add \
    "${STAGE}/${SERVICES}/${SERVICES}.csproj" \
    "${STAGE}/${UNITTESTS}/${UNITTESTS}.csproj" >/dev/null

# ---------------------------------------------------------------------------- make it self-contained
#
# Both projects carry a PackageReference to this session's Contracts package, which a competitor cannot
# resolve from a bare folder. Ship the feed with it: a nuget.config and the packages it names.
#
# Safe to include, because swap_dir excludes nuget.config and Directory.Build.* when it copies a submission
# into the image - so these help the competitor locally and cannot influence how they are judged.
[[ -d "${DIR}/local-nuget" ]] || { printf 'make-competitor-start: run ./pack-contracts.sh first.\n' >&2; exit 1; }

mkdir -p "${STAGE}/local-nuget"
cp "${DIR}/local-nuget"/*.nupkg "${STAGE}/local-nuget/" 2>/dev/null || true

cat >"${STAGE}/nuget.config" <<'NUGET'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!-- Everything this solution needs is in local-nuget/, so it restores with no network. -->
    <clear />
    <add key="session-local" value="./local-nuget" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
NUGET

rm -rf "${KIT}"
mkdir -p "${KIT}"
cp -R "${STAGE}/." "${KIT}/"

# ---------------------------------------------------------------------------- verify
#
# Built WHERE THE COMPETITOR WILL BUILD IT - a copy of the kit alone, nothing else. An earlier version copied
# the kit back into the full module, so it was really re-testing the module and printed ok for a kit that
# failed NU1101/MSB1003 in a competitor's hands. Verifying the wrong artifact is worse than not verifying.
#
# The whole solution, so the test project's reference to the stubbed one is checked too - and with
# local-nuget as the ONLY package source. The shipped nuget.config lists nuget.org as a fallback for
# competitors who are online; leaving it in here would let a machine with internet, or a warm global cache,
# print ok for a kit that fails NU1101 on the venue LAN. pack-contracts.sh vendors the whole closure for this.
printf '\nverifying the starter kit compiles the way a competitor will, offline\n'
WORK=$(mktemp -d)
trap 'rm -rf "${STAGE}" "${WORK}"' EXIT
cp -R "${KIT}/." "${WORK}/"
find "${WORK}" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true
cat >"${WORK}/nuget.config" <<'NUGET'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="session-local" value="./local-nuget" />
  </packageSources>
</configuration>
NUGET

if NUGET_PACKAGES="${WORK}/.packages" dotnet build "${WORK}/${SOLUTION}.sln" -c Release --nologo >"${WORK}/build.log" 2>&1; then
    printf 'ok - a competitor can restore and build this offline\n'
else
    printf 'FAIL - the starter kit does not compile in isolation:\n\n' >&2
    grep -E 'error|Error' "${WORK}/build.log" | head -10 >&2 || tail -20 "${WORK}/build.log" >&2
    printf '\nfull log: %s/build.log\n' "${WORK}" >&2
    trap - EXIT
    rm -rf "${STAGE}"
    exit 1
fi
