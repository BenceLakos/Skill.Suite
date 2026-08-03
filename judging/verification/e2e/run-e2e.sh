#!/usr/bin/env bash
# run-e2e.sh [image] [competitor]
#
# Proves the whole platform chain on a local machine: a git push reaches the webhook, the submission is
# cloned, the judgement container runs, its events are parsed, and the results land in the database with the
# competitor-visible score computed.
#
#   judging/verification/e2e/run-e2e.sh                              # judge-smoke, competitor "alice"
#   judging/verification/e2e/run-e2e.sh judge-fixture:1 bob          # a real white-box judge
#
# Everything is provisioned from scratch and is idempotent, so it can be re-run.
#
# Why no credentials are involved: the Gitea repository is created public and the judge image is local, so
# Session.GitCredentialId and JudgementImagePullCredentialId both stay null. That keeps the whole thing
# scriptable - a Credential row is DataProtection-encrypted and can only be created through the running
# application - at the cost of not exercising the registry-auth path. See README.md.

set -euo pipefail

. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

PUSHED_COMMIT=""
JUDGE_IMAGE=${1:-skill-suite-judge-smoke:1}
COMPETITOR=${2:-alice}
SESSION_NAME="E2E ${JUDGE_IMAGE}"

# The slug is load-bearing, not cosmetic: ProcessGitWebhookHandler finds the session by matching the pushed
# repository's OWNER against Session.Slug, so the Gitea organisation must be named exactly this. Derived from
# the image because Slug is uniquely indexed and a second image would otherwise collide with the first run.
SESSION_SLUG="e2e-$(printf '%s' "${JUDGE_IMAGE}" | tr -c 'a-zA-Z0-9' '-' | tr -s '-' | sed 's/^-//;s/-$//')"
readonly SESSION_SLUG

# ---------------------------------------------------------------------------- 1. infrastructure

step_infra() {
    log "Bringing up Postgres and Gitea"
    compose_infra up -d postgres gitea >/dev/null
    wait_for "Postgres accepting connections" 120 \
        docker exec "${PG_CONTAINER}" pg_isready -q -U skillsuite -d skillsuite
    wait_for "Gitea responding" 180 curl -sf "${GITEA_URL}/api/healthz"

    log "Building and starting the application"
    # The judge image must exist on the host daemon: docker run is invoked with no --pull, and the image is
    # local rather than in a registry.
    docker image inspect "${JUDGE_IMAGE}" >/dev/null 2>&1 \
        || die "judge image ${JUDGE_IMAGE} not found on this daemon; build it first"

    compose_app up -d --build skill.suite >/dev/null
    wait_for "application responding" 300 curl -sf -o /dev/null "${APP_URL}/Account/Login"
}

# ---------------------------------------------------------------------------- 2. Gitea

step_gitea() {
    log "Provisioning Gitea"

    # The password is regenerated per run, so an admin left over from an earlier run has a different one.
    # Create or reset accordingly - checking "can I authenticate?" first would fail for exactly that reason
    # and then fail again trying to create a user that already exists.
    if docker exec -u git "${GITEA_CONTAINER}" gitea admin user list 2>/dev/null \
        | awk 'NR > 1 { print $2 }' | grep -qx "${GITEA_ADMIN_USER}"; then
        docker exec -u git "${GITEA_CONTAINER}" gitea admin user change-password \
            --username "${GITEA_ADMIN_USER}" --password "${GITEA_ADMIN_PASSWORD}" \
            --must-change-password=false >/dev/null 2>&1 \
            || die "could not reset the password for the existing Gitea admin ${GITEA_ADMIN_USER}"
    else
        docker exec -u git "${GITEA_CONTAINER}" gitea admin user create \
            --admin --username "${GITEA_ADMIN_USER}" --password "${GITEA_ADMIN_PASSWORD}" \
            --email "${GITEA_ADMIN_EMAIL}" --must-change-password=false >/dev/null 2>&1 \
            || die "could not create the Gitea admin user ${GITEA_ADMIN_USER}"
    fi

    # A direct curl, not a wrapper function: wait_for runs its command with "$@", and a shell function is not
    # visible inside the subshell that would be needed to pipe it.
    wait_for "Gitea admin ${GITEA_ADMIN_USER} usable" 60 \
        curl -sf -o /dev/null -u "${GITEA_ADMIN_USER}:${GITEA_ADMIN_PASSWORD}" "${GITEA_URL}/api/v1/user"

    # One organisation per session, named after the session slug, and the repository inside it named after the
    # competitor - the same shape StartSession provisions, so this harness exercises the real resolution path
    # rather than a shortcut that only works here.
    if ! gitea_api GET "/orgs/${SESSION_SLUG}" | grep -q '"username"'; then
        gitea_api POST /orgs \
            "{\"username\":\"${SESSION_SLUG}\",\"visibility\":\"public\"}" >/dev/null
    fi
    ok "organisation ${SESSION_SLUG}"

    # Public repository: a public repo needs no credential to clone, which is what keeps this script free of
    # any secret the application would have to store.
    if ! gitea_api GET "/repos/${SESSION_SLUG}/${COMPETITOR}" | grep -q '"full_name"'; then
        gitea_api POST "/orgs/${SESSION_SLUG}/repos" \
            "{\"name\":\"${COMPETITOR}\",\"private\":false,\"auto_init\":false}" >/dev/null
    fi
    ok "repository ${SESSION_SLUG}/${COMPETITOR} (public)"

    # No secret on this hook. The handler accepts an unsigned push only when the session row carries no
    # webhook secret, which is the case here because this script inserts the session with SQL and cannot
    # produce the DataProtection-encrypted form. A session started through the UI always gets a signed hook.
    local hooks
    hooks=$(gitea_api GET "/orgs/${SESSION_SLUG}/hooks")
    if ! printf '%s' "${hooks}" | grep -q '/webhooks/git'; then
        gitea_api POST "/orgs/${SESSION_SLUG}/hooks" \
            "{\"type\":\"gitea\",\"active\":true,\"events\":[\"push\"],\"branch_filter\":\"main\",\"config\":{\"url\":\"http://${APP_CONTAINER}:8080/webhooks/git\",\"content_type\":\"json\"}}" >/dev/null
    fi
    ok "push webhook -> http://${APP_CONTAINER}:8080/webhooks/git"
}

