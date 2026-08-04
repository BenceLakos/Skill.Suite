#!/usr/bin/env bash
# Fills local-nuget/ with everything the offline restore needs: this module's Contracts package, plus the
# judging packages it depends on. Run before any build or docker build.

set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
JUDGING="$(cd "${DIR}/../../.." && pwd)"

mkdir -p "${DIR}/local-nuget"

# The judging packages are consumed as packages, not project references, so the fixture exercises the same
# restore path a real session module does.
# Skill.Suite.Marker is only needed by the black-box image, which installs it as a dotnet tool from this
# feed at build time. Packing it here keeps that build offline-capable before the tool is published.
for pkg in Skill.Suite.TestLog Skill.Suite.TestLog.Xunit Skill.Suite.Marker; do
    if [[ ! -f "${DIR}/local-nuget/${pkg}.1.0.0.nupkg" ]]; then
        echo "packing ${pkg}"
        (cd "${JUDGING}" && dotnet pack "${pkg}" -c Release -o "${DIR}/local-nuget" >/dev/null)
    fi
done

echo "packing JudgeFixture.Contracts"
dotnet pack "${DIR}/JudgeFixture.Contracts/JudgeFixture.Contracts.csproj" \
    -c Release -o "${DIR}/local-nuget" >/dev/null

# Bust the global cache copy, or a re-pack of the same version is silently ignored.
rm -rf "${HOME}/.nuget/packages/judgefixture.contracts"

ls -1 "${DIR}/local-nuget"/*.nupkg
