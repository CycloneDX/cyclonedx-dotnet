FROM ubuntu:22.04

RUN apt-get update \
  && apt-get install -y --no-install-recommends software-properties-common \
  && add-apt-repository -y ppa:dotnet/backports \
  && apt-get update \
  && apt-get install -y --no-install-recommends dotnet-sdk-10.0 \
  && rm -rf /var/lib/apt/lists/*
