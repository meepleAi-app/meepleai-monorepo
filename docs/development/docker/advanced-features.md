# Docker Advanced Features

**Last Updated**: 2026-02-02

Advanced Docker Compose features for power users: custom profiles, overrides, optimizations, and IDE integrations.

---

## Table of Contents

1. [Custom Docker Compose Overrides](#custom-docker-compose-overrides)
2. [Docker Profiles Customization](#docker-profiles-customization)
3. [Build Optimizations](#build-optimizations)
4. [Layer Caching Strategies](#layer-caching-strategies)
5. [Multi-Stage Builds](#multi-stage-builds)
6. [VS Code Integration](#vs-code-integration)
7. [JetBrains Rider Integration](#jetbrains-rider-integration)
8. [Performance Tuning](#performance-tuning)
9. [Production Best Practices](#production-best-practices)
10. [CI/CD Integration](#cicd-integration)

---

## Custom Docker Compose Overrides

### What Are Overrides?

Docker Compose automatically merges `docker-compose.yml` with `docker-compose.override.yml` if it exists. This allows **per-developer customization** without modifying the main compose file.

### Create Personal Override

**File**: `infra/docker-compose.override.yml` (gitignored)

```yaml
# Personal development overrides
services:
  api:
    # Custom port mapping
    ports:
      - "8081:8080"  # Avoid conflict with other projects

    # Mount local code for hot reload
    volumes:
      - ../apps/api/src/Api:/app:ro

    # Override environment variables
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      Logging__LogLevel__Default: Debug

  web:
    # Custom Next.js config
    environment:
      NEXT_PUBLIC_API_BASE: http://localhost:8081

    # Hot reload with volume mount
    volumes:
      - ../apps/web:/app
      - /app/node_modules  # Exclude node_modules
      - /app/.next         # Exclude build artifacts

  postgres:
    # Expose PostgreSQL to host for GUI tools
    ports:
      - "5433:5432"  # Different host port

  # Disable heavy services for development
  ollama:
    profiles:
      - disabled  # Won't start with any profile

  smoldocling-service:
    profiles:
      - disabled
```

**Usage**:
```bash
# Automatically uses overrides if file exists
docker compose up -d

# Explicitly specify override
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d

# Ignore override
docker compose -f docker-compose.yml up -d
```

### Multiple Override Files

**Scenario**: Different overrides for different environments

```bash
# Development overrides
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d

# Staging overrides
docker compose -f docker-compose.yml -f docker-compose.staging.yml up -d

# Production overrides
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Combine multiple overrides
docker compose -f docker-compose.yml -f docker-compose.dev.yml -f docker-compose.hyperdx.yml up -d
```

### Common Override Patterns

#### 1. Custom Resource Limits

```yaml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '4.0'      # More CPU for development
          memory: 8G       # More RAM for profiling
```

#### 2. Debug Ports

```yaml
services:
  api:
    ports:
      - "5000:5000"  # .NET debugger port
    environment:
      DOTNET_DEBUGGER_PORT: 5000

  web:
    ports:
      - "9229:9229"  # Node.js inspector
    command: ["npm", "run", "dev", "--", "--inspect=0.0.0.0:9229"]
```

#### 3. Local File Mounts

```yaml
services:
  api:
    volumes:
      # Mount appsettings for live config changes
      - ../apps/api/src/Api/appsettings.Development.json:/app/appsettings.Development.json:ro

      # Mount logs directory
      - ./logs:/app/logs
```

#### 4. Custom Networks

```yaml
services:
  api:
    networks:
      - meepleai
      - other-project-network  # Share network with another project

networks:
  other-project-network:
    external: true
```

---

## Docker Profiles Customization

### Create Custom Profiles

**File**: `docker-compose.override.yml`

```yaml
# Define custom profile: "my-workflow"
services:
  api:
    profiles:
      - minimal
      - my-workflow  # Add to custom profile

  web:
    profiles:
      - minimal
      - my-workflow

  postgres:
    profiles:
      - minimal
      - my-workflow

  redis:
    profiles:
      - minimal
      - my-workflow

  mailpit:
    profiles:
      - my-workflow  # Include email testing

  grafana:
    profiles:
      - my-workflow  # Include monitoring
```

**Usage**:
```bash
docker compose --profile my-workflow up -d
```

### Exclude Services from Profiles

```yaml
# Disable service in all profiles
services:
  ollama:
    profiles:
      - never  # Custom profile that's never used
```

### Profile Aliases

**File**: `infra/scripts/profiles.sh`

```bash
#!/bin/bash

# Profile aliases for convenience
alias dc-minimal="docker compose --profile minimal up -d"
alias dc-dev="docker compose --profile dev up -d"
alias dc-ai="docker compose --profile ai up -d"
alias dc-full="docker compose --profile full up -d"
alias dc-stop="docker compose down"

# Custom combinations
alias dc-backend="docker compose up -d postgres qdrant redis api"
alias dc-frontend="docker compose up -d api web"
alias dc-ml="docker compose --profile ai up -d"
```

**PowerShell Version** (`infra/scripts/profiles.ps1`):
```powershell
# Docker Compose profile aliases
function dc-minimal { docker compose --profile minimal up -d }
function dc-dev { docker compose --profile dev up -d }
function dc-ai { docker compose --profile ai up -d }
function dc-full { docker compose --profile full up -d }
function dc-stop { docker compose down }

# Custom combinations
function dc-backend { docker compose up -d postgres qdrant redis api }
function dc-frontend { docker compose up -d api web }
```

**Usage**:
```bash
# Linux/Mac
source infra/scripts/profiles.sh
dc-dev  # Starts dev profile

# Windows
. infra/scripts/profiles.ps1
dc-dev  # Starts dev profile
```

---

## Build Optimizations

### Enable Docker BuildKit

**BuildKit** = Faster builds, better caching, parallel builds

**Linux/Mac**:
```bash
export DOCKER_BUILDKIT=1
docker compose build
```

**Windows PowerShell**:
```powershell
$env:DOCKER_BUILDKIT=1
docker compose build
```

**Permanent** (add to `~/.bashrc` or PowerShell profile):
```bash
# ~/.bashrc
export DOCKER_BUILDKIT=1
```

```powershell
# PowerShell profile: $PROFILE
$env:DOCKER_BUILDKIT=1
```

### Parallel Builds

```bash
# Build multiple services in parallel
docker compose build --parallel

# Limit parallelism
docker compose build --parallel 4  # Max 4 concurrent builds
```

### Build with Progress

```bash
# Plain progress (default)
docker compose build

# Tty progress (better for terminals)
docker compose build --progress tty

# Plain text progress (for CI)
docker compose build --progress plain
```

### Target Specific Build Stages

**Dockerfile** (multi-stage example):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY . .
RUN dotnet build

FROM build AS test
RUN dotnet test

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
COPY --from=build /app/bin/Release /app
ENTRYPOINT ["dotnet", "Api.dll"]
```

**Build specific stage**:
```bash
# Build only 'build' stage (faster for dev)
docker compose build --target build api

# Build and run tests
docker compose build --target test api
```

---

## Layer Caching Strategies

### Optimize Dockerfile Layer Order

**❌ Bad** (cache breaks on any code change):
```dockerfile
FROM node:20-alpine
WORKDIR /app
COPY . .                    # Cache breaks here on any file change
RUN npm install             # Always re-runs
RUN npm run build
```

**✅ Good** (cache preserved for dependencies):
```dockerfile
FROM node:20-alpine
WORKDIR /app
COPY package*.json ./       # Only copies package files
RUN npm install             # Cached unless package.json changes
COPY . .                    # Code changes don't break dependency cache
RUN npm run build
```

### Use .dockerignore

**File**: `apps/api/.dockerignore`
```
# Exclude unnecessary files from build context
bin/
obj/
*.log
*.md
.git/
.vs/
.vscode/
tests/
docs/
```

**File**: `apps/web/.dockerignore`
```
node_modules/
.next/
.git/
.vscode/
*.log
*.md
tests/
docs/
README.md
```

**Benefits**:
- Faster build context transfer
- Smaller build context size
- Better layer caching

### Cache Mounts (BuildKit)

**Dockerfile** (with BuildKit cache mounts):
```dockerfile
# syntax=docker/dockerfile:1
FROM node:20-alpine
WORKDIR /app

# Cache npm packages
RUN --mount=type=cache,target=/root/.npm \
    npm install

# Cache build artifacts
RUN --mount=type=cache,target=/app/.next/cache \
    npm run build
```

**Build with cache**:
```bash
DOCKER_BUILDKIT=1 docker compose build
```

---

## Multi-Stage Builds

### Backend Multi-Stage (Smaller Images)

**File**: `apps/api/Dockerfile`
```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Api.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime (smaller image)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]

# Image sizes:
# sdk:9.0 ~ 1.2 GB
# aspnet:9.0 ~ 200 MB  ✅ Much smaller!
```

### Frontend Multi-Stage

**File**: `apps/web/Dockerfile`
```dockerfile
# Stage 1: Dependencies
FROM node:20-alpine AS deps
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production

# Stage 2: Build
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Stage 3: Runtime
FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
COPY --from=deps /app/node_modules ./node_modules
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
COPY package*.json ./
EXPOSE 3000
CMD ["npm", "start"]
```

### Benefits of Multi-Stage

- ✅ **Smaller images**: Runtime images exclude build tools
- ✅ **Security**: Fewer attack surfaces (no build tools in production)
- ✅ **Faster deployment**: Smaller images transfer faster
- ✅ **Better caching**: Stages can be cached independently

---

## VS Code Integration

### Docker Extension

**Install**: [Docker Extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-docker)

**Features**:
- View containers, images, volumes, networks
- Start/stop containers with GUI
- View logs in VS Code
- Attach shell to containers
- Inspect container filesystem

**Usage**:
1. Install extension
2. Click Docker icon in sidebar
3. Right-click services → Start/Stop/Logs/Shell

### Dev Containers

**File**: `.devcontainer/devcontainer.json`
```json
{
  "name": "MeepleAI Development",
  "dockerComposeFile": ["../infra/docker-compose.yml"],
  "service": "api",
  "workspaceFolder": "/app",
  "settings": {
    "terminal.integrated.shell.linux": "/bin/bash"
  },
  "extensions": [
    "ms-dotnettools.csharp",
    "esbenp.prettier-vscode",
    "dbaeumer.vscode-eslint"
  ],
  "forwardPorts": [8080, 3000, 5432],
  "postCreateCommand": "dotnet restore"
}
```

**Usage**:
1. Install "Dev Containers" extension
2. `Ctrl+Shift+P` → "Reopen in Container"
3. VS Code runs inside Docker container

### Debugging in Docker

**File**: `.vscode/launch.json`
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Docker: Attach to .NET",
      "type": "coreclr",
      "request": "attach",
      "processId": "${command:pickRemoteProcess}",
      "pipeTransport": {
        "pipeCwd": "${workspaceFolder}",
        "pipeProgram": "docker",
        "pipeArgs": ["exec", "-i", "meepleai-api"],
        "debuggerPath": "/vsdbg/vsdbg",
        "quoteArgs": false
      },
      "sourceFileMap": {
        "/app": "${workspaceFolder}/apps/api/src/Api"
      }
    }
  ]
}
```

---

## JetBrains Rider Integration

### Docker Run Configuration

**Setup**:
1. Run → Edit Configurations
2. Add New → Docker → Docker Compose
3. Configuration:
   - **Compose files**: `infra/docker-compose.yml`
   - **Service**: `api`
   - **Before launch**: Build project

### Remote Debugging

**Setup**:
1. Run → Edit Configurations
2. Add New → .NET Remote
3. Configuration:
   - **Host**: `localhost`
   - **Port**: `5000` (debugger port)
   - **Attach to process**: `Api.dll`

**docker-compose.override.yml**:
```yaml
services:
  api:
    environment:
      DOTNET_DEBUGGER_PORT: 5000
    ports:
      - "5000:5000"
    entrypoint: ["/bin/bash", "-c", "sleep 10 && dotnet Api.dll"]
```

### Database Tools

**Connect to PostgreSQL in Docker**:
1. View → Tool Windows → Database
2. Add → Data Source → PostgreSQL
3. Configuration:
   - **Host**: `localhost`
   - **Port**: `5432`
   - **Database**: `meepleai`
   - **User**: From `infra/secrets/database.secret`
   - **Password**: From `infra/secrets/database.secret`

---

## Performance Tuning

### Docker Desktop Settings

**Windows/Mac**:
1. Docker Desktop → Settings → Resources
2. Adjust:
   - **CPUs**: 50-75% of system cores
   - **Memory**: 50-75% of system RAM
   - **Swap**: 2-4 GB
   - **Disk Image Size**: 100+ GB for full stack

**Recommended Allocations**:
- **Development**: 8 GB RAM, 4 CPUs
- **AI Development**: 16 GB RAM, 6 CPUs
- **Full Stack**: 24 GB RAM, 8 CPUs

### File Sharing Performance

**Windows** (use WSL 2 for better performance):
1. Docker Desktop → Settings → General
2. Enable "Use the WSL 2 based engine"
3. Clone repo in WSL filesystem: `/home/user/projects`

**Mac** (use VirtioFS for better I/O):
1. Docker Desktop → Settings → General
2. Enable "VirtioFS" file sharing

### Build Cache Configuration

**File**: `infra/docker-compose.yml`
```yaml
x-build-cache: &build-cache
  cache_from:
    - type=local,src=/tmp/.buildx-cache
  cache_to:
    - type=local,dest=/tmp/.buildx-cache

services:
  api:
    build:
      context: ../apps/api
      <<: *build-cache  # Use build cache
```

---

## Production Best Practices

### Environment-Specific Configs

**Production compose** (`infra/docker-compose.prod.yml`):
```yaml
services:
  api:
    restart: always  # Auto-restart on failure
    logging:
      driver: "json-file"
      options:
        max-size: "50m"
        max-file: "10"
    deploy:
      resources:
        limits:
          cpus: '4.0'
          memory: 8G
        reservations:
          cpus: '2.0'
          memory: 4G
    healthcheck:
      interval: 30s
      timeout: 10s
      retries: 5
```

### Security Hardening

```yaml
services:
  api:
    # Run as non-root user
    user: "1000:1000"

    # Read-only root filesystem
    read_only: true

    # Drop capabilities
    cap_drop:
      - ALL
    cap_add:
      - NET_BIND_SERVICE

    # Security options
    security_opt:
      - no-new-privileges:true
```

### Secrets Management

**Use Docker secrets instead of env vars in production**:
```yaml
services:
  api:
    secrets:
      - postgres_password
      - jwt_secret
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/postgres_password
      JWT_SECRET_FILE: /run/secrets/jwt_secret

secrets:
  postgres_password:
    file: ./secrets/prod/postgres-password.txt
  jwt_secret:
    file: ./secrets/prod/jwt-secret.txt
```

---

## CI/CD Integration

### GitHub Actions Example

**File**: `.github/workflows/docker-build.yml`
```yaml
name: Docker Build & Test

on:
  push:
    branches: [main-dev, main]
  pull_request:
    branches: [main-dev, main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Build services
        run: |
          cd infra
          docker compose build --parallel

      - name: Start services
        run: |
          cd infra
          docker compose --profile minimal up -d

      - name: Wait for services
        run: |
          timeout 60 bash -c 'until docker compose ps | grep "(healthy)"; do sleep 2; done'

      - name: Run health checks
        run: |
          curl -f http://localhost:8080/health
          curl -f http://localhost:3000/

      - name: Run tests
        run: |
          docker compose exec -T api dotnet test
          docker compose exec -T web npm test

      - name: Collect logs
        if: failure()
        run: |
          docker compose logs > docker-logs.txt

      - name: Upload logs
        if: failure()
        uses: actions/upload-artifact@v3
        with:
          name: docker-logs
          path: docker-logs.txt
```

---

## Additional Resources

- **Quick Start**: [quick-start.md](./quick-start.md)
- **Service Endpoints**: [service-endpoints.md](./service-endpoints.md)
- **Clean Builds**: [clean-builds.md](./clean-builds.md)
- **Common Commands**: [common-commands.md](./common-commands.md)
- **Troubleshooting**: [troubleshooting.md](./troubleshooting.md)
- **Docker Profiles**: [docker-profiles.md](./docker-profiles.md)
- **Docker Documentation**: https://docs.docker.com/
- **BuildKit Documentation**: https://docs.docker.com/build/buildkit/

---

**Last Updated**: 2026-02-02
**Maintainer**: MeepleAI Development Team
