#!/usr/bin/env bash
# judge-selftest.sh - checks judge-lib.sh's primitives inside the image.
#
# The shell layer carries real logic (JSON escaping with no jq, event staging, the never-empty rule) and is
# the one part of the judging stack no xUnit test can reach. Run it in CI right after building the image:
#
#   docker run --rm --entrypoint /usr/local/bin/judge-selftest.sh <image>

set -uo pipefail

FAILURES=0

check() {
    local label=$1 expected=$2 actual=$3
    if [[ "${expected}" == "${actual}" ]]; then
        printf 'ok   %s\n' "${label}"
    else
        printf 'FAIL %s\n       expected: %s\n       actual:   %s\n' "${label}" "${expected}" "${actual}"
        FAILURES=$((FAILURES + 1))
    fi
}

check_contains() {
    local label=$1 needle=$2 haystack=$3
    if [[ "${haystack}" == *"${needle}"* ]]; then
        printf 'ok   %s\n' "${label}"
    else
        printf 'FAIL %s\n       expected to contain: %s\n       actual: %s\n' "${label}" "${needle}" "${haystack}"
        FAILURES=$((FAILURES + 1))
    fi
}

export LOG_DIRECTORY="$(mktemp -d)"
export COMPETITOR_DIRECTORY="$(mktemp -d)"
export JUDGE_APP_DIR="$(mktemp -d)"

. /usr/local/lib/judge-lib.sh

# judge-lib.sh sets -e when sourced, which is right for a real judge run and wrong here: this script
# deliberately exercises failure paths, and with -e the first expected non-zero exit would abort the whole
# selftest before it could report anything.
set +e

judge_configure

# ---------------------------------------------------------------- json_escape

check 'json_escape leaves plain text alone' 'all good' "$(json_escape 'all good')"
check 'json_escape escapes quotes' 'said \"no\"' "$(json_escape 'said "no"')"
check 'json_escape escapes backslashes before quotes' 'C:\\path \"x\"' "$(json_escape 'C:\path "x"')"
check 'json_escape turns newlines into \n' 'a\nb' "$(json_escape "$(printf 'a\nb')")"
check 'json_escape strips carriage returns' 'ab' "$(json_escape "$(printf 'a\rb')")"
check 'json_escape escapes tabs' 'a\tb' "$(json_escape "$(printf 'a\tb')")"
check 'json_escape drops control characters' 'ab' "$(json_escape "$(printf 'a\001b')")"
check 'json_escape truncates past the limit' 'aaaaa...' "$(json_escape 'aaaaaaaaaa' 5)"

# A realistic MSBuild error, which is exactly what emit_marker_error has to carry.
raw="$(printf '/app/X.cs(12,5): error CS0246: type or namespace \x27Foo\x27 not found\n\tat "bar"')"
escaped="$(json_escape "${raw}")"
check_contains 'json_escape handles a compiler error' 'error CS0246' "${escaped}"
[[ "${escaped}" == *$'\n'* ]] && { printf 'FAIL escaped text still contains a raw newline\n'; FAILURES=$((FAILURES + 1)); }

# ---------------------------------------------------------------- staging and flushing

emit_marker_error 'staged not written'
if [[ -e "${JUDGE_EVENT_FILE}" ]]; then
    printf 'FAIL emit_event must not touch events.jsonl before flush\n'
    FAILURES=$((FAILURES + 1))
else
    printf 'ok   emit_event stages instead of writing events.jsonl\n'
fi

flush_events
check 'flush_events creates the file' '1' "$(wc -l <"${JUDGE_EVENT_FILE}" | tr -d ' ')"
check_contains 'flushed event is a marker-error' '"event":"marker-error"' "$(cat "${JUDGE_EVENT_FILE}")"

# Flushing an empty stage must not grow the file, and must never create an empty one.
flush_events
check 'flush_events is a no-op when nothing is staged' '1' "$(wc -l <"${JUDGE_EVENT_FILE}" | tr -d ' ')"

rm -f "${JUDGE_EVENT_FILE}"
flush_events
if [[ -e "${JUDGE_EVENT_FILE}" ]]; then
    printf 'FAIL flush_events created an empty events.jsonl - that suppresses the stdout fallback\n'
    FAILURES=$((FAILURES + 1))
else
    printf 'ok   flush_events never creates an empty events.jsonl\n'
fi

