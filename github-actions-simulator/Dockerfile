# GitHub Actions Simulator - Multi-stage build
# Base image with act runner
FROM catthehacker/ubuntu:full-latest AS base

# Install additional tools
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    git \
    jq \
    python3 \
    python3-pip \
    docker.io \
    postgresql-client \
    redis-tools \
    && rm -rf /var/lib/apt/lists/*

# Install .NET 9 SDK
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm dotnet-install.sh

# Install Node.js 20 and pnpm
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g pnpm@9

# Install act (GitHub Actions runner)
RUN curl -s https://raw.githubusercontent.com/nektos/act/master/install.sh | bash

# Install k6 for load testing
RUN gpg -k \
    && gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg \
    --keyserver hkp://keyserver.ubuntu.com:80 \
    --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69 \
    && echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | \
    tee /etc/apt/sources.list.d/k6.list \
    && apt-get update \
    && apt-get install k6 \
    && rm -rf /var/lib/apt/lists/*

# Install Semgrep for SAST
RUN pip3 install semgrep

# Install actionlint for workflow validation
RUN wget -O - https://raw.githubusercontent.com/rhysd/actionlint/main/scripts/download-actionlint.bash | bash

# Create working directories
RUN mkdir -p /workspace /logs /artifacts /cache /.act

# Set working directory
WORKDIR /workspace

# Copy helper scripts
COPY scripts/ /usr/local/bin/

# Make scripts executable
RUN chmod +x /usr/local/bin/*.sh

# Environment variables
ENV CI=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    ACT_LOG_DIR=/logs \
    ACT_ARTIFACT_DIR=/artifacts \
    ACT_CACHE_DIR=/cache

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD act --version && dotnet --version && node --version || exit 1

# Default command
CMD ["/bin/bash"]
