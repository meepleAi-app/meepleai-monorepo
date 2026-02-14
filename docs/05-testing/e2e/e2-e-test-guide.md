# E2E Test Guide - Full Suite Execution

**Purpose**: Complete guide for running end-to-end tests requiring full infrastructure.

---

## Test Categories Overview

| Category | Test Count | Prerequisites | Offline Capable |
|----------|-----------|---------------|-----------------|
| **Unit** | ~3,500 | None | ✅ Yes |
| **Integration** | ~1,800 | Docker (Testcontainers) | ✅ Yes |
| **E2E** | ~700 | API + Full Infra | ❌ No |

---

## Quick Start - Run All Tests

### Option 1: Unit + Integration Only (Offline)
```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "Category!=E2E"
```

**Result**: ~5,300 tests pass without external services.

### Option 2: Full Suite (Requires Infrastructure)
```bash
# Terminal 1: Start infrastructure
cd infra
docker compose up postgres qdrant redis

# Terminal 2: Start API
cd apps/api/src/Api
dotnet run

# Terminal 3: Run all tests
cd apps/api/tests/Api.Tests
dotnet test
```

**Result**: All ~6,021 tests execute (requires API + services).

---

## Prerequisites by Test Category

### Unit Tests (~3,500 tests)
**No prerequisites** - Pure logic tests with mocks.

```bash
dotnet test --filter "Category=Unit"
```

**Examples**:
- Domain entity tests
- Value object validation
- Command handler logic (with mocked repositories)

---

### Integration Tests (~1,800 tests)
**Requires**: Docker Desktop (for Testcontainers)

```bash
# Ensure Docker is running
docker ps

# Run integration tests
dotnet test --filter "Category=Integration"
```

**How It Works**:
- **Testcontainers** automatically spins up PostgreSQL/Redis containers
- Each test gets isolated database (no pollution)
- Containers cleaned up after test completion

**Examples**:
- Repository persistence tests
- Database migration tests
- Cache integration tests
- Query handler tests

---

### E2E Tests (~700 tests)
**Requires**: Full infrastructure + running API

#### Step 1: Start Infrastructure Services

```bash
cd infra
docker compose up -d postgres qdrant redis
```

**Verify Services**:
```bash
# PostgreSQL
docker exec -it meepleai-postgres psql -U meepleai -d meepleai -c "SELECT version();"

# Qdrant
curl http://localhost:6333/collections

# Redis
docker exec -it meepleai-redis redis-cli ping
```

#### Step 2: Configure Secrets

```bash
cd infra/secrets

# Copy example secrets
for file in *.secret.example; do cp "$file" "${file%.example}"; done

# Edit secrets with your values
nano admin.secret        # Admin credentials
nano openrouter.secret   # OpenRouter API key (for LLM tests)
nano database.secret     # Database credentials
```

**Required Secrets for E2E**:
- `admin.secret`: Admin user credentials
- `openrouter.secret`: `OPENROUTER_API_KEY` for LLM tests
- `database.secret`: PostgreSQL connection details

#### Step 3: Start API

```bash
cd apps/api/src/Api
dotnet run
```

**Verify API Running**:
```bash
# Health check
curl http://localhost:8080/health

# Swagger UI
open http://localhost:8080/scalar/v1
```

#### Step 4: Run E2E Tests

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "Category=E2E"
```

**Examples**:
- RAG accuracy validation (`FirstAccuracyBaselineTest`)
- AI agent integration tests
- Full workflow tests (chat → RAG → response)

---

## Environment Variables

### Required for E2E Tests
```bash
# OpenRouter (LLM provider)
OPENROUTER_API_KEY=sk-or-v1-xxxxx

# Database
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=meepleai
POSTGRES_USER=meepleai
POSTGRES_PASSWORD=***

# Qdrant (Vector DB)
QDRANT_URL=http://localhost:6333

# Redis (Cache)
REDIS_URL=localhost:6379
```

### Optional (with Defaults)
```bash
# Embedding
EMBEDDING_PROVIDER=ollama
EMBEDDING_MODEL=nomic-embed-text

# API Base URL
API_BASE_URL=http://localhost:8080
```

---

## Troubleshooting

### ❌ "API not available at http://localhost:8080"

**Symptoms**:
```
❌ API not running
Prerequisites:
  1. Start API: cd apps/api/src/Api && dotnet run
  2. Ensure services: docker compose up postgres qdrant redis