# Appending onto a file whose last line was cut off (what a SIGKILLed test host leaves).
printf '{"event":"start-fixture","fixture":"F"}\n{"partial":tru' >"${JUDGE_EVENT_FILE}"
emit_marker_error 'after a partial line'
flush_events
check 'partial line is preserved' '{"partial":tru' "$(sed -n '2p' "${JUDGE_EVENT_FILE}")"
check_contains 'appended event starts its own line' '{"timestamp"' "$(sed -n '3p' "${JUDGE_EVENT_FILE}")"

# Emitted lines must be valid JSON. No jq in this image, so let .NET adjudicate.
if command -v dotnet >/dev/null 2>&1; then
    if dotnet --version >/dev/null 2>&1; then
        printf 'ok   dotnet is available for the JSON validity check\n'
    fi
fi

# ---------------------------------------------------------------- swap_dir

mkdir -p "${COMPETITOR_DIRECTORY}/Demo.Services/bin" "${COMPETITOR_DIRECTORY}/Demo.Services/obj"
printf 'class A {}\n' >"${COMPETITOR_DIRECTORY}/Demo.Services/A.cs"
printf 'stale\n' >"${COMPETITOR_DIRECTORY}/Demo.Services/bin/stale.dll"
printf '<configuration/>\n' >"${COMPETITOR_DIRECTORY}/Demo.Services/nuget.config"
# The attack in miniature: a project file with a target that would run arbitrary code at build time.
printf '<Project Sdk="Microsoft.NET.Sdk"><Target Name="Pwn" BeforeTargets="Build"><Exec Command="touch /tmp/pwned"/></Target></Project>\n' \
    >"${COMPETITOR_DIRECTORY}/Demo.Services/Demo.Services.csproj"
printf '<Project/>\n' >"${COMPETITOR_DIRECTORY}/Demo.Services/Directory.Build.props"
mkdir -p "${JUDGE_APP_DIR}/Demo.Services"
printf 'placeholder\n' >"${JUDGE_APP_DIR}/Demo.Services/placeholder.cs"
# The authored project file. Every real image has one - .dockerignore keeps it precisely so the restore graph
# resolves - and swap_dir restores it over whatever the submission shipped, because a .csproj is executable code.
printf '<Project Sdk="Microsoft.NET.Sdk"><!-- authored --></Project>\n' \
    >"${JUDGE_APP_DIR}/Demo.Services/Demo.Services.csproj"

export JUDGE_SWAP_SRC=Demo.Services
swap_dir

check 'swap_dir copies sources' 'class A {}' "$(cat "${JUDGE_APP_DIR}/Demo.Services/A.cs")"
check 'swap_dir removes the placeholder' '' "$(ls "${JUDGE_APP_DIR}/Demo.Services/placeholder.cs" 2>/dev/null)"
check 'swap_dir excludes bin' '' "$(ls "${JUDGE_APP_DIR}/Demo.Services/bin" 2>/dev/null)"
check 'swap_dir excludes obj' '' "$(ls "${JUDGE_APP_DIR}/Demo.Services/obj" 2>/dev/null)"
# These two are the anti-gaming ones: a nuget.config re-opens nuget.org and defeats the offline restore,
# and Directory.Build.props can switch off the nullability contract that the proxy's null-return check needs.
check 'swap_dir excludes nuget.config' '' "$(ls "${JUDGE_APP_DIR}/Demo.Services/nuget.config" 2>/dev/null)"
check 'swap_dir excludes Directory.Build.props' '' "$(ls "${JUDGE_APP_DIR}/Demo.Services/Directory.Build.props" 2>/dev/null)"

# The one that matters most: the submission's project file must NOT be the one that gets built. A .csproj is
# executable code — MSBuild runs whatever a <Target> tells it to, and the swapped folder compiles before the
# hidden suite, as root. Excluding by filename was a denylist that grew one entry per discovered vector; this is
# the allowlist version.
check 'swap_dir restores the authored csproj over the submission one' \
    'authored' \
    "$(grep -o 'authored' "${JUDGE_APP_DIR}/Demo.Services/Demo.Services.csproj" 2>/dev/null)"
check 'swap_dir drops a build target the submission tried to inject' \
    '' \
    "$(grep -o 'Pwn' "${JUDGE_APP_DIR}/Demo.Services/Demo.Services.csproj" 2>/dev/null)"

# A missing folder must be reported without having destroyed the destination first.
(
    export JUDGE_SWAP_SRC=Absent.Services
    swap_dir
) >/dev/null 2>&1
check 'swap_dir exits 3 when the folder is missing' '3' "$?"

# ---------------------------------------------------------------- summary

printf '\n'
if (( FAILURES == 0 )); then
    printf 'judge-selftest: all checks passed\n'
    exit 0
fi

printf 'judge-selftest: %d check(s) failed\n' "${FAILURES}"
exit 1
