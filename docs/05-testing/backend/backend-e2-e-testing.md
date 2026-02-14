# Backend E2E Testing Guide

**Purpose**: Backend API testing with full infrastructure setup.

---

## Test Categories

| Category | Count | Duration | Prerequisites | Command |
|----------|-------|----------|---------------|---------|
| **Unit** | ~3,500 | 2-3 min | None | `dotnet test --filter "Category=Unit"` |
| **Integration** | ~1,800 | 12-15 min | Docker (Testcontainers) | `dotnet test --filter "Category=Integration"` |
| **E2E** | ~75+ | <10 min | Testcontainers + API | `dotnet test --filter "Category=E2E"` |

**Filter Patterns**:
```bash
dotnet test --filter "Category!=E2E"  # Skip E2E
dotnet test --filter "FullyQualifiedName~GameManagement"  # By context
```

---

## Quick Start

### Development (Unit + Integration)
```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "Category!=E2E"
# Result: ~5,300 tests in 15-20 min
```

### Full Suite (E2E Included)
```bash
# Terminal 1: Infrastructure
cd infra && docker compose up postgres qdrant redis

# Terminal 2: API
cd apps/api/src/Api && dotnet run

# Terminal 3: Tests
cd apps/api/tests/Api.Tests && dotnet test
# Result: ~6,021 tests in 25-35 min
```

---

## E2E Test Coverage (Issue #3012)

| Category | File | Tests | Coverage |
|----------|------|-------|----------|
| **Auth** | `AuthenticationE2ETests.cs` | 13 | Registration, login, 2FA |
| **Games** | `GameManagementE2ETests.cs` | 11 | CRUD, search, pagination |
| **Share** | `ShareRequestE2ETests.cs` | 6 | Submit, review, approve |
| **Library** | `UserLibraryE2ETests.cs` | 12 | Add/remove, stats, quota |
| **Notifications** | `NotificationsE2ETests.cs` | 9 | List, mark read, count |
| **Documents** | `DocumentProcessingE2ETests.cs` | 10 | Upload, OCR, extraction |
| **Chat** | `ChatE2ETests.cs` | 14 | Threads, messages, lifecycle |

---

## Setup Guide

### 1. Prerequisites

| Platform | Docker | .NET 9 | Verify |
|----------|--------|--------|--------|
| **Windows** | `winget install Docker.DockerDesktop` | `winget install Microsoft.DotNet.SDK.9` | `docker --version && dotnet --version` |
| **Linux** | `curl -fsSL https://get.docker.com \| sh` | `wget https://dot.net/v1/dotnet-install.sh && ./dotnet-install.sh --channel 9.0` | `docker --version && dotnet --version` |
| **macOS** | `brew install --cask docker` | `brew install dotnet@9` | `docker --version && dotnet --version` |

### 2. Secrets Configuration

**Quick Setup** (auto-generates all secrets):
```bash
cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated
```

**Manual Setup**:
```bash
cd infra/secrets
echo "POSTGRES_PASSWORD=secure_pwd" > database.secret
echo "REDIS_PASSWORD=redis_pwd" > redis.secret
echo "QDRANT_API_KEY=qdrant_key" > qdrant.secret
echo "JWT_SECRET=min_32_chars_secret" > jwt.secret
echo "ADMIN_EMAIL=admin@meepleai.com" > admin.secret
echo "ADMIN_PASSWORD=Admin123!" >> admin.secret
echo "OPENROUTER_API_KEY=sk-or-v1-xxx" > openrouter.secret
```

### 3. Verify Infrastructure

```bash
cd infra && docker compose up -d postgres qdrant redis

# Health Checks
docker exec -it meepleai-postgres psql -U meepleai -d meepleai -c "SELECT version();"
curl http://localhost:6333/collections  # Qdrant
docker exec -it meepleai-redis redis-cli ping  # Redis (expect PONG)
```

### 4. Start API (E2E Only)

```bash
cd apps/api/src/Api && dotnet run
# Verify: curl http://localhost:8080/health  # Expect {"status":"Healthy"}
```

---

## Test Execution

