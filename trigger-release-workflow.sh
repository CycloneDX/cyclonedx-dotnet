#!/bin/sh
# requires the environment variable GITHUB_TOKEN to be set
# (the personal access token requires repo scope)
curl -H "Accept: application/vnd.github.everest-preview+json" \
    -H "Authorization: token $GITHUB_TOKEN" \
    --request POST \
    --data '{"event_type": "release"}' \
    https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/dispatches