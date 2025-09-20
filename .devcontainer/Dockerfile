FROM mcr.microsoft.com/dotnet/sdk:9.0

# Create a non-root user
ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID

# Install additional packages
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends \
        curl \
        git \
        sudo \
        vim \
        wget \
        zip \
        unzip \
        procps \
        lsb-release

# Create the user
RUN groupadd --gid $USER_GID $USERNAME \
    && useradd --uid $USER_UID --gid $USER_GID -m $USERNAME \
    && echo $USERNAME ALL=\(root\) NOPASSWD:ALL > /etc/sudoers.d/$USERNAME \
    && chmod 0440 /etc/sudoers.d/$USERNAME

# Set the default user
USER $USERNAME

# Install useful .NET tools
RUN dotnet tool install --global dotnet-ef \
    && dotnet tool install --global dotnet-aspnet-codegenerator \
    && dotnet tool install --global dotnet-dump \
    && dotnet tool install --global dotnet-trace

# Add .NET tools to PATH
ENV PATH="$PATH:/home/$USERNAME/.dotnet/tools"

# Set the working directory
WORKDIR /workspace