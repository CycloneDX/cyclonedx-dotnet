FROM gitpod/openvscode-server

USER root
# Install .NET runtime dependencies
RUN apt-get update \
    && apt-get install -y \
        libc6 \
        libgcc1 \
        libgssapi-krb5-2 \
        libicu60 \
        libssl1.1 \
        libstdc++6 \
        zlib1g \
    && rm -rf /var/lib/apt/lists/*

USER vscode-server

# Install .NET SDK
# Source: https://docs.microsoft.com/dotnet/core/install/linux-scripted-manual#scripted-install
RUN mkdir -p /home/vscode/dotnet \
    && wget --output-document=/home/vscode/dotnet/dotnet-install.sh https://dot.net/v1/dotnet-install.sh \
    && chmod +x /home/vscode/dotnet/dotnet-install.sh

RUN /home/vscode/dotnet/dotnet-install.sh --channel 2.1 --install-dir /home/vscode/dotnet
RUN /home/vscode/dotnet/dotnet-install.sh --channel 3.1 --install-dir /home/vscode/dotnet
RUN /home/vscode/dotnet/dotnet-install.sh --channel 5.0 --install-dir /home/vscode/dotnet

ENV DOTNET_ROOT=/home/vscode/dotnet
ENV PATH=$PATH:/home/vscode/dotnet