# ---------------------------------------------------------------------------- 3. platform records

step_records() {
    log "Provisioning the session and competitor"

    # Inserted with SQL rather than through the UI because the application exposes no admin API - only the
    # webhook, the log download and login. A Credential would have to go through the app (DataProtection),
    # which is exactly why this E2E avoids needing one.
    #
    # EncryptedPassword is required but never read on this path: the competitor is resolved from the
    # repository name, and only the admin UI ever decrypts it.
    psql_exec "
        INSERT INTO competitors (\"Id\", \"Username\", \"FullName\", \"EncryptedPassword\", \"IpAddress\",
                                 \"CountryCode\", \"CreatedAt\")
        SELECT gen_random_uuid(), '${COMPETITOR}', 'E2E ${COMPETITOR}', '\\x00'::bytea, '127.0.0.1',
               'HUN', now()
        WHERE NOT EXISTS (SELECT 1 FROM competitors WHERE \"Username\" = '${COMPETITOR}');" >/dev/null
    ok "competitor ${COMPETITOR}"

    # Exactly one Active session must exist: the webhook picks the Active one with the lowest StartsAt, so a
    # leftover from an earlier run would silently judge with the wrong image.
    psql_exec "UPDATE sessions SET \"Status\" = 2 WHERE \"Status\" = 1 AND \"Name\" <> '${SESSION_NAME}';" >/dev/null

    psql_exec "
        INSERT INTO sessions (\"Id\", \"Name\", \"Slug\", \"StartsAt\", \"EndsAt\", \"Status\",
                              \"JudgementImage\", \"DatabaseReadAccess\", \"DatabaseWriteAccess\",
                              \"DockerImages\", \"CreatedAt\")
        SELECT gen_random_uuid(), '${SESSION_NAME}', '${SESSION_SLUG}', now() - interval '1 hour',
               now() + interval '1 day', 1, '${JUDGE_IMAGE}', true, true, '[]'::jsonb, now()
        WHERE NOT EXISTS (SELECT 1 FROM sessions WHERE \"Slug\" = '${SESSION_SLUG}');" >/dev/null

    psql_exec "
        UPDATE sessions SET \"Status\" = 1, \"JudgementImage\" = '${JUDGE_IMAGE}'
        WHERE \"Name\" = '${SESSION_NAME}';" >/dev/null
    ok "active session '${SESSION_NAME}' judging with ${JUDGE_IMAGE}"

    # The enrolment row StartSession would have written. Without it the webhook has no competitor to attribute
    # the push to and rejects it - deliberately, since guessing from the repository name is what let a push to
    # 'alice' be credited to a competitor named 'ali'.
    psql_exec "
        INSERT INTO session_competitors (\"Id\", \"SessionId\", \"CompetitorId\", \"RepositoryName\",
                                         \"RepositoryUrl\", \"ProvisionStatus\", \"ProvisionedAt\")
        SELECT gen_random_uuid(), s.\"Id\", c.\"Id\", '${COMPETITOR}',
               'http://gitea:3000/${SESSION_SLUG}/${COMPETITOR}.git', 1, now()
        FROM sessions s, competitors c
        WHERE s.\"Slug\" = '${SESSION_SLUG}' AND c.\"Username\" = '${COMPETITOR}'
          AND NOT EXISTS (SELECT 1 FROM session_competitors x
                          WHERE x.\"SessionId\" = s.\"Id\" AND x.\"RepositoryName\" = '${COMPETITOR}');" >/dev/null
    ok "competitor ${COMPETITOR} enrolled as ${SESSION_SLUG}/${COMPETITOR}"
}

# ---------------------------------------------------------------------------- 4. push

step_push() {
    log "Pushing a submission"

    local work
    work=$(mktemp -d)

    local persona=${E2E_PERSONA:-}
    if [[ -n "${persona}" && -d "${persona}" ]]; then
        cp -R "${persona}/." "${work}/"
        ok "submission contents from ${persona}"
    else
        printf '# E2E submission\n\nThe smoke judge ignores this content entirely.\n' >"${work}/README.md"
        ok "submission contents: placeholder README"
    fi

    (
        cd "${work}"
        git init -q -b main
        git -c user.email=e2e@example.invalid -c user.name='E2E' add -A
        git -c user.email=e2e@example.invalid -c user.name='E2E' commit -qm "E2E submission $(date -u +%H:%M:%S)"
        # Credentials in the URL are the throwaway admin generated for this run. Push always needs auth even
        # for a public repository.
        git remote add origin \
            "http://${GITEA_ADMIN_USER}:${GITEA_ADMIN_PASSWORD}@localhost:3000/${SESSION_SLUG}/${COMPETITOR}.git"
        git push -q --force origin main
        git rev-parse HEAD >"${work}/.pushed-sha"
    ) || die "push to Gitea failed"

    # The assertions match on this. Taking "the newest terminal run" instead silently passes against a stale
    # run left in the database by earlier work - which is exactly what happened the first time this ran, and
    # reported success for a run the push had nothing to do with.
    PUSHED_COMMIT=$(tr -d '[:space:]' <"${work}/.pushed-sha")
    rm -rf "${work}"

    ok "pushed ${PUSHED_COMMIT:0:8} to ${SESSION_SLUG}/${COMPETITOR}"
}

# ---------------------------------------------------------------------------- 5. assert

step_assert() {
    log "Waiting for the judgement run for commit ${PUSHED_COMMIT:0:8}"

    # Scoped to this push's commit, never "the newest run". A stale run from earlier work would otherwise
    # satisfy every assertion below and report a green E2E that proved nothing.
    local created="" waited=0
    while (( waited < 120 )); do
        created=$(psql_exec "
            SELECT \"Id\" FROM test_runs WHERE \"CommitSha\" = '${PUSHED_COMMIT}' LIMIT 1;" \
            | tr -d '[:space:]')
        [[ -n "${created}" ]] && break
        sleep 3
        waited=$((waited + 3))
    done

    if [[ -z "${created}" ]]; then
        warn "no TestRun row exists for this commit - the webhook never reached the application"
        warn "check: docker logs ${APP_CONTAINER} | tail, and the delivery log in Gitea's repository settings"
        die "webhook did not create a run within 120s"
    fi
    ok "webhook created run ${created}"

    local run_id="" status=""
    waited=0
    while (( waited < 300 )); do
        status=$(psql_exec "
            SELECT \"Status\" FROM test_runs WHERE \"Id\" = '${created}';" | tr -d '[:space:]')
        if [[ "${status}" == "3" || "${status}" == "4" || "${status}" == "5" ]]; then
            run_id=${created}
            break
        fi
        sleep 3
        waited=$((waited + 3))
    done

    [[ -n "${run_id}" ]] || die "run ${created} did not reach a terminal state within 300s (last status ${status})"

    local reason fixtures units
    reason=$(psql_exec "SELECT coalesce(\"FailureReason\", '') FROM test_runs WHERE \"Id\" = '${run_id}';")
    fixtures=$(psql_exec "SELECT count(*) FROM test_fixture_results WHERE \"TestRunId\" = '${run_id}';" | tr -d '[:space:]')
    units=$(psql_exec "
        SELECT count(*) FROM test_unit_results u
        JOIN test_fixture_results f ON f.\"Id\" = u.\"FixtureId\"
        WHERE f.\"TestRunId\" = '${run_id}';" | tr -d '[:space:]')

    printf '\n'
    printf '  run          %s\n' "${run_id}"
    printf '  status       %s (3=Completed 4=Failed 5=Cancelled)\n' "${status}"
    printf '  fixtures     %s\n' "${fixtures}"
    printf '  unit results %s\n' "${units}"
    [[ -n "${reason//[[:space:]]/}" ]] && printf '  reason       %s\n' "${reason}"

    psql_exec "
        SELECT '  fixture      ' || f.\"Name\" || '  kind=' || f.\"Kind\"
               || '  run=' || f.\"TestsRun\" || ' passed=' || f.\"TestsPassed\" || ' failed=' || f.\"TestsFailed\"
               || coalesce('  quality=' || round(f.\"Quality\"::numeric, 4), '')
        FROM test_fixture_results f WHERE f.\"TestRunId\" = '${run_id}' ORDER BY f.\"StartedAt\";"

    # Aspect plumbing is the newest part of the chain, so surface it explicitly.
    psql_exec "
        SELECT '  aspect       ' || u.\"Aspect\" || '  visible=' || u.\"AspectCompetitorVisible\"
               || '  outcome=' || u.\"Outcome\"
        FROM test_unit_results u
        JOIN test_fixture_results f ON f.\"Id\" = u.\"FixtureId\"
        WHERE f.\"TestRunId\" = '${run_id}' AND u.\"Aspect\" IS NOT NULL
        ORDER BY u.\"Aspect\";"
    printf '\n'

    (( fixtures > 0 )) || die "the run produced no fixtures - the event log was not parsed"
    [[ "${status}" == "3" ]] || die "expected status 3 (Completed), got ${status}: ${reason}"

    ok "run ${run_id} Completed with ${fixtures} fixture(s) and ${units} unit result(s)"

    # The staff log download is a separate code path from parsing, and it is how an expert investigates a
    # disputed result.
    if curl -sf -o /dev/null "${APP_URL}/api/test-runs/${run_id}/log"; then
        ok "staff log download reachable"
    else
        warn "log download returned non-success (it requires an authenticated staff session)"
    fi
}

# ---------------------------------------------------------------------------- orchestration

# Opt-in: E2E_SUPERSEDE=1. Two pushes back to back should leave at most one run standing for the competitor.
step_supersede() {
    log "Checking supersede on a rapid double push"

    step_push
    local first=${PUSHED_COMMIT}
    step_push
    local second=${PUSHED_COMMIT}

    local waited=0 second_status=""
    while (( waited < 300 )); do
        second_status=$(psql_exec "
            SELECT coalesce((SELECT \"Status\"::text FROM test_runs WHERE \"CommitSha\" = '${second}' LIMIT 1), '');" \
            | tr -d '[:space:]')
        [[ "${second_status}" =~ ^(3|4|5)$ ]] && break
        sleep 3
        waited=$((waited + 3))
    done

    local first_status
    first_status=$(psql_exec "
        SELECT coalesce((SELECT \"Status\"::text FROM test_runs WHERE \"CommitSha\" = '${first}' LIMIT 1), '');" \
        | tr -d '[:space:]')

    printf '  first  %s -> status %s\n' "${first:0:8}" "${first_status:-<no run>}"
    printf '  second %s -> status %s\n' "${second:0:8}" "${second_status:-<no run>}"

    # Deliberately not asserting "the first is always Cancelled": with a fast judge the first run can finish
    # before the second push arrives, and a test that fails on a lost race teaches people to ignore it. What
    # must hold is that the first run does not sit in a non-terminal state forever.
    case "${first_status}" in
        5) ok "the first run was superseded (Cancelled), as intended under contention" ;;
        3|4) warn "the first run finished before the second push arrived - supersede not exercised this time" ;;
        "") warn "no run recorded for the first push" ;;
        *) die "the first run is stuck in non-terminal status ${first_status}" ;;
    esac

    [[ "${second_status}" =~ ^(3|4|5)$ ]] \
        || die "the second run never reached a terminal state (status ${second_status:-<none>})"
    ok "the second run reached a terminal state"
}

main() {
    log "E2E: image=${JUDGE_IMAGE} competitor=${COMPETITOR}"
    step_infra
    step_gitea
    step_records

    if [[ "${E2E_SUPERSEDE:-0}" == "1" ]]; then
        step_supersede
        return
    fi

    step_push
    step_assert
    printf '\n'
    ok "end-to-end chain verified: push -> webhook -> clone -> judge -> parse -> database"
    printf '     app UI: %s   Gitea: %s (%s / %s)\n' \
        "${APP_URL}" "${GITEA_URL}" "${GITEA_ADMIN_USER}" "${GITEA_ADMIN_PASSWORD}"
}

main "$@"
