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