| Strategy | Command | Duration | Use Case |
|----------|---------|----------|----------|
| **Fast Loop** | `dotnet watch test --filter "Category=Unit"` | 2-3 min | TDD, active dev |
| **Pre-Commit** | `dotnet test --filter "Category!=E2E"` | 15-20 min | PR validation |
| **Full Suite** | `dotnet test` (after API start) | 25-35 min | Pre-release |
| **Parallel CI** | `dotnet test --filter "Category!=E2E" -- RunConfiguration.MaxCpuCount=4` | 10-15 min | CI/CD |

---

## Troubleshooting

| Issue | Diagnosis | Fix |
|-------|-----------|-----|
| **API not available** | `netstat -ano \| findstr :8080` (Win) or `lsof -i :8080` (Unix) | Start API: `cd apps/api/src/Api && dotnet run` or kill PID |
| **PostgreSQL container failed** | `docker ps -a \| grep postgres` | Kill port 5432 process, run `pwsh cleanup-testcontainers.ps1`, restart Docker |
| **Qdrant collection not found** | `curl http://localhost:6333/collections` | Restart: `docker compose restart qdrant`, API auto-creates collections |
| **OpenRouter API key missing** | Check `infra/secrets/openrouter.secret` | Get key from https://openrouter.ai/keys, save to `openrouter.secret` |
| **Testhost blocking** | `tasklist \| grep testhost` | `taskkill /IM testhost.exe /F` (Issue #2593) |
| **789 test failures** | Connection string typo | ✅ FIXED (commit `6228a1877`), `git pull origin main-dev` |

---

## CI/CD Integration

**Workflow**: `.github/workflows/backend-e2e-tests.yml` (Issue #3012)

**Triggers**: Push/PR to `main`, `main-dev` (API changes only)

**Execution Flow**:
1. Testcontainers auto-start (PostgreSQL, Redis, Qdrant)
2. WebApplicationFactory creates isolated API instance
3. Each test class gets isolated database
4. Auto-cleanup after completion

**Architecture**:
```
E2ETestFixture → WebApplicationFactory<Program>
  ├─ PostgreSQL Testcontainer (isolated DB per test class)
  ├─ Redis Testcontainer (shared cache)
  └─ Qdrant Testcontainer (vector DB)

E2ETestBase → HttpClient (authenticated) + DbContext + SeedTestDataAsync()
```

**Why Testcontainers**: No manual setup, isolated execution, CI-friendly, fast (~8-10 min)

**Comparison**:

| Aspect | Frontend E2E | Backend E2E |
|--------|-------------|-------------|
| Tool | Playwright | xUnit + WebApplicationFactory |
| Focus | UI flows | API endpoints |
| Duration | 15-25 min (4 shards) | <10 min |

---

## Best Practices

### Development Checklist

| Phase | Action | Command |
|-------|--------|---------|
| **During Dev** | Run Unit tests frequently | `dotnet watch test --filter "Category=Unit"` (2-3 min) |
| **Before Commit** | Run Integration tests | `dotnet test --filter "Category!=E2E"` (15-20 min) |
| **Before PR** | Full suite (no E2E) | Same as commit + verify CI status |
| **Before Release** | Complete suite (E2E) | Start infra+API → `dotnet test` (25-35 min) |

### Performance

| Type | Single | Suite | Resources | Optimization |
|------|--------|-------|-----------|--------------|
| **Unit** | 5-50ms | 2-3 min | ~500MB | `maxParallelThreads: 8` |
| **Integration** | 100ms-2s | 12-15 min | Docker+2GB | `Pooling=true;MaxPoolSize=50` |
| **E2E** | 500ms-5s | <10 min | Full stack | Isolated DB: `test_{Guid:N}` |

### Debugging

| Step | Command |
|------|---------|
| Verbose logs | `dotnet test --verbosity detailed` |
| Single test | `dotnet test --filter "FullyQualifiedName~TestName"` |
| DB inspection | `docker exec -it meepleai-postgres psql -U meepleai -d test_abc123` |
| Container logs | `docker compose logs postgres` |

---

**Last Updated**: 2026-01-27 • **Issue**: #3012 • **Maintained By**: Backend Team