```

**Solutions**:
1. **Check API Process**:
   ```bash
   # Windows
   netstat -ano | findstr :8080

   # Linux/Mac
   lsof -i :8080
   ```

2. **Restart API**:
   ```bash
   cd apps/api/src/Api
   dotnet run --launch-profile "Development"
   ```

3. **Check Logs**: Look for startup errors in console output.

---

### ❌ "PostgreSQL container failed to start"

**Symptoms**:
```
System.InvalidOperationException: PostgreSQL container failed to start after 3 attempts
```

**Solutions**:
1. **Check Docker**:
   ```bash
   docker ps -a | grep postgres
   ```

2. **Kill Conflicting Process**:
   ```bash
   # Windows
   netstat -ano | findstr :5432
   taskkill /PID <PID> /F

   # Linux/Mac
   lsof -i :5432
   kill -9 <PID>
   ```

3. **Restart Containers**:
   ```bash
   docker compose down -v  # Remove volumes
   docker compose up -d postgres
   ```

---

### ❌ "Qdrant collection not found"

**Symptoms**:
```
Collection 'game_rules' not found
```

**Solutions**:
1. **Check Qdrant**:
   ```bash
   curl http://localhost:6333/collections
   ```

2. **Restart Qdrant**:
   ```bash
   docker compose restart qdrant
   ```

3. **Reinitialize Collection**: Restart API (auto-creates collection on startup).

---

### ❌ "OpenRouter API key not configured"

**Symptoms**:
```
InvalidOperationException: OPENROUTER_API_KEY is required
```

**Solutions**:
1. **Get API Key**: https://openrouter.ai/keys
2. **Configure Secret**:
   ```bash
   echo "OPENROUTER_API_KEY=sk-or-v1-xxxxx" > infra/secrets/openrouter.secret
   ```
3. **Restart API** to load new secret.

---

### ❌ "789 test failures with NpgsqlConnectionStringBuilder"

**Symptoms**:
```
System.ArgumentException: Couldn't set connection timeout
Parameter name: connection timeout
```

**Status**: ✅ **FIXED** in commit `6228a1877` (2026-01-16)

**Solution**: Already resolved in `main-dev`. Pull latest changes:
```bash
git pull origin main-dev
```

---

## Test Execution Strategies

### Strategy 1: Fast Feedback (Unit Only)
```bash
dotnet test --filter "Category=Unit" --verbosity minimal
```

**Duration**: 2-3 minutes
**Use Case**: Quick validation during development

---

### Strategy 2: Pre-Commit Validation (Unit + Integration)
```bash
dotnet test --filter "Category!=E2E" --logger "console;verbosity=minimal"
```

**Duration**: 15-20 minutes
**Use Case**: Before creating PR, local CI simulation

---

### Strategy 3: Full Coverage (All Tests)
```bash
# Start infra first!
docker compose up -d && cd apps/api/src/Api && dotnet run &

# Then run all tests
dotnet test --logger "trx;LogFileName=test-results.trx"
```

**Duration**: 25-35 minutes
**Use Case**: Pre-release validation, comprehensive coverage

---

### Strategy 4: Specific Bounded Context
```bash
# GameManagement tests only
dotnet test --filter "BoundedContext=GameManagement"

# SharedGameCatalog tests only
dotnet test --filter "BoundedContext=SharedGameCatalog"
```

**Duration**: 1-5 minutes per context
**Use Case**: Focused testing after context-specific changes

---

## CI/CD Configuration

### Current GitHub Actions Setup

**backend-ci.yml** (runs on every PR):
```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - name: Run Unit + Integration Tests
        run: dotnet test --filter "Category!=E2E"
```

**Why E2E Skipped in CI**:
- Requires long-running API process
- Depends on external API keys (OpenRouter)
- Takes 25-35 minutes (expensive for every PR)
- Better suited for nightly/release builds

### Recommended: Nightly E2E Workflow

Create `.github/workflows/nightly-e2e.yml`:
```yaml
name: Nightly E2E Tests

on:
  schedule:
    - cron: '0 2 * * *'  # 2 AM daily
  workflow_dispatch:

