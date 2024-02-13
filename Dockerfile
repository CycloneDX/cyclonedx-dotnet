FROM mcr.microsoft.com/dotnet/sdk:8.0.101

ARG VERSION
COPY ./nupkgs /tmp/nupkgs/
RUN dotnet tool install --global CycloneDX --version ${VERSION} --add-source /tmp/nupkgs && \
    ln -s /root/.dotnet/tools/dotnet-CycloneDX /usr/bin/CycloneDX

ENTRYPOINT [ "CycloneDX" ]
CMD [ "--help" ]
