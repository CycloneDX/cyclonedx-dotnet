FROM ubuntu:22.04

RUN apt-get update && apt-get install -y software-properties-common \
  && add-apt-repository -y ppa:dotnet/backports \
  && apt-get update

RUN apt-get install -y dotnet-sdk-10.0 && \
    rm -rf /var/lib/apt/lists/*
