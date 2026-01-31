# Docker Image Versioning & Registry Management

> **Scope**: Complete guide to Docker image versioning, tagging strategies, and GitHub Container Registry usage for MeepleAI
> **Last Updated**: 2026-01-30

---

## Table of Contents

1. [Overview](#overview)
2. [Versioning Strategy](#versioning-strategy)
3. [Tagging Conventions](#tagging-conventions)
4. [GitHub Container Registry](#github-container-registry)
5. [Build Process](#build-process)
6. [Registry Operations](#registry-operations)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)

---

## Overview

### Why Version Docker Images?

- **Reproducibility**: Deploy exact same version across environments
- **Rollback**: Quickly revert to previous working version
- **Traceability**: Know exactly what code is running in production
- **Testing**: Test specific versions before promoting to production
- **Audit**: Track deployments and changes over time

### MeepleAI Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   GitHub Container Registry                  │
│                     ghcr.io/owner/repo                       │
├─────────────────────────────────────────────────────────────┤
│  API Images                    │  Web Images                 │
│  ├─ api:v1.2.3                │  ├─ web:v1.2.3             │
│  ├─ api:latest                │  ├─ web:latest             │
│  ├─ api:staging-20260130-abc  │  ├─ web:staging-20260130   │
│  └─ api:v1.2.2                │  └─ web:v1.2.2             │
└─────────────────────────────────────────────────────────────┘
         │                                    │
         ▼                                    ▼
┌────────────────┐              ┌────────────────────┐
│   Staging      │              │    Production      │
│   Environment  │              │    Environment     │
│                │              │                    │
│  api:staging-* │              │  api:v1.2.3        │
│  web:staging-* │              │  web:v1.2.3        │
└────────────────┘              └────────────────────┘
```

---

## Versioning Strategy

### Semantic Versioning (Production)

MeepleAI follows **Semantic Versioning 2.0.0**: `MAJOR.MINOR.PATCH`

```
v1.2.3
│ │ │
│ │ └─── PATCH: Bug fixes, security patches (backward compatible)
│ └───── MINOR: New features (backward compatible)
└─────── MAJOR: Breaking changes (NOT backward compatible)
```

### Examples

| Version | Type | Description | Trigger |
|---------|------|-------------|---------|
| `v1.0.0` | Major | Initial production release | First production deploy |
| `v1.1.0` | Minor | Add game session feature | New feature complete |
| `v1.1.1` | Patch | Fix auth bug | Hotfix merged |
| `v2.0.0` | Major | New API architecture | Breaking API changes |

### Version Bumping Rules

```bash
# PATCH: Bug fixes, security updates
v1.2.3 → v1.2.4
- Fix JWT token expiration bug
- Security patch for dependency
- Performance optimization (no API change)

# MINOR: New features, backward compatible
v1.2.4 → v1.3.0
- Add game session endpoints
- New PDF processing service
- Enhanced search filters

# MAJOR: Breaking changes
v1.3.0 → v2.0.0
- API v2 with different endpoint structure
- Remove deprecated endpoints
- Database schema redesign requiring migration
```

---

## Tagging Conventions

### Environment-Specific Tags

| Environment | Tag Format | Example | Purpose |
|-------------|------------|---------|---------|
| **Development** | `build` (local only) | N/A | Local development, not pushed |
| **Staging** | `staging-YYYYMMDD-SHA` | `staging-20260130-a1b2c3d` | Pre-production testing |
| **Production** | `v*.*.*` | `v1.2.3` | Production releases |
| **Latest** | `latest` | `latest` | Points to latest production |

### Multiple Tags Strategy

Each production image receives **multiple tags** for flexibility:

```yaml
# Production build creates 2 tags:
ghcr.io/owner/meepleai-monorepo/api:v1.2.3     # Specific version (immutable)
ghcr.io/owner/meepleai-monorepo/api:latest     # Latest production (mutable)

# Staging build creates 2 tags:
ghcr.io/owner/meepleai-monorepo/api:staging-20260130-a1b2c3d  # Specific staging
ghcr.io/owner/meepleai-monorepo/api:staging-latest            # Latest staging
```

### Tag Lifecycle

```
Development (Local)
    │
    ├─ Build: No tag, local image only
    └─ Test: Unit + Integration tests
         │
         ▼
Staging (main-staging branch)
    │
    ├─ Build: staging-YYYYMMDD-SHA
    ├─ Push:  ghcr.io/.../api:staging-20260130-a1b2c3d
    ├─ Tag:   ghcr.io/.../api:staging-latest
    └─ Deploy: Staging environment
         │
         │ (Verification period: 1-7 days)
         ▼
Production (main branch + git tag)
    │
    ├─ Tag:   git tag v1.2.3
    ├─ Build: From main branch
    ├─ Push:  ghcr.io/.../api:v1.2.3
    ├─ Tag:   ghcr.io/.../api:latest
    └─ Deploy: Production environment (after approval)
```

---

## GitHub Container Registry

### Registry Configuration

```yaml
# GitHub Actions environment variables
env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}
  API_IMAGE: ghcr.io/${{ github.repository }}/api
  WEB_IMAGE: ghcr.io/${{ github.repository }}/web
```

### Authentication

**GitHub Actions** (automatic):
```yaml
- name: Login to GitHub Container Registry
  uses: docker/login-action@v3
  with:
    registry: ${{ env.REGISTRY }}
    username: ${{ github.actor }}
    password: ${{ secrets.GITHUB_TOKEN }}
```

**Local Development** (manual):
```bash
# Create GitHub Personal Access Token with packages:read/write
# Settings → Developer settings → Personal access tokens → Tokens (classic)
# Scopes: read:packages, write:packages, delete:packages

# Login
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin

# Test
docker pull ghcr.io/owner/meepleai-monorepo/api:latest
```

### Registry Permissions

| Scope | Access Level | Use Case |
|-------|--------------|----------|
| `read:packages` | Pull images | Deploy to servers |
| `write:packages` | Push images | CI/CD builds |
| `delete:packages` | Delete images | Cleanup old versions |
| `repo` | Repository access | Required for packages |

---

## Build Process

### GitHub Actions Build (Automated)

**Staging Build** (`.github/workflows/deploy-staging.yml`):
```yaml
- name: Generate Version
  id: version
  run: |
    VERSION="staging-$(date +'%Y%m%d')-${GITHUB_SHA::7}"
    echo "version=$VERSION" >> $GITHUB_OUTPUT
    echo "📦 Version: $VERSION"

- name: Build and Push API Image
  uses: docker/build-push-action@v5
  with:
    context: ./apps/api
    file: ./apps/api/src/Api/Dockerfile
    push: true
    tags: |
      ghcr.io/${{ github.repository }}/api:${{ steps.version.outputs.version }}
      ghcr.io/${{ github.repository }}/api:staging-latest
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

**Production Build** (`.github/workflows/deploy-production.yml`):
```yaml
- name: Determine Version
  id: version
  run: |
    if [[ "$GITHUB_REF" == refs/tags/v* ]]; then
      VERSION="${GITHUB_REF#refs/tags/}"      # v1.2.3 from git tag
    else
      VERSION="v$(date +'%Y.%m.%d')-${GITHUB_SHA::7}"  # Fallback
    fi
    echo "version=$VERSION" >> $GITHUB_OUTPUT

- name: Build and Push API Image
  uses: docker/build-push-action@v5
  with:
    context: ./apps/api
    file: ./apps/api/src/Api/Dockerfile
    push: true
    tags: |
      ghcr.io/${{ github.repository }}/api:${{ steps.version.outputs.version }}
      ghcr.io/${{ github.repository }}/api:latest
    build-args: |
      ASPNETCORE_ENVIRONMENT=Production
      VERSION=${{ steps.version.outputs.version }}
```

### Local Build (Manual)

```bash
# Development build (no push)
cd apps/api
docker build -t meepleai-api:dev -f src/Api/Dockerfile .

# Test build
docker run --rm meepleai-api:dev

# Build with version tag (for testing registry)
docker build \
  -t ghcr.io/owner/meepleai-monorepo/api:test-$(date +%Y%m%d) \
  -f src/Api/Dockerfile .

# Push to registry (requires authentication)
docker push ghcr.io/owner/meepleai-monorepo/api:test-$(date +%Y%m%d)
```

### Build Arguments

```dockerfile
# Dockerfile example
ARG ASPNETCORE_ENVIRONMENT=Production
ARG VERSION=unknown

# Use in build
LABEL version="${VERSION}"
ENV ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
```

```bash
# Pass build args
docker build \
  --build-arg ASPNETCORE_ENVIRONMENT=Staging \
  --build-arg VERSION=v1.2.3 \
  -t api:v1.2.3 .
```

---

## Registry Operations

### Pull Images

```bash
# Production latest
docker pull ghcr.io/owner/meepleai-monorepo/api:latest
docker pull ghcr.io/owner/meepleai-monorepo/web:latest

# Specific version
docker pull ghcr.io/owner/meepleai-monorepo/api:v1.2.3

# Staging
docker pull ghcr.io/owner/meepleai-monorepo/api:staging-latest
```

### Push Images

```bash
# Tag local image
docker tag meepleai-api:local ghcr.io/owner/meepleai-monorepo/api:v1.2.3

# Push to registry
docker push ghcr.io/owner/meepleai-monorepo/api:v1.2.3

# Push multiple tags
docker tag ghcr.io/owner/meepleai-monorepo/api:v1.2.3 ghcr.io/owner/meepleai-monorepo/api:latest
docker push ghcr.io/owner/meepleai-monorepo/api:latest
```

### List Registry Images

```bash
# Using GitHub API
curl -H "Authorization: Bearer $GITHUB_TOKEN" \
  https://api.github.com/users/OWNER/packages/container/meepleai-monorepo%2Fapi/versions

# Using GitHub web interface
# Navigate to: github.com/owner/repo/pkgs/container/meepleai-monorepo%2Fapi
```

### Delete Old Images

```bash
# Delete specific version via GitHub API
curl -X DELETE \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  https://api.github.com/user/packages/container/meepleai-monorepo%2Fapi/versions/VERSION_ID

# Automated cleanup (GitHub Actions scheduled)
# See: .github/workflows/cleanup-old-images.yml (if implemented)
```

---

## Best Practices

### 1. Immutable Production Tags

**✅ DO**:
```yaml
# Production uses immutable version tags
docker pull ghcr.io/owner/repo/api:v1.2.3  # Never changes
```

**❌ DON'T**:
```yaml
# Never use 'latest' in production docker-compose
image: ghcr.io/owner/repo/api:latest  # Changes unpredictably!
```

### 2. Semantic Versioning

**✅ DO**:
```bash
# Follow semantic versioning
v1.2.3 → v1.2.4  # Patch: bug fix
v1.2.4 → v1.3.0  # Minor: new feature
v1.3.0 → v2.0.0  # Major: breaking change
```

**❌ DON'T**:
```bash
# Avoid random version numbers
v1.2.3 → v1.5.0  # Skipping versions
v1.2.3 → v1.2.10 # Too many patches (should be minor)
```

### 3. Build Metadata

**✅ DO**:
```dockerfile
# Add metadata to images
LABEL maintainer="MeepleAI Team"
LABEL version="${VERSION}"
LABEL git.commit="${GIT_COMMIT}"
LABEL build.date="${BUILD_DATE}"
```

**Inspect**:
```bash
docker inspect ghcr.io/owner/repo/api:v1.2.3 | jq '.[0].Config.Labels'
```

### 4. Multi-Stage Builds

**✅ DO**:
```dockerfile
# Multi-stage build reduces image size
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "Api.dll"]
```

### 5. Layer Caching

**✅ DO**:
```dockerfile
# Copy dependencies first (better caching)
COPY *.csproj .
RUN dotnet restore

# Copy source code last (changes more frequently)
COPY . .
RUN dotnet build
```

### 6. Security Scanning

```bash
# Scan images for vulnerabilities
docker scan ghcr.io/owner/repo/api:v1.2.3

# Trivy scanner
trivy image ghcr.io/owner/repo/api:v1.2.3
```

---

## Troubleshooting

### Issue: Cannot Pull Image

```bash
# Error: unauthorized: authentication required
# Solution: Login to registry
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin

# Verify token permissions
curl -H "Authorization: Bearer $GITHUB_TOKEN" \
  https://api.github.com/user
```

### Issue: Image Tag Not Found

```bash
# Error: manifest unknown
# Solution: Verify tag exists
docker pull ghcr.io/owner/repo/api:v1.2.3

# List available tags (GitHub API)
curl -H "Authorization: Bearer $GITHUB_TOKEN" \
  https://api.github.com/users/OWNER/packages/container/repo%2Fapi/versions | jq '.[].metadata.container.tags'
```

### Issue: Build Cache Issues

```bash
# Build without cache
docker build --no-cache -t api:v1.2.3 .

# Clear all build cache
docker builder prune -a -f
```

### Issue: Image Size Too Large

```bash
# Check image layers
docker history ghcr.io/owner/repo/api:v1.2.3

# Use dive for interactive analysis
dive ghcr.io/owner/repo/api:v1.2.3

# Optimize Dockerfile:
# - Use alpine base images
# - Multi-stage builds
# - Remove build artifacts
# - Minimize layers
```

### Issue: Push Rate Limit

```yaml
# GitHub Container Registry Limits:
# - Public: Unlimited pulls
# - Private: 5000 pulls/hour for authenticated users
# - Pushes: No explicit limit but rate-limited

# Solution: Implement caching in CI/CD
cache-from: type=gha
cache-to: type=gha,mode=max
```

---

## Quick Reference

### Common Commands

```bash
# Build
docker build -t api:v1.2.3 .

# Tag
docker tag api:v1.2.3 ghcr.io/owner/repo/api:v1.2.3

# Push
docker push ghcr.io/owner/repo/api:v1.2.3

# Pull
docker pull ghcr.io/owner/repo/api:v1.2.3

# Inspect
docker inspect ghcr.io/owner/repo/api:v1.2.3

# History
docker history ghcr.io/owner/repo/api:v1.2.3

# Login
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin

# Logout
docker logout ghcr.io
```

### Version Workflow

```bash
# 1. Development: Local builds
docker build -t api:dev .

# 2. Staging: Push to main-staging
git push origin main-staging
# GitHub Actions builds: staging-YYYYMMDD-SHA

# 3. Production: Create release tag
git tag -a v1.2.3 -m "Release v1.2.3"
git push origin v1.2.3
# GitHub Actions builds: v1.2.3 + latest
```

---

## Additional Resources

- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [Semantic Versioning](https://semver.org/)
- [GitHub Container Registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [Multi-Stage Builds](https://docs.docker.com/build/building/multi-stage/)

---

**Next**: [Deployment Workflows Guide](./deployment-workflows-guide.md)
