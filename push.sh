#!/bin/sh

VERSION=`cat semver.txt`
OUTPUT=./nupkgs

dotnet nuget push --source https://api.nuget.org/v3/index.json "./CycloneDX/$OUTPUT/CycloneDX.$VERSION.nupkg"

REPO=cyclonedx/cyclonedx-dotnet
docker login
docker push $REPO:latest
docker push $REPO:$VERSION