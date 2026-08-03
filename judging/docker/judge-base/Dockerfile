# syntax=docker/dockerfile:1.6
#
# judge-base - the shared foundation for every Skill.Suite judgement image.
#
# Build:
#   docker build -t ghcr.io/bencelakos/skill-suite-judge-base:9.0 judging/docker/judge-base
#
# The full SDK, not the runtime image: `dotnet test` needs it, and so does the offline restore.
FROM mcr.microsoft.com/dotnet/sdk:9.0

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    # Defaults matching what the platform injects, so `docker run` without them still behaves.
    COMPETITOR_DIRECTORY=/workspace \
    LOG_DIRECTORY=/var/log/skill-suite \
    JUDGE_APP_DIR=/app \
    JUDGE_TIMEOUT_SECONDS=300 \
    JUDGE_MUTATION_TIMEOUT_SECONDS=900 \
    JUDGE_FAIL_ON_RED=false

COPY judge-lib.sh /usr/local/lib/judge-lib.sh
COPY entrypoint.sh /usr/local/bin/judge-entrypoint.sh
COPY judge-selftest.sh /usr/local/bin/judge-selftest.sh

RUN chmod 0755 /usr/local/bin/judge-entrypoint.sh /usr/local/bin/judge-selftest.sh \
    && chmod 0644 /usr/local/lib/judge-lib.sh \
    && mkdir -p /app /var/log/skill-suite

# WORKDIR is documentation only: the platform passes `-w /workspace`, which overrides it - and that path is
# read-only. Every step in judge-lib.sh cds to $JUDGE_APP_DIR itself.
WORKDIR /app

ENTRYPOINT ["/usr/local/bin/judge-entrypoint.sh"]
