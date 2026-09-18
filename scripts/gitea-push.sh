#!/usr/bin/env bash
# gitea-push.sh - create a repository on Gitea and push a local git repository into it.
#
#   scripts/gitea-push.sh --url http://localhost:3000 --token "$GITEA_TOKEN"
#
#   scripts/gitea-push.sh --url https://gitea.competition.local --path ~/competition/session-01 \
#       --owner worldskills --name session-01 --description 'Session 01 judging module'
#
# The first form publishes THIS repository - the one the script lives in - so that the Gitea Actions workflow
# under .gitea/workflows/ can build the platform image and push it to Gitea's container registry. The second
# form publishes any other repository, a session repository say, under an organisation that already exists.
#
# Everything is idempotent: an existing repository is reused, a drifted remote is re-pointed, and the push is
# an ordinary fast-forward. Nothing is force-pushed - a Gitea repository that already carries history is a
# reason to stop and look rather than to overwrite, so a rejected push reports how to reconcile it instead.
#
# The token reaches git through a temporary GIT_ASKPASS helper rather than an embedded credential in the
# remote URL (https://token@host/owner/repo.git). An embedded token is written verbatim into .git/config, so
# it outlives this run, shows up in every `git remote -v`, and travels with anything copied out of the
# terminal. It would also sit in the process argument list, which `ps` shows to every user on the machine.
# The helper instead lives in a 0700 temporary directory that an EXIT trap removes.

set -euo pipefail

API_PREFIX=/api/v1
DEFAULT_REMOTE=gitea
REPO_NAME_PATTERN='^[A-Za-z0-9._-]+$'
WORKFLOW_FILE=.gitea/workflows/publish-image.yml
HTTP_POST_BUFFER=$((1024 * 1024 * 1024))
SCRIPT_NAME=$(basename "${BASH_SOURCE[0]}")
SCRIPT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

GITEA_URL=${GITEA_URL:-}
TOKEN=${GITEA_TOKEN:-}
REPO_PATH=${SCRIPT_ROOT}
OWNER=""
REPO_NAME=""
BRANCH=""
DESCRIPTION=""
REMOTE=${DEFAULT_REMOTE}
ALL_BRANCHES=0
PRIVATE=true

WORK_DIR=""
HTTP_STATUS=""
HTTP_BODY=""

