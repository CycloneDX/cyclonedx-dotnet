# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy necessary files for restore
COPY ["CycloneDX.sln", "nuget.config", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "./"]
COPY ["CycloneDX/CycloneDX.csproj", "CycloneDX/"]
COPY ["CycloneDX.Tests/CycloneDX.Tests.csproj", "CycloneDX.Tests/"]
COPY ["CycloneDX.E2ETests/CycloneDX.E2ETests.csproj", "CycloneDX.E2ETests/"]

RUN dotnet restore

COPY . .

# Build and Publish
RUN dotnet publish "CycloneDX/CycloneDX.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:Version=$(cat semver.txt) -f net10.0

# Runtime Stage (SDK is required because cyclonedx calls the dotnet cli)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS final

WORKDIR /work

ENV DOTNET_CLI_HOME=/tmp/dotnet-home \
    NUGET_PACKAGES=/tmp/nuget-packages \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN mkdir -p /tmp/dotnet-home /tmp/nuget-packages \
    && chmod -R 1777 /tmp/dotnet-home /tmp/nuget-packages

COPY --from=build /app/publish /app

ENTRYPOINT ["dotnet", "/app/CycloneDX.dll"]
CMD [ "--help" ]