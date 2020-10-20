FROM ubuntu:20.04

RUN apt-get update \
    && apt-get install -y \
        wget \
        apt-transport-https \
    && wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y dotnet-sdk-2.1 \
    && apt-get install -y dotnet-sdk-3.1 \
    && rm -rf /var/lib/apt/lists/*
