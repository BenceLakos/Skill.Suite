# syntax=docker/dockerfile:1.6
#
# judge-smoke - fixed-output judgement image for verifying platform wiring.
#
# Build from the judging/ directory, because it compiles Skill.Suite.TestLog from source:
#   docker build -f judging/docker/judge-smoke/Dockerfile -t ghcr.io/bencelakos/skill-suite-judge-smoke:1 judging
#
# Runs on the runtime image, not the SDK: it compiles nothing at run time, and a wiring check should be fast
# to pull.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip

WORKDIR /src

COPY Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY Skill.Suite.TestLog/ Skill.Suite.TestLog/
COPY docker/judge-smoke/ docker/judge-smoke/

# global.json is deliberately NOT copied: it pins 9.0.202, which the image may not carry.
RUN dotnet publish docker/judge-smoke/Skill.Suite.JudgeSmoke.csproj \
        -c Release -o /out --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:9.0

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    COMPETITOR_DIRECTORY=/workspace \
    LOG_DIRECTORY=/var/log/skill-suite

COPY --from=build /out /app

RUN mkdir -p /var/log/skill-suite

# No WORKDIR reliance: the platform passes `-w /workspace`, which is read-only. The app writes only to
# $LOG_DIRECTORY, so an absolute entrypoint is all it needs.
ENTRYPOINT ["dotnet", "/app/Skill.Suite.JudgeSmoke.dll"]
