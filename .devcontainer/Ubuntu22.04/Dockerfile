FROM ubuntu:22.04

RUN apt-get install -y --no-install-recommends wget=2.0.1
RUN apt-get install -y --no-install-recommends apt-transport-https=2.5.6
RUN curl -o ./packages-microsoft-prod.deb https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get install -y --no-install-recommends dotnet-sdk-8.0=8.0
RUN rm -rf /var/lib/apt/lists/*
