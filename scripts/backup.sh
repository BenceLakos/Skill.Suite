#!/usr/bin/env bash
# backup.sh [destination] - snapshot everything a competition cannot be reconstructed without.
#
#   scripts/backup.sh /Volumes/backup/skillsuite
#
# Run it on a timer during a competition, to a DIFFERENT disk from the one the stack runs on. A single volume
# loss otherwise loses every mark with no way back.
#
# Three things are backed up, and all three are required — any one missing makes the others useless:
#
#   1. Postgres          the marks, the runs, the sessions, the competitors.
#   2. DataProtection    the key ring. Without it every stored credential is unwrappable and every session's
#      key ring        webhook secret undecryptable, so restoring the database alone gives you a competition
#                      where no push can be verified and no repository can be cloned.
#   3. The workdir       the cloned submissions and each run's events.jsonl. This is what an expert re-reads to
#      volume           settle a dispute, and what ReprocessTestRunLog needs to rebuild a lost result.
#
# The starter packages volume is taken as well. It is reconstructible from the session repositories, but a
# restored competition whose sessions point at empty template folders cannot enrol a late competitor, and the
# packages are small.
#
# Restore with scripts/restore.sh, and REHEARSE it before the competition. An untested backup is a belief.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST=${1:-"${HERE}/backups"}
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
OUT="${DEST}/${STAMP}"

PG_CONTAINER=${PG_CONTAINER:-skill-suite-postgres}
PG_USER=${PG_USER:-skillsuite}
PG_DB=${PG_DB:-skillsuite}
KEYS_VOLUME=${KEYS_VOLUME:-skill-suite-keys}
WORKDIR_VOLUME=${WORKDIR_VOLUME:-skill-suite-workdir}
STARTER_VOLUME=${STARTER_VOLUME:-skill-suite-starter-packages}

# Helper image for reading docker-managed volumes. Defaults to the Postgres image, which is guaranteed present
# because the database is running from it. It used to be alpine:3, which is not — and pulling it hung on a
# credential helper, so the backup script's first act was a network round trip it did not need. A backup tool
# that requires the internet is no use on a competition machine.
HELPER_IMAGE=${HELPER_IMAGE:-$(docker inspect --format '{{.Config.Image}}' "${PG_CONTAINER}" 2>/dev/null || echo postgres:18.4)}

# The workdir holds every clone ever made and nothing prunes it, so it grows without bound over a competition.
# Archiving it on a 15-minute timer is not viable. Off by default: the marks live in Postgres and the events
# logs are recoverable from the runs themselves, so this matters for dispute evidence rather than for scoring.
BACKUP_WORKDIR=${BACKUP_WORKDIR:-0}

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()  { printf '\033[1;32m ok\033[0m %s\n' "$*"; }
die() { printf '\033[1;31mFAIL\033[0m %s\n' "$*" >&2; exit 1; }

docker inspect "${PG_CONTAINER}" >/dev/null 2>&1 || die "container ${PG_CONTAINER} is not running"
mkdir -p "${OUT}"

# --format=custom so restore can be selective and parallel, and so a truncated file is detectable rather than
# silently replaying half a competition.
log "dumping ${PG_DB}"
docker exec "${PG_CONTAINER}" pg_dump -U "${PG_USER}" -d "${PG_DB}" --format=custom \
    >"${OUT}/postgres.dump" || die "pg_dump failed"
ok "postgres.dump ($(du -h "${OUT}/postgres.dump" | cut -f1))"

# Volumes are copied through a throwaway container because they are docker-managed: there is no host path to
# tar directly, and hunting for one under /var/lib/docker is not portable.
backup_volume() {
    local volume=$1 name=$2
    if ! docker volume inspect "${volume}" >/dev/null 2>&1; then
        printf '  ! volume %s does not exist; skipping\n' "${volume}" >&2
        return
    fi

    log "archiving volume ${volume}"
    docker run --rm -v "${volume}:/src:ro" -v "${OUT}:/out" "${HELPER_IMAGE}" \
        tar -C /src -czf "/out/${name}.tar.gz" . || die "could not archive ${volume}"
    ok "${name}.tar.gz ($(du -h "${OUT}/${name}.tar.gz" | cut -f1))"
}

backup_volume "${KEYS_VOLUME}" keys
backup_volume "${STARTER_VOLUME}" starter-packages

if [[ "${BACKUP_WORKDIR}" == "1" ]]; then
    backup_volume "${WORKDIR_VOLUME}" workdir
else
    printf '  ! skipping the workdir volume (BACKUP_WORKDIR=1 to include it). Marks are in Postgres; this\n'
    printf '    volume holds the checkouts and event logs used as dispute evidence, and it never prunes.\n'
fi

# A manifest, so a restore knows what it is looking at and can refuse a mismatched set.
cat >"${OUT}/manifest.txt" <<EOF
taken            ${STAMP}
postgres         ${PG_DB} as ${PG_USER} from ${PG_CONTAINER}
keys volume      ${KEYS_VOLUME}
workdir volume   ${WORKDIR_VOLUME}
starter volume   ${STARTER_VOLUME}
migrations       $(docker exec "${PG_CONTAINER}" psql -U "${PG_USER}" -d "${PG_DB}" -tAc \
                   'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1' 2>/dev/null || echo unknown)
test_runs        $(docker exec "${PG_CONTAINER}" psql -U "${PG_USER}" -d "${PG_DB}" -tAc \
                   'SELECT count(*) FROM test_runs' 2>/dev/null || echo unknown)
EOF

printf '\n'
ok "backup complete: ${OUT}"
cat "${OUT}/manifest.txt" | sed 's/^/     /'
printf '\n     Restore drill: scripts/restore.sh %s\n' "${OUT}"
