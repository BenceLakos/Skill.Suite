#!/usr/bin/env bash
# Fills local-nuget/ with everything the offline restore needs: this module's Contracts package, plus the
# judging packages it depends on. Run before any build or docker build.
#
#   ./pack-contracts.sh
#
# The judging packages (Skill.Suite.TestLog, .Xunit, .Marker) come from one of two places:
#   * a local checkout of the judging subtree - set JUDGING_ROOT, or let it be discovered
#   * a NuGet feed - set JUDGING_FEED to a source, for a session module that lives in its own repository
#
# Skill.Suite.Marker is only needed by the black-box image, which installs it as a dotnet tool from this feed
# at build time. It is packed regardless so that build stays offline-capable.

set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
readonly PACKAGES=(Skill.Suite.TestLog Skill.Suite.TestLog.Xunit Skill.Suite.Marker)

mkdir -p "${DIR}/local-nuget"

# Discover the judging checkout by walking up for the marker file, so this works whether the module sits
# inside the judging tree or a few directories away from it.
discover_judging_root() {
    local candidate="${DIR}"
    for _ in 1 2 3 4 5 6; do
        if [[ -f "${candidate}/global.json" && -d "${candidate}/Skill.Suite.TestLog" ]]; then
            printf '%s' "${candidate}"
            return 0
        fi
        candidate="$(dirname "${candidate}")"
    done
    return 1
}

JUDGING_ROOT=${JUDGING_ROOT:-$(discover_judging_root || true)}

fetch_from_feed() {
    local feed=$1 pkg
    for pkg in "${PACKAGES[@]}"; do
        echo "downloading ${pkg} from ${feed}"
        # `nuget install` is not guaranteed present; a restore into a throwaway project is, and it resolves
        # the same graph the image will.
        local work
        work=$(mktemp -d)
        (
            cd "${work}"
            dotnet new classlib -o probe --framework net9.0 >/dev/null
            cd probe
            dotnet add package "${pkg}" --version 1.0.0 --source "${feed}" \
                --package-directory "${work}/pkgs" >/dev/null
        )
        find "${work}/pkgs" -name "${pkg,,}.1.0.0.nupkg" -exec cp {} "${DIR}/local-nuget/" \; 2>/dev/null || true
        rm -rf "${work}"
    done
}

if [[ -n "${JUDGING_FEED:-}" ]]; then
    fetch_from_feed "${JUDGING_FEED}"
elif [[ -n "${JUDGING_ROOT}" ]]; then
    for pkg in "${PACKAGES[@]}"; do
        if [[ ! -f "${DIR}/local-nuget/${pkg}.1.0.0.nupkg" ]]; then
            echo "packing ${pkg} from ${JUDGING_ROOT}"
            # Packed from that directory so judging/global.json pins the SDK the competitors get.
            (cd "${JUDGING_ROOT}" && dotnet pack "${pkg}" -c Release -o "${DIR}/local-nuget" >/dev/null)
        fi
    done
else
    printf 'pack-contracts: cannot find the judging packages.\n' >&2
    printf '  Set JUDGING_ROOT=/path/to/judging, or JUDGING_FEED=<nuget source>.\n' >&2
    exit 1
fi

echo "packing Checkout.Rules.Contracts"
dotnet pack "${DIR}/Checkout.Rules.Contracts/Checkout.Rules.Contracts.csproj" \
    -c Release -o "${DIR}/local-nuget" >/dev/null

# Bust the global cache copy, or a re-pack of the same version is silently ignored and the image gets the
# previous contract.
rm -rf "${HOME}/.nuget/packages/checkout.rules.contracts"

ls -1 "${DIR}/local-nuget"/*.nupkg
