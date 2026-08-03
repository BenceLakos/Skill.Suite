#!/usr/bin/env bash
# restore.sh <backup-directory> - restore a competition from a scripts/backup.sh snapshot.
#
#   scripts/restore.sh backups/20260803T120000Z
#
# DESTRUCTIVE: it drops and recreates the database and replaces both volumes. It refuses to run against a
# stack with the application up, because restoring under a live app produces a database the app has half-cached
# and a key ring it has already loaded.
#
# Rehearse this before the competition, into a clean stack, and check that the marks come back. A backup nobody
# has restored is a belief rather than a plan — and the moment you need it is the worst moment to discover the
# key ring was missing.

set -euo pipefail

SRC=${1:-}
[[ -n "${SRC}" && -d "${SRC}" ]] || { printf 'usage: %s <backup-directory>\n' "$(basename "$0")" >&2; exit 2; }

PG_CONTAINER=${PG_CONTAINER:-skill-suite-postgres}
PG_USER=${PG_USER:-skillsuite}
PG_DB=${PG_DB:-skillsuite}
KEYS_VOLUME=${KEYS_VOLUME:-skill-suite-keys}
WORKDIR_VOLUME=${WORKDIR_VOLUME:-skill-suite-workdir}
APP_CONTAINER=${APP_CONTAINER:-skill-suite}
# Same reasoning as backup.sh: an image already on the host, not one that needs pulling mid-incident.
HELPER_IMAGE=${HELPER_IMAGE:-$(docker inspect --format '{{.Config.Image}}' "${PG_CONTAINER}" 2>/dev/null || echo postgres:18.4)}

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()  { printf '\033[1;32m ok\033[0m %s\n' "$*"; }
die() { printf '\033[1;31mFAIL\033[0m %s\n' "$*" >&2; exit 1; }

[[ -f "${SRC}/postgres.dump" ]] || die "no postgres.dump in ${SRC}"

if docker ps --format '{{.Names}}' | grep -qx "${APP_CONTAINER}"; then
    die "${APP_CONTAINER} is running. Stop it first: docker compose stop skill.suite"
fi

if [[ -f "${SRC}/manifest.txt" ]]; then
    printf '\nRestoring this snapshot:\n'; sed 's/^/     /' "${SRC}/manifest.txt"; printf '\n'
fi

[[ -f "${SRC}/keys.tar.gz" ]] || printf '  ! no keys.tar.gz — every stored credential and webhook secret will\n    be undecryptable after this restore. Continue only if that is expected.\n\n' >&2

read -r -p "This replaces the database and both volumes. Type the database name to confirm: " confirm
[[ "${confirm}" == "${PG_DB}" ]] || die "not confirmed"

log "recreating ${PG_DB}"
# Terminate other sessions first, or DROP DATABASE fails with "is being accessed by other users".
docker exec "${PG_CONTAINER}" psql -U "${PG_USER}" -d postgres -c \
    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '${PG_DB}' AND pid <> pg_backend_pid();" >/dev/null
docker exec "${PG_CONTAINER}" psql -U "${PG_USER}" -d postgres -c "DROP DATABASE IF EXISTS ${PG_DB};" >/dev/null
docker exec "${PG_CONTAINER}" psql -U "${PG_USER}" -d postgres -c "CREATE DATABASE ${PG_DB} OWNER ${PG_USER};" >/dev/null

log "restoring postgres.dump"
docker exec -i "${PG_CONTAINER}" pg_restore -U "${PG_USER}" -d "${PG_DB}" --no-owner <"${SRC}/postgres.dump" \
    || die "pg_restore failed"
ok "database restored"

restore_volume() {
    local volume=$1 archive=$2
    [[ -f "${SRC}/${archive}" ]] || return 0

    log "restoring volume ${volume}"
    docker volume create "${volume}" >/dev/null
    # Emptied first: an overlay would leave files that the snapshot no longer has, and for the key ring a
    # leftover key is worse than a missing one — it decrypts some rows and not others.
    docker run --rm -v "${volume}:/dst" -v "${SRC}:/in:ro" "${HELPER_IMAGE}" \
        sh -c 'rm -rf /dst/* /dst/..?* 2>/dev/null; tar -C /dst -xzf "/in/'"${archive}"'"' \
        || die "could not restore ${volume}"
    ok "${volume} restored"
}

restore_volume "${KEYS_VOLUME}" keys.tar.gz
restore_volume "${WORKDIR_VOLUME}" workdir.tar.gz

printf '\n'
ok "restore complete"
printf '     Now: docker compose up -d skill.suite\n'
printf '     Then verify a known mark is back before trusting it — count test_runs and open one run detail.\n'
