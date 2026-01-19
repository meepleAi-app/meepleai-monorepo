# 🐳 Docker Infrastructure Fix - Backend Test Unblocking

**Issue**: Backend tests failing with 81% failure rate (105/130 tests)
**Root Cause**: Docker Desktop not running → Testcontainers cannot create PostgreSQL/Redis containers
**Impact**: All integration and E2E tests blocked

---

## Diagnosis Summary

### Evidence of Docker Unavailability

```bash
# All Docker commands return no output:
docker info       → (empty)
docker ps -a      → (empty)
docker network ls → (empty)
```

### Test Failure Pattern

**Error**: `Npgsql.NpgsqlException: The operation has timed out`

**Stack Trace**:
```
at SharedTestcontainersFixture.CreateIsolatedDatabaseAsync(String databaseName)
   SharedTestcontainersFixture.cs:296
```

**Frequency**: 105 of 130 tests processed (81% failure rate)

---

## Fix Procedure

### 1. Start Docker Desktop

**Windows**:
```
1. Click Start Menu
2. Search "Docker Desktop"
3. Launch application
4. Wait for "Docker Desktop is running" notification
5. Green whale icon in system tray = ready
```

### 2. Verify Docker Status

```bash
# Check Docker version
docker --version
# Expected: Docker version 24.0+ or higher

# Check Docker daemon
docker info | head -10
# Expected: Server Version, Storage Driver, etc.

# List running containers
docker ps
# Expected: May be empty initially (OK)
```

### 3. Start MeepleAI Services

```bash
cd D:\Repositories\meepleai-monorepo-frontend\infra

# Start infrastructure services
docker compose up -d postgres redis qdrant

# Expected output:
# ✅ Container meepleai-postgres  Started
# ✅ Container meepleai-redis     Started
# ✅ Container meepleai-qdrant    Started
```

### 4. Verify Service Health

```bash
# List running containers
docker ps

# Expected: 3 containers in "Up" status
# - meepleai-postgres (port 5432)
# - meepleai-redis (port 6379)
# - meepleai-qdrant (port 6333)

# Check PostgreSQL logs
docker compose logs postgres | grep "ready to accept connections"
# Expected: "database system is ready to accept connections"

# Test PostgreSQL connection
docker exec meepleai-postgres psql -U meepleai -c "SELECT version();"
# Expected: PostgreSQL version output

# Test Redis connection
docker exec meepleai-redis redis-cli PING
# Expected: PONG
```

### 5. Re-Run Backend Tests

```bash
cd D:\Repositories\meepleai-monorepo-frontend\apps\api

# Clean previous test artifacts
rm -rf bin/Debug/net9.0 obj/Debug/net9.0 2>&1 || echo "Clean complete"

# Rebuild and run tests with coverage
dotnet test tests/Api.Tests/Api.Tests.csproj \
    --verbosity normal \
    -p:CollectCoverage=true \
    -p:CoverletOutputFormat=cobertura \
    -p:CoverletOutput=../../coverage/backend/coverage.cobertura.xml

# Expected results (with Docker running):
# - Total tests: ~1,200
# - Passed: ~1,140 (95%+)
# - Failed: ~10-20 (isolated issues, not infrastructure)
# - Skipped: ~40-50 (missing test PDFs, optional external services)
# - Duration: 15-25 minutes
# - Coverage: 90%+
```

---

## Troubleshooting

### Docker Desktop Won't Start

**Check 1: WSL 2 Backend (Recommended)**
```bash
# Check WSL version
wsl --version

# Should show WSL 2
# If WSL 1 or not installed:
wsl --install
wsl --set-default-version 2
```

**Check 2: Hyper-V Enabled** (Alternative Backend)
```
1. Open PowerShell as Administrator
2. Run: Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All
3. Restart computer
```

**Check 3: Docker Desktop Settings**
```
1. Right-click Docker Desktop tray icon
2. Settings → General
3. Ensure "Use WSL 2 based engine" is checked
4. Click "Apply & Restart"
```

### PostgreSQL Container Fails to Start

