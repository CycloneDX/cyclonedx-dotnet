#!/bin/sh

VERSION=`cat semver.txt`
OUTPUT=./nupkgs

rm -f -R $OUTPUT

dotnet clean
dotnet restore
dotnet test
# boolean short circuit to exit if test failures
[ $? -eq 0 ] || exit 1
dotnet build --configuration Release
dotnet pack CycloneDX\\CycloneDX.csproj --configuration Release --version-suffix $VERSION --output $OUTPUT
# boolean short circuit to exit if previous failures
[ $? -eq 0 ] || exit 1

# Cleanup containers/images, build new image and push to Docker Hub
REPO=cyclonedx/cyclonedx-dotnet
docker rm cyclonedx-dotnet
docker rmi $REPO:latest
docker rmi $REPO:$VERSION
docker build -f Dockerfile --build-arg VERSION=$VERSION -t $REPO:$VERSION -t $REPO:latest .