log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m ok\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m  !\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31mFAIL\033[0m %s\n' "$*" >&2; exit 1; }

cleanup() { [[ -n ${WORK_DIR} && -d ${WORK_DIR} ]] && rm -rf "${WORK_DIR}"; return 0; }
trap cleanup EXIT

usage() {
    cat <<EOF
${SCRIPT_NAME} - create a Gitea repository and push a local repository into it.

usage: scripts/${SCRIPT_NAME} --url <gitea-url> --token <token> --path <repo-dir> [options]

  --url     <url>        Gitea base URL, e.g. http://localhost:3000        (env: GITEA_URL)
  --token   <token>      personal access token with repo write scope       (env: GITEA_TOKEN)
  --path    <dir>        local git repository to publish                   (default: ${SCRIPT_ROOT})
  --owner   <user|org>   repository owner                                  (default: the token's own user)
  --name    <repo>       repository name                                   (default: basename of --path)
  --branch  <branch>     branch to push                                    (default: the checked-out branch)
  --all-branches         push every local branch instead of just one
  --public               create the repository public                      (default: private)
  --remote  <name>       git remote to configure in the local repository   (default: ${DEFAULT_REMOTE})
  --description <text>   repository description
  -h, --help             show this help

Prefer the environment for the token (export GITEA_TOKEN=...) so it stays out of the shell history and out
of the process argument list, which \`ps\` shows to every user on the machine.

An --owner that differs from the token's own user is treated as an organisation, which must already exist;
this script does not create organisations.
EOF
}

usage_error() {
    printf '\033[1;31mFAIL\033[0m %s\n\n' "$*" >&2
    usage >&2
    exit 2
}

need_value() { [[ $# -ge 2 ]] || usage_error "option $1 needs a value"; }

while [[ $# -gt 0 ]]; do
    case $1 in
        --url)          need_value "$@"; GITEA_URL=$2; shift 2 ;;
        --token)        need_value "$@"; TOKEN=$2; shift 2 ;;
        --path)         need_value "$@"; REPO_PATH=$2; shift 2 ;;
        --owner)        need_value "$@"; OWNER=$2; shift 2 ;;
        --name)         need_value "$@"; REPO_NAME=$2; shift 2 ;;
        --branch)       need_value "$@"; BRANCH=$2; shift 2 ;;
        --description)  need_value "$@"; DESCRIPTION=$2; shift 2 ;;
        --remote)       need_value "$@"; REMOTE=$2; shift 2 ;;
        --all-branches) ALL_BRANCHES=1; shift ;;
        --public)       PRIVATE=false; shift ;;
        -h|--help)      usage; exit 0 ;;
        --)             shift; break ;;
        *)              usage_error "unknown argument: $1" ;;
    esac
done

[[ $# -eq 0 ]] || usage_error "unexpected argument: $1"
[[ -n ${GITEA_URL} ]] || usage_error "a Gitea URL is required (--url or GITEA_URL)"
[[ -n ${TOKEN} ]]     || usage_error "a personal access token is required (--token or GITEA_TOKEN)"
[[ -n ${REMOTE} ]]    || usage_error "--remote cannot be empty"

GITEA_URL=${GITEA_URL%"${GITEA_URL##*[!/]}"}
[[ ${GITEA_URL} =~ ^https?:// ]] || usage_error "--url must start with http:// or https:// (got '${GITEA_URL}')"

command -v git  >/dev/null 2>&1 || die "git is not installed"
command -v curl >/dev/null 2>&1 || die "curl is not installed"
if command -v jq >/dev/null 2>&1; then HAVE_JQ=1; else HAVE_JQ=0; fi

WORK_DIR=$(mktemp -d "${TMPDIR:-/tmp}/gitea-push.XXXXXX")
chmod 700 "${WORK_DIR}"

# Only top-level, unique, flat string fields are read (login, default_branch, message), so the jq-less
# fallback only has to find the first "key": "value" pair. It is deliberately not asked for html_url or
# clone_url: a repository payload nests an owner object that carries an html_url of its own, ahead of the
# repository's, and those two URLs are built from ${GITEA_URL} below instead. A value containing an escaped
# quote would be truncated here; none of the fields read carry one.
json_field() {
    local body=$1 field=$2 value=""
    if [[ ${HAVE_JQ} -eq 1 ]]; then
        value=$(printf '%s' "${body}" | jq -r --arg f "${field}" '.[$f] // empty' 2>/dev/null || true)
    else
        value=$(printf '%s' "${body}" | tr -d '\n' \
            | grep -o "\"${field}\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" \
            | head -n 1 \
            | sed -e 's/^[^:]*:[[:space:]]*"//' -e 's/"$//' || true)
    fi
    printf '%s' "${value}"
}

# Embedded newlines and tabs become spaces before escaping, so the value stays a single JSON string and two
# words either side of one do not get glued together. The final tr only drops the newline sed appends.
json_escape() { printf '%s' "$1" | tr '\n\r\t' '   ' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' | tr -d '\n'; }

single_quote() { printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"; }

api_error() {
    local message
    message=$(json_field "${HTTP_BODY}" message)
    [[ -n ${message} ]] && printf '%s' "${message}" || printf 'HTTP %s' "${HTTP_STATUS}"
}

# Sets HTTP_STATUS and HTTP_BODY. The Authorization header goes through a curl config file in the 0700
# temporary directory for the same reason the git token does: -H on the command line is visible in `ps`.
CURL_CONFIG=""
api_request() {
    local method=$1 path=$2 body=${3:-} response="" status=0
    local -a args=(-sS -X "${method}" -K "${CURL_CONFIG}" -H 'Accept: application/json'
                   -w '\n%{http_code}' --connect-timeout 10 --max-time 120)

    [[ -n ${body} ]] && args+=(-H 'Content-Type: application/json' --data-binary "${body}")

    response=$(curl "${args[@]}" "${GITEA_URL}${API_PREFIX}${path}" 2>"${WORK_DIR}/curl.err") || status=$?
    if [[ ${status} -ne 0 ]]; then
        warn "$(tr -d '\r' <"${WORK_DIR}/curl.err")"
        die "cannot reach ${GITEA_URL} - check that Gitea is running and that the --url is correct"
    fi

    HTTP_STATUS=${response##*$'\n'}
    HTTP_BODY=${response%$'\n'*}
    [[ ${HTTP_STATUS} == 401 ]] && die "the token was rejected by ${GITEA_URL} - it is invalid or expired"
    return 0
}

printf 'header = "Authorization: token %s"\n' \
    "$(printf '%s' "${TOKEN}" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g')" >"${WORK_DIR}/curl.conf"
chmod 600 "${WORK_DIR}/curl.conf"
CURL_CONFIG="${WORK_DIR}/curl.conf"

REPO_TOP=$(git -C "${REPO_PATH}" rev-parse --show-toplevel 2>/dev/null) \
    || die "--path '${REPO_PATH}' is not a git work tree"

if [[ -n ${BRANCH} ]]; then
    git -C "${REPO_TOP}" rev-parse --verify --quiet "refs/heads/${BRANCH}" >/dev/null \
        || die "branch '${BRANCH}' does not exist in ${REPO_TOP}"
else
    BRANCH=$(git -C "${REPO_TOP}" symbolic-ref --quiet --short HEAD || true)
    if [[ -z ${BRANCH} ]]; then
        [[ ${ALL_BRANCHES} -eq 1 ]] || die "HEAD is detached in ${REPO_TOP} - pass --branch to say what to push"
        warn "HEAD is detached; the Gitea default branch will be left to Gitea to decide"
    elif ! git -C "${REPO_TOP}" rev-parse --verify --quiet "refs/heads/${BRANCH}" >/dev/null; then
        die "branch '${BRANCH}' has no commits yet - commit something in ${REPO_TOP} before publishing it"
    fi
fi

if [[ ${ALL_BRANCHES} -eq 1 ]]; then
    [[ -n $(git -C "${REPO_TOP}" for-each-ref --count 1 --format='%(refname)' refs/heads) ]] \
        || die "${REPO_TOP} has no branches with commits yet"
fi

[[ -n $(git -C "${REPO_TOP}" status --porcelain) ]] \
    && warn "${REPO_TOP} has uncommitted changes; only committed content is pushed"

[[ -n ${REPO_NAME} ]] || REPO_NAME=$(basename "${REPO_TOP}")
[[ ${REPO_NAME} =~ ${REPO_NAME_PATTERN} ]] \
    || usage_error "repository name '${REPO_NAME}' is not valid on Gitea (letters, digits, '.', '-', '_')"
[[ ${REPO_NAME} == "." || ${REPO_NAME} == ".." ]] \
    && usage_error "repository name '${REPO_NAME}' is not usable - pass --name"

log "authenticating against ${GITEA_URL}"
api_request GET /user
[[ ${HTTP_STATUS} == 200 ]] || die "could not identify the token's user: $(api_error)"
LOGIN=$(json_field "${HTTP_BODY}" login)
[[ -n ${LOGIN} ]] || die "the response to GET ${API_PREFIX}/user carried no login field"
ok "authenticated as ${LOGIN}"

IS_ORG=0
[[ -n ${OWNER} ]] || OWNER=${LOGIN}
if [[ ${OWNER} != "${LOGIN}" ]]; then
    api_request GET "/orgs/${OWNER}"
    case ${HTTP_STATUS} in
        200) IS_ORG=1 ;;
        404) die "organisation '${OWNER}' does not exist - create it in Gitea first, or drop --owner" ;;
        *)   die "could not look up organisation '${OWNER}': $(api_error)" ;;
    esac
fi

CLONE_URL="${GITEA_URL}/${OWNER}/${REPO_NAME}.git"
HTML_URL="${GITEA_URL}/${OWNER}/${REPO_NAME}"
REPO_EXISTED=0
EXISTING_DEFAULT_BRANCH=""

log "looking for ${OWNER}/${REPO_NAME}"
api_request GET "/repos/${OWNER}/${REPO_NAME}"
case ${HTTP_STATUS} in
    200) REPO_EXISTED=1 ;;
    404) REPO_EXISTED=0 ;;
    *)   die "could not look up ${OWNER}/${REPO_NAME}: $(api_error)" ;;
esac

if [[ ${REPO_EXISTED} -eq 0 ]]; then
    CREATE_BODY=$(printf '{"name":"%s","private":%s,"description":"%s","auto_init":false' \
        "$(json_escape "${REPO_NAME}")" "${PRIVATE}" "$(json_escape "${DESCRIPTION}")")
    [[ -n ${BRANCH} ]] && CREATE_BODY+=$(printf ',"default_branch":"%s"' "$(json_escape "${BRANCH}")")
    CREATE_BODY+='}'

    if [[ ${IS_ORG} -eq 1 ]]; then CREATE_PATH="/orgs/${OWNER}/repos"; else CREATE_PATH=/user/repos; fi

    log "creating ${OWNER}/${REPO_NAME}"
    api_request POST "${CREATE_PATH}" "${CREATE_BODY}"
    case ${HTTP_STATUS} in
        201) ok "created ${OWNER}/${REPO_NAME}" ;;
        409) REPO_EXISTED=1 ;;
        403) die "the token may not create repositories under '${OWNER}': $(api_error)" ;;
        *)   die "could not create ${OWNER}/${REPO_NAME}: $(api_error)" ;;
    esac

    if [[ ${REPO_EXISTED} -eq 1 ]]; then
        api_request GET "/repos/${OWNER}/${REPO_NAME}"
        [[ ${HTTP_STATUS} == 200 ]] || die "could not read ${OWNER}/${REPO_NAME}: $(api_error)"
    fi
fi

if [[ ${REPO_EXISTED} -eq 1 ]]; then
    EXISTING_DEFAULT_BRANCH=$(json_field "${HTTP_BODY}" default_branch)
    ok "reusing the existing repository ${OWNER}/${REPO_NAME}"
fi

# Actions are off per repository on a fresh Gitea, so the workflow would sit there doing nothing. Older Gitea
# builds do not know the has_actions field; that is worth a warning, not a failed publish.
log "enabling Actions on ${OWNER}/${REPO_NAME}"
api_request PATCH "/repos/${OWNER}/${REPO_NAME}" '{"has_actions":true}'
if [[ ${HTTP_STATUS} == 200 ]]; then
    ok "Actions enabled"
else
    warn "could not enable Actions ($(api_error)); turn them on under Settings -> Repository -> Actions"
fi

CURRENT_REMOTE_URL=$(git -C "${REPO_TOP}" remote get-url "${REMOTE}" 2>/dev/null || true)
if [[ -z ${CURRENT_REMOTE_URL} ]]; then
    git -C "${REPO_TOP}" remote add "${REMOTE}" "${CLONE_URL}"
    ok "added remote '${REMOTE}' -> ${CLONE_URL}"
elif [[ ${CURRENT_REMOTE_URL} != "${CLONE_URL}" ]]; then
    git -C "${REPO_TOP}" remote set-url "${REMOTE}" "${CLONE_URL}"
    ok "repointed remote '${REMOTE}' -> ${CLONE_URL}"
fi

ASKPASS="${WORK_DIR}/askpass.sh"
cat >"${ASKPASS}" <<EOF
#!/bin/sh
case "\$1" in
    *[Uu]sername*) printf '%s\n' $(single_quote "${LOGIN}") ;;
    *)             printf '%s\n' $(single_quote "${TOKEN}") ;;
esac
EOF
chmod 700 "${ASKPASS}"

# credential.helper is emptied to reset the list, so a configured helper - osxkeychain on the competition
# Macs - cannot answer with a stale Gitea credential before the askpass helper is ever consulted.
#
# http.postBuffer is raised so the pack is sent with a Content-Length instead of chunked transfer encoding.
# Above the 1 MiB default git switches to chunked, and the git that ships with macOS (2.40 on Apple's
# libcurl 8.7) terminates that upload immediately: Gitea's receive-pack sees an empty body and both ends
# report "the remote end hung up unexpectedly". Verified to fail with and without a proxy in between, and to
# work from a Linux git; buffering costs memory equal to the pack size, which is nothing for a source repo.
git_push() {
    GIT_ASKPASS="${ASKPASS}" GIT_TERMINAL_PROMPT=0 \
        git -C "${REPO_TOP}" -c credential.helper= -c "http.postBuffer=${HTTP_POST_BUFFER}" push "$@" 2>&1 \
        | tee -a "${WORK_DIR}/push.log"
}

if [[ ${ALL_BRANCHES} -eq 1 ]]; then
    log "pushing every branch to ${REMOTE}"
    PUSH_OK=0; git_push --all "${REMOTE}" || PUSH_OK=$?
else
    log "pushing ${BRANCH} to ${REMOTE}"
    PUSH_OK=0; git_push "${REMOTE}" "refs/heads/${BRANCH}:refs/heads/${BRANCH}" || PUSH_OK=$?
fi

if [[ ${PUSH_OK} -ne 0 ]]; then
    if grep -qiE 'rejected|non-fast-forward|fetch first' "${WORK_DIR}/push.log"; then
        die "${OWNER}/${REPO_NAME} already has diverging history on Gitea. Reconcile it first -
     git -C ${REPO_TOP} fetch ${REMOTE} && git -C ${REPO_TOP} rebase ${REMOTE}/${BRANCH:-<branch>}
     - or publish under a fresh name with --name. This script never force-pushes."
    fi
    if grep -qiE 'authentication failed|permission denied|returned error: 40' "${WORK_DIR}/push.log"; then
        die "git was refused by ${GITEA_URL} - the token needs the write:repository scope"
    fi
    die "the push to ${REMOTE} failed; see the git output above"
fi
ok "pushed"

if [[ -n $(git -C "${REPO_TOP}" tag --list | head -n 1) ]]; then
    log "pushing tags"
    git_push "${REMOTE}" --tags || warn "the tags could not be pushed; the branch is published regardless"
fi

# Workflows are keyed on the branch they arrive at, so a repository whose default branch is still Gitea's
# initial guess would accept the push and run nothing.
if [[ ${ALL_BRANCHES} -eq 0 && -n ${EXISTING_DEFAULT_BRANCH} && ${EXISTING_DEFAULT_BRANCH} != "${BRANCH}" ]]; then
    log "moving the default branch from ${EXISTING_DEFAULT_BRANCH} to ${BRANCH}"
    api_request PATCH "/repos/${OWNER}/${REPO_NAME}" \
        "$(printf '{"default_branch":"%s"}' "$(json_escape "${BRANCH}")")"
    if [[ ${HTTP_STATUS} == 200 ]]; then
        ok "default branch is now ${BRANCH}"
    else
        warn "could not set the default branch to ${BRANCH} ($(api_error)); set it in the Gitea settings"
    fi
fi

printf '\n'
ok "repository  ${HTML_URL}"
ok "clone url   ${CLONE_URL}"
printf '\n     Next steps:\n'
printf '       1. Register runners if none is registered yet: infra/runner-token.sh, then\n'
printf '          docker compose -f infra/compose.yaml up -d\n'
printf '       2. Every push from now on triggers %s in this repository.\n' "${WORKFLOW_FILE}"
printf '       3. Push again with: git -C %s push %s %s\n\n' "${REPO_TOP}" "${REMOTE}" "${BRANCH:-<branch>}"
