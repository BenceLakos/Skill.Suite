FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Run as root in the runtime stage so the bind-mounted /var/run/docker.sock is
# accessible. git and docker are required by ProcessGitClient / ProcessContainerRunner,
# which shell out to the CLIs to clone submissions and run judgement containers.
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        git \
        docker.io \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Skill.Suite/Skill.Suite.csproj", "Skill.Suite/"]
COPY ["Skill.Suite.Application/Skill.Suite.Application.csproj", "Skill.Suite.Application/"]
COPY ["Skill.Suite.Infra/Skill.Suite.Infra.csproj", "Skill.Suite.Infra/"]
COPY ["Skill.Suite.Domain/Skill.Suite.Domain.csproj", "Skill.Suite.Domain/"]
RUN dotnet restore "Skill.Suite/Skill.Suite.csproj"
COPY . .
WORKDIR "/src/Skill.Suite"
RUN dotnet build "./Skill.Suite.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Skill.Suite.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Skill.Suite.dll"]
