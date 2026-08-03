# syntax=docker/dockerfile:1.6
#
# White-box judge image for SkillSuite.Session (S1, S3).
#
# The competitor implements SkillSuite.Session.Services; the hidden suite is baked in. At run time their
# folder replaces the placeholder and the suite runs against it. Each aspect's tests pass or fail; this image
# computes no marks.
#
#   ./pack-contracts.sh && ./build-image.sh
#
# Nothing session-specific is passed on the command line. The names below are this module's own, so they are
# declared here rather than as build args a caller has to remember and can get wrong. Only BASE_IMAGE remains
# an argument, because that is about the environment, not the session.
#
# This image contains the hidden tests. Private registry only, immutable tags only, never public CI.

ARG BASE_IMAGE=JUDGE_BASE_IMAGE_DEFAULT

# ---------------------------------------------------------------------------- build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip

WORKDIR /src

# Restore from csproj files only, so editing a test does not re-restore.
# nuget.config points at ./local-nuget, so the module's own packages resolve without network access.
COPY SkillSuite.Session.sln ./
COPY nuget.config ./
COPY local-nuget/ local-nuget/
COPY */*.csproj ./
# `COPY */*.csproj` flattens, so put each csproj back beside its project.
RUN for proj in *.csproj; do \
        dir="${proj%.csproj}"; \
        mkdir -p "$dir"; \
        mv "$proj" "$dir/"; \
    done

# No --source flags: those *replace* the nuget.config sources, and the build still needs nuget.org for the
# test SDK and xunit. /root/.nuget/packages also does not exist yet in a fresh SDK image, which NuGet reports
# as NU1301. The offline lockdown belongs at run time, where restore_offline passes explicit sources to stop a
# submission adding a package reference.
RUN dotnet restore "SkillSuite.Session.sln"

# Only the stable sources. The reference implementation is excluded by .dockerignore and never copied - the
# placeholder folder keeps just its csproj so the restore graph stays intact.
COPY . .

# Prebuild the contracts so a run only compiles the swapped folder and the tests.
RUN dotnet build "SkillSuite.Session.Contracts/SkillSuite.Session.Contracts.csproj" -c Release --no-restore

# Fail the build - not the run - if the answer key leaked in. Cheaper to catch here than to discover a
# competitor found the reference implementation inside an image they were told to pull.
RUN if find "/src/SkillSuite.Session.Services" -name '*.cs' -print -quit | grep -q .; then \
        echo "FIREWALL: reference sources found in SkillSuite.Session.Services; check .dockerignore" >&2; \
        exit 1; \
    fi \
    && if [ -d /src/_design ]; then \
        echo "FIREWALL: _design/ is present in the image; check .dockerignore" >&2; exit 1; \
    fi

# ---------------------------------------------------------------------------- runtime

FROM ${BASE_IMAGE}

# The judge contract, read by judge-lib.sh. Baked in, because these are facts about this module.
ENV JUDGE_SOLUTION=SkillSuite.Session.sln \
    JUDGE_SWAP_SRC=SkillSuite.Session.Services \
    JUDGE_TEST_PROJECT=SkillSuite.Session.UnitTests/SkillSuite.Session.UnitTests.csproj \
    JUDGE_APP_DIR=/app \
    # White-box: red tests are a result, not a failed run. Partial credit depends on this.
    JUDGE_FAIL_ON_RED=false

WORKDIR /app

COPY --from=build /src/ /app/
# The warmed package cache is what makes the run-time restore work with no network.
COPY --from=build /root/.nuget/packages/ /root/.nuget/packages/

# Entrypoint inherited from judge-base: swap_dir -> restore_offline -> build_tests -> run_tests -> assert
