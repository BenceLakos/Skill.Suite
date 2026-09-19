#!/usr/bin/env bash
# Fills local-nuget/ with everything the offline restore needs: this module's Contracts package, the judging
# packages it depends on, and the whole package closure of the test project (xunit, the test SDK, coverlet),
# so the competitor kit restores with no network. Run before any build or docker build.
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

# Writes a nuget.config naming the feed and nuget.org. NuGet on .NET 9 refuses a plain-http source (NU1302)
# unless the source carries allowInsecureConnections, and there is no command-line switch for that - so a
# feed served over http, which is what a Gitea on a venue LAN is, has to come through a config file rather
# than --source. nuget.org stays in because the judging packages depend on xunit and friends, which only
# live there; the probe restore resolves the whole graph even though only the four packages are kept.
write_feed_config() {
    local feed=$1 target=$2 insecure=""
    [[ "${feed}" == http://* ]] && insecure=' allowInsecureConnections="true"'
    cat >"${target}" <<NUGET
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="judging" value="${feed}"${insecure} />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
NUGET
}

fetch_from_feed() {
    local feed=$1 pkg work
    work=$(mktemp -d)
    write_feed_config "${feed}" "${work}/nuget.config"

    # PackageDownload rather than `dotnet add package`: two of the four are dotnet tools, and a PackageReference
    # to a tool package is refused outright (NU1212). PackageDownload fetches any package type without adding
    # a reference, in one restore for all of them, and drops the .nupkg files into the package directory.
    {
        printf '<Project Sdk="Microsoft.NET.Sdk">\n'
        printf '  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>\n'
        printf '  <ItemGroup>\n'
        for pkg in "${PACKAGES[@]}"; do
            printf '    <PackageDownload Include="%s" Version="[1.0.0]" />\n' "${pkg}"
        done
        printf '  </ItemGroup>\n</Project>\n'
    } >"${work}/probe.csproj"

    echo "downloading ${PACKAGES[*]} from ${feed}"
    # The CLI reports restore errors on stdout, so the output is kept and shown only on failure; a feed that
    # is missing one package must stop here, not surface later as NU1101 inside the image.
    if ! dotnet restore "${work}/probe.csproj" --packages "${work}/pkgs" >"${work}/restore.log" 2>&1; then
        cat "${work}/restore.log" >&2
        printf 'pack-contracts: could not download the judging packages from %s\n' "${feed}" >&2
        rm -rf "${work}"
        exit 1
    fi

    for pkg in "${PACKAGES[@]}"; do
        find "${work}/pkgs" -name "${pkg,,}.1.0.0.nupkg" -exec cp {} "${DIR}/local-nuget/" \;
        [[ -f "${DIR}/local-nuget/${pkg,,}.1.0.0.nupkg" ]] \
            || { printf 'pack-contracts: %s 1.0.0 was not downloaded from %s\n' "${pkg}" "${feed}" >&2; rm -rf "${work}"; exit 1; }
    done
    rm -rf "${work}"
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

echo "packing SkillSuite.Session.Contracts"
dotnet pack "${DIR}/SkillSuite.Session.Contracts/SkillSuite.Session.Contracts.csproj" \
    -c Release -o "${DIR}/local-nuget" >/dev/null

# Bust the global cache copy, or a re-pack of the same version is silently ignored and the image gets the
# previous contract.
rm -rf "${HOME}/.nuget/packages/skillsuite.session.contracts"

# ---------------------------------------------------------------------------- the rest of the closure
#
# The competitor kit is a whole solution, test project included, and that project pulls xunit, the test SDK
# and coverlet - none of which is a judging package. A venue machine without internet cannot restore them
# from nuget.org, so everything the two projects need is vendored here too: a restore of the reference
# projects into a scratch package directory downloads every .nupkg in the graph, and those are copied in.
# The judge is unaffected either way - it restores from the cache warmed in the image - but the kit's own
# verification build runs against local-nuget alone, which is what proves a competitor can build offline.
echo "vendoring the test project's package closure"
CLOSURE=$(mktemp -d)
if ! dotnet restore "${DIR}/SkillSuite.Session.UnitTests/SkillSuite.Session.UnitTests.csproj" \
        --packages "${CLOSURE}" >"${CLOSURE}/restore.log" 2>&1; then
    cat "${CLOSURE}/restore.log" >&2
    printf 'pack-contracts: could not restore the test project to collect its packages\n' >&2
    rm -rf "${CLOSURE}"
    exit 1
fi
find "${CLOSURE}" -name '*.nupkg' -not -name '*.snupkg' -exec cp -n {} "${DIR}/local-nuget/" \;
rm -rf "${CLOSURE}"

ls -1 "${DIR}/local-nuget"/*.nupkg
