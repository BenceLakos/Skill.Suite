#!/usr/bin/env bash
# Fills local-nuget/ with everything the offline restore needs: this module's Contracts package, plus the
# judging packages it depends on. Run before any build or docker build.

set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
JUDGING="$(cd "${DIR}/../.." && pwd)"

mkdir -p "${DIR}/local-nuget"

# The judging packages are consumed as packages, not project references, so this module exercises the same
# restore path a real session module does.
for pkg in Skill.Suite.TestLog Skill.Suite.TestLog.Xunit; do
    if [[ ! -f "${DIR}/local-nuget/${pkg}.1.0.0.nupkg" ]]; then
        echo "packing ${pkg}"
        (cd "${JUDGING}" && dotnet pack "${pkg}" -c Release -o "${DIR}/local-nuget" >/dev/null)
    fi
done

echo "packing Fibonacci.Contracts"
dotnet pack "${DIR}/Fibonacci.Contracts/Fibonacci.Contracts.csproj" \
    -c Release -o "${DIR}/local-nuget" >/dev/null

# Bust the global cache copy, or a re-pack of the same version is silently ignored.
rm -rf "${HOME}/.nuget/packages/fibonacci.contracts"

ls -1 "${DIR}/local-nuget"/*.nupkg
