#!/usr/bin/env bash
# Shared helpers for the end-to-end kit. Sourced, not executed.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
readonly REPO_ROOT

# Service names and ports come from infra/compose.yaml and compose.yaml; keep them in one place.
readonly PG_CONTAINER=skill-suite-postgres
readonly GITEA_CONTAINER=skill-suite-gitea
readonly APP_CONTAINER=skill-suite
readonly GITEA_URL=http://localhost:3000
readonly APP_URL=http://localhost:8080

# The app reaches Gitea over the docker network, not via localhost. RepositoryUrlRewriter replaces the
# scheme/host/port of whatever the webhook advertises with Webhook__GitInternalBaseUrl.
readonly GITEA_INTERNAL=http://gitea:3000

# Throwaway local credentials. Generated per run rather than checked in, so nothing here is a real secret and
# there is no committed password to leak or to be reused by accident.
GITEA_ADMIN_USER=${GITEA_ADMIN_USER:-e2eadmin}
GITEA_ADMIN_PASSWORD=${GITEA_ADMIN_PASSWORD:-$(head -c 18 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 20)e2E1!}
GITEA_ADMIN_EMAIL=${GITEA_ADMIN_EMAIL:-e2eadmin@example.invalid}

log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m ok\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m  !\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31mFAIL\033[0m %s\n' "$*" >&2; exit 1; }

# wait_for <description> <timeout-seconds> <command...>
wait_for() {
    local what=$1 timeout=$2
    shift 2
    local waited=0
    until "$@" >/dev/null 2>&1; do
        if (( waited >= timeout )); then
            die "timed out after ${timeout}s waiting for ${what}"
        fi
        sleep 2
        waited=$((waited + 2))
    done
    ok "${what}"
}

# psql <sql> - runs SQL against the skillsuite database as a single statement.
psql_exec() {
    docker exec -i "${PG_CONTAINER}" psql -U skillsuite -d skillsuite -v ON_ERROR_STOP=1 -tAc "$1"
}

# gitea_api <method> <path> [json] - authenticated Gitea API call as the E2E admin.
gitea_api() {
    local method=$1 path=$2 body=${3:-}
    if [[ -n "${body}" ]]; then
        curl -sS -u "${GITEA_ADMIN_USER}:${GITEA_ADMIN_PASSWORD}" \
            -X "${method}" -H 'Content-Type: application/json' \
            -d "${body}" "${GITEA_URL}/api/v1${path}"
    else
        curl -sS -u "${GITEA_ADMIN_USER}:${GITEA_ADMIN_PASSWORD}" \
            -X "${method}" "${GITEA_URL}/api/v1${path}"
    fi
}

compose_infra() { docker compose -f "${REPO_ROOT}/infra/compose.yaml" "$@"; }

# compose.override.yaml is picked up automatically; it drops the amd64 pin for local builds.
compose_app() { (cd "${REPO_ROOT}" && docker compose "$@"); }