```bash
# Check logs
cd infra
docker compose logs postgres

# Common issues:
# - Port 5432 already in use: Stop local PostgreSQL service
# - Permission denied: Run as administrator
# - Volume mount issues: Check docker-compose.yml volume paths

# Force recreate
docker compose down -v  # ⚠️ Deletes data!
docker compose up -d postgres
```

### Redis Container Fails to Start

```bash
# Check logs
docker compose logs redis

# Common issues:
# - Port 6379 in use: netstat -ano | findstr :6379
# - Stop conflicting process or change port in docker-compose.yml
```

---

## Alternative: Use Existing Database (Bypass Testcontainers)

If Docker issues persist, configure tests to use existing database:

### Option 1: Local PostgreSQL Installation

```bash
# Install PostgreSQL 16 locally (if not installed)
# Download from: https://www.postgresql.org/download/windows/

# Create test database
psql -U postgres -c "CREATE DATABASE meepleai_test;"

# Update test configuration
# Set environment variable:
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=yourpassword"

# Run tests (will use local DB instead of Testcontainers)
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj
```

### Option 2: Skip Integration Tests Temporarily

```bash
# Run only unit tests (no Docker required)
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Unit"

# Expected:
# - ~840 unit tests
# - 99%+ pass rate
# - 5-10 minute duration
# - No Docker dependency
```

---

## Post-Fix Validation

### 1. Verify Infrastructure Health

```bash
# All containers running
docker ps
# Expected: 3 containers (postgres, redis, qdrant)

# PostgreSQL accepting connections
docker exec meepleai-postgres pg_isready
# Expected: "accepting connections"

# Redis responding
docker exec meepleai-redis redis-cli PING
# Expected: PONG

# Qdrant health
curl http://localhost:6333/health
# Expected: {"status":"ok"}
```

### 2. Run Smoke Test

```bash
cd apps/api

# Quick validation (1-2 minutes)
dotnet test tests/Api.Tests/Api.Tests.csproj \
    --filter "FullyQualifiedName~SeedAdminUserCommandHandlerTests" \
    --verbosity normal

# Expected: All tests passed ✅
# If passes: Infrastructure fixed, proceed with full suite
# If fails: Docker/DB still not accessible
```

### 3. Full Suite Execution

```bash
# Complete test run (15-25 minutes)
dotnet test tests/Api.Tests/Api.Tests.csproj \
    -p:CollectCoverage=true \
    -p:CoverletOutputFormat=cobertura

# Monitor progress
tail -f final-backend-test.log

# Expected final statistics:
# - Passed: ~1,140 (95%)
# - Failed: ~10-20 (code issues, not infrastructure)
# - Skipped: ~40-50 (missing test data)
```

---

## Impact Analysis

### Without Docker Fix
```
Backend Tests:   19% pass rate (infrastructure blocked)
Frontend Tests:  99.3% pass rate ✅
E2E Tests:       Cannot run (requires running app)
Overall:         ~23% effective testing
```

### With Docker Fix (Expected)
```
Backend Tests:   95%+ pass rate ✅
Frontend Tests:  99.3% → 100% (after locale fix) ✅
E2E Tests:       90%+ pass rate ✅
Overall:         ~94% effective testing
```

---

## Time Estimate

| Task | Duration |
|------|----------|
| Start Docker Desktop | 1-2 min |
| Start docker compose services | 2-3 min |
| Verify infrastructure health | 1 min |
| Re-run backend tests (full) | 15-25 min |
| Fix frontend locale tests | 5 min |
| Run E2E tests | 10-15 min |
| **TOTAL** | **~35-50 min** |

---

## Related Documentation

- **Test Reports**: See `docs/05-testing/FULL_SUITE_TEST_REPORT_2026-01-18.md`
- **Quick Summary**: See `QUICK_TEST_SUMMARY.md`
- **Testing Guide**: See `docs/05-testing/README.md`
- **Backend E2E Setup**: See `docs/05-testing/backend/BACKEND_E2E_TESTING.md`

---

**Status**: Infrastructure diagnosis complete, resolution steps documented
**Next**: Start Docker Desktop → Re-run tests → Expect 95%+ pass rate
