#!/usr/bin/env bash
# starter-packages.sh <command> - manage the starter packages volume from a shell.
#
#   scripts/starter-packages.sh push judging/samples/fibonacci-session      copy a session in, as fibonacci-session
#   scripts/starter-packages.sh push ~/sessions/task-01 task-01             ...under an explicit name
#   scripts/starter-packages.sh list                                        what is in the volume
#   scripts/starter-packages.sh pull fibonacci-session ./out                copy a package back out
#   scripts/starter-packages.sh remove fibonacci-session                    delete one package
#
# The platform reads session template folders from the docker volume skill-suite-starter-packages, mounted at
# /starter-packages in the container, so a session's TemplateFolder is /starter-packages/<name>/competitor-start.
# The volume, rather than a host directory, is what makes the deployment identical on a Linux, macOS or
# Windows docker host - and it means the files are only reachable through docker, which is what this script
# does. The Starter packages page in the UI does the same over http: a new package from a zip archive, and
# files and folders uploaded straight into a package's own folders.
#
# Copies go through a throwaway container that mounts the volume and the source directory, because a
# docker-managed volume has no host path to write to directly.
#
# Env:
#   STARTER_VOLUME   volume name (default skill-suite-starter-packages)
#   HELPER_IMAGE     image for the copy container; defaults to the running Postgres image so no pull is needed

set -euo pipefail

STARTER_VOLUME=${STARTER_VOLUME:-skill-suite-starter-packages}
PG_CONTAINER=${PG_CONTAINER:-skill-suite-postgres}
HELPER_IMAGE=${HELPER_IMAGE:-$(docker inspect --format '{{.Config.Image}}' "${PG_CONTAINER}" 2>/dev/null || echo postgres:18.4)}
PACKAGE_NAME_PATTERN='^[A-Za-z0-9][A-Za-z0-9._-]*$'
MOUNT=/starter-packages

log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m ok\033[0m %s\n' "$*"; }
die()  { printf '\033[1;31mFAIL\033[0m %s\n' "$*" >&2; exit 1; }

usage() {
    cat <<EOF
usage: scripts/starter-packages.sh <command>

  push <dir> [name]     copy <dir> into the volume as <name> (default: basename of <dir>), replacing it
  pull <name> <dir>     copy the package <name> out of the volume into <dir>/<name>
  list                  list the packages and their top-level content
  remove <name>         delete the package <name>

Env: STARTER_VOLUME (default ${STARTER_VOLUME}), HELPER_IMAGE
EOF
}

# One helper-container invocation. The volume is always at ${MOUNT}; callers add their own mounts.
in_volume() {
    docker run --rm -v "${STARTER_VOLUME}:${MOUNT}" "$@"
}

require_name() {
    [[ $1 =~ ${PACKAGE_NAME_PATTERN} ]] || die "'$1' is not a usable package name (letters, digits, '.', '-', '_')"
}

cmd_push() {
    local dir=$1 name=${2:-} src
    [[ -d ${dir} ]] || die "'${dir}' is not a directory"
    src=$(cd "${dir}" && pwd)
    [[ -n ${name} ]] || name=$(basename "${src}")
    require_name "${name}"
    [[ -n $(ls -A "${src}") ]] || die "'${src}' is empty"

    log "copying ${src} into ${STARTER_VOLUME} as ${name}"
    # rm first so files deleted from the source do not linger in the package; -T-less cp of `/src/.` copies the
    # content, not the directory, and keeps hidden files.
    in_volume -v "${src}:/src:ro" "${HELPER_IMAGE}" \
        sh -c "rm -rf '${MOUNT}/${name}' && mkdir -p '${MOUNT}/${name}' && cp -R /src/. '${MOUNT}/${name}/'" \
        || die "copy failed"
    ok "${name} is in the volume"
    printf '     TemplateFolder for a session: %s/%s/competitor-start\n' "${MOUNT}" "${name}"
}

cmd_pull() {
    local name=$1 dir=$2 dest
    require_name "${name}"
    mkdir -p "${dir}"
    dest=$(cd "${dir}" && pwd)
    log "copying ${name} out of ${STARTER_VOLUME} into ${dest}/${name}"
    in_volume -v "${dest}:/dst" "${HELPER_IMAGE}" \
        sh -c "[ -d '${MOUNT}/${name}' ] || { echo 'no such package: ${name}' >&2; exit 1; }; mkdir -p '/dst/${name}' && cp -R '${MOUNT}/${name}/.' '/dst/${name}/'" \
        || die "copy failed"
    ok "${dest}/${name}"
}

cmd_list() {
    docker volume inspect "${STARTER_VOLUME}" >/dev/null 2>&1 \
        || die "volume ${STARTER_VOLUME} does not exist yet - it is created by the first deployment or the first push"
    in_volume "${HELPER_IMAGE}" sh -c "
        cd '${MOUNT}' || exit 1
        found=0
        for p in */; do
            [ -d \"\$p\" ] || continue
            found=1
            printf '%s\n' \"\${p%/}\"
            ls -A \"\$p\" | sed 's/^/    /'
        done
        [ \"\$found\" = 1 ] || echo '(empty)'"
}

cmd_remove() {
    local name=$1
    require_name "${name}"
    log "removing ${name} from ${STARTER_VOLUME}"
    in_volume "${HELPER_IMAGE}" sh -c "[ -d '${MOUNT}/${name}' ] || { echo 'no such package: ${name}' >&2; exit 1; }; rm -rf '${MOUNT}/${name}'" \
        || die "remove failed"
    ok "removed"
}

main() {
    command -v docker >/dev/null 2>&1 || die "docker is not installed"
    local command=${1:-}
    [[ -n ${command} ]] || { usage >&2; exit 2; }
    shift
    case ${command} in
        push)   [[ $# -ge 1 && $# -le 2 ]] || { usage >&2; exit 2; }; cmd_push "$@" ;;
        pull)   [[ $# -eq 2 ]] || { usage >&2; exit 2; }; cmd_pull "$@" ;;
        list)   [[ $# -eq 0 ]] || { usage >&2; exit 2; }; cmd_list ;;
        remove) [[ $# -eq 1 ]] || { usage >&2; exit 2; }; cmd_remove "$@" ;;
        -h|--help|help) usage ;;
        *) usage >&2; die "unknown command: ${command}" ;;
    esac
}

main "$@"
