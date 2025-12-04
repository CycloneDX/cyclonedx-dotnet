# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy necessary files for restore
COPY ["CycloneDX.sln", "nuget.config", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "./"]
COPY ["CycloneDX/CycloneDX.csproj", "CycloneDX/"]
COPY ["CycloneDX.Tests/CycloneDX.Tests.csproj", "CycloneDX.Tests/"]

RUN dotnet restore

COPY . .

# Build and Publish
RUN dotnet publish "CycloneDX/CycloneDX.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:Version=$(cat semver.txt) -f net10.0

# Runtime Stage (SDK is required because cyclonedx calls the dotnet cli)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS final

WORKDIR /work

ENV DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=build /app/publish /app

ENTRYPOINT ["dotnet", "/app/CycloneDX.dll"]
CMD [ "--help" ]