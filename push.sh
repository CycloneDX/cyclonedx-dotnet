#!/bin/sh

VERSION=`cat semver.txt`
OUTPUT=./nupkgs

dotnet nuget push --source https://api.nuget.org/v3/index.json "./CycloneDX/$OUTPUT/CycloneDX.$VERSION.nupkg"
