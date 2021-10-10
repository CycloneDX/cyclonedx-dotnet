#!/usr/bin/env bash

docker build --tag cyclonedx-dotnet-development --file .localdev.Dockerfile .
docker run -it --init -p 3000:3000 -v "$(pwd):/home/workspace:cached" cyclonedx-dotnet-development