jobs:
  e2e-full:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: meepleai
          POSTGRES_PASSWORD: meepleai
      qdrant:
        image: qdrant/qdrant:latest
      redis:
        image: redis:7-alpine

    steps:
      - uses: actions/checkout@v4

      - name: Setup Secrets
        run: |
          mkdir -p infra/secrets
          echo "OPENROUTER_API_KEY=${{ secrets.OPENROUTER_API_KEY }}" > infra/secrets/openrouter.secret

      - name: Start API (Background)
        run: |
          cd apps/api/src/Api
          dotnet run &
          sleep 30

      - name: Run E2E Tests
        run: dotnet test --filter "Category=E2E"

      - name: Upload Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: e2e-test-results
          path: '**/TestResults/*.trx'
```

---

## Performance Benchmarks

| Test Type | Count | Avg Duration | Resources Used |
|-----------|-------|--------------|----------------|
| Unit (single) | 1 | 5-50ms | RAM only |
| Integration (single) | 1 | 100ms-2s | Docker container |
| E2E (single) | 1 | 500ms-5s | Full stack |
| Full Unit Suite | 3,500 | 2-3 min | ~500MB RAM |
| Full Integration Suite | 1,800 | 12-15 min | Docker + 2GB RAM |
| Full E2E Suite | 700 | 10-15 min | Full infra |

---

## Best Practices

### During Development
1. ✅ Run unit tests frequently (`dotnet test --filter Category=Unit`)
2. ✅ Run integration tests before committing
3. ❌ Don't run E2E tests on every change (too slow)

### Before PR Creation
1. ✅ Run full test suite locally (except E2E)
2. ✅ Verify CI checks will pass
3. ✅ Check test coverage if adding features

### Before Release
1. ✅ Run complete test suite (including E2E)
2. ✅ Validate on fresh database (reset Docker volumes)
3. ✅ Check all bounded contexts pass

---

## Test Isolation Principles

### Database Isolation (Integration Tests)
- Each test class gets unique database via `SharedTestcontainersFixture.CreateIsolatedDatabaseAsync()`
- Database dropped after test class completion
- No cross-contamination between test classes

### Example:
```csharp
public async ValueTask InitializeAsync()
{
    _dbName = $"test_{Guid.NewGuid():N}";
    var connString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);
    // Use isolated database
}

public async ValueTask DisposeAsync()
{
    await _fixture.DropIsolatedDatabaseAsync(_dbName);
}
```

---

## Debugging Failed Tests

### Enable Verbose Logging
```bash
dotnet test --verbosity detailed --logger "console;verbosity=detailed"
```

### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~MyTest.MySpecificTestMethod"
```

### Attach Debugger
1. Open test file in IDE
2. Set breakpoint in test method
3. Right-click → Debug Test

### Check Test Output
- Test logs: `apps/api/tests/Api.Tests/TestResults/`
- Container logs: `docker compose logs postgres`
- API logs: Console output from `dotnet run`

---

## Known Issues

### Issue: 779 Test Failures (Pre-Fix)
**Status**: ✅ **RESOLVED** (commits `0c928386c`, `858109cf8`, `6228a1877`)

**History**:
- **Before**: 789 failures (PostgreSQL/in-memory mismatch + connection string typo)
- **Fix 1**: Corrected 11 integration test DbContext usage
- **Fix 2**: Fixed `SharedTestcontainersFixture` connection string (`Timeout=10`)
- **After**: 0 structural test failures

### Issue: E2E Tests Require Manual Setup
**Status**: ⚠️ **By Design**

**Rationale**: E2E tests validate full system integration, require:
- Running API (background process)
- External API keys (OpenRouter)
- Complete infrastructure stack

**Solution**: Follow setup steps in this guide or use automated scripts.

---

## Test Data

### Seed Data (Development)
- Auto-seeded by `AutoConfigurationService` on first run
- Admin user: From `infra/secrets/admin.secret`
- Test user: `Test@meepleai.com` / `Demo123!`
- AI models: 6 models (OpenRouter + Ollama)

### Test Fixtures
- User fixtures: `TestUserFactory`, `UserEntityBuilder`
- Game fixtures: `GameTestDataFactory`
- PDF fixtures: `PdfTestFixtureBuilder`

---

## Contact & Support

- **Documentation**: [docs/05-testing/](README.md)
- **Issues**: [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
- **Related Issue**: #2533 (E2E Test Documentation)

---

**Last Updated**: 2026-01-16
**Maintained By**: MeepleAI Development Team
