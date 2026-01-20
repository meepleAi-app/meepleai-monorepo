# Docker Memory Analysis - Test Crash Investigation

**Date**: 2026-01-19
**Issue**: Test process crash (exit code -1073741819) likely caused by Docker memory constraints
**Hypothesis**: Docker Desktop memory limit too low for test suite requirements

---

## Problem Analysis

### Test Crash Symptoms
- **Exit Code**: `-1073741819` = `0xC0000005` = ACCESS_VIOLATION
- **Timing**: Crash after 26 minutes of test execution
- **Context**: 5,369 tests passed, then catastrophic failure
- **Pattern**: Consistent crash at ~26-27 minute mark

### Docker Memory Configuration

From `infra/docker-compose.yml`, memory limits for core services:

| Service | Memory Limit | Memory Reservation | Notes |
|---------|-------------|-------------------|-------|
| **postgres** | 2GB | 1GB | Test database |
| **qdrant** | 4GB | 2GB | Vector database (memory-intensive) |
| **redis** | 1GB | 512MB | Cache + sessions |
| **ollama** | 8GB | 4GB | LLM service (AI profile) |
| **embedding-service** | 4GB | 2GB | Python ML service |
| **reranker-service** | 2GB | 1GB | Python ML service |
| **unstructured-service** | 4GB | 2GB | PDF processing |
| **smoldocling-service** | 2GB | 1GB | OCR service |

**Total Memory Requirements** (minimal profile):
- **Limits**: postgres (2GB) + qdrant (4GB) + redis (1GB) = **7GB**
- **Reservations**: postgres (1GB) + qdrant (2GB) + redis (512MB) = **3.5GB**

**Total Memory Requirements** (AI profile for tests):
- **Limits**: 7GB + embedding (4GB) + reranker (2GB) + unstructured (4GB) = **17GB+**
- **Reservations**: 3.5GB + embedding (2GB) + reranker (1GB) + unstructured (2GB) = **8.5GB+**

### Likely Root Cause

**Default Docker Desktop Memory**: Typically 8GB on Windows

**Problem**:
1. Test suite requires AI services (embedding, reranker, unstructured for PDF tests)
2. Total memory reservations: **8.5GB+** (exceeds typical 8GB Docker limit)
3. Memory limits: **17GB+** (significantly exceeds default allocation)
4. As tests accumulate memory over 26 minutes, Docker OOM killer activates
5. Container gets killed → ACCESS_VIOLATION in test process

**Why After 26 Minutes**:
- Tests accumulate memory (PDF processing, vector operations)
- Docker memory usage creeps toward limit
- OOM killer terminates container or test process
- Test process crashes with ACCESS_VIOLATION

---

## Solution: Increase Docker Desktop Memory

### Option 1: Increase Docker Desktop Memory (Recommended)

**For Test Suite**: Minimum **16GB**, Recommended **20GB**

**Steps (Docker Desktop on Windows)**:
1. Open Docker Desktop
2. Go to Settings → Resources → Advanced
3. Increase "Memory" slider to **16GB** (min) or **20GB** (recommended)
4. Click "Apply & Restart"

**Rationale**:
- Accommodates all test services (17GB limits)
- Provides headroom for test execution memory accumulation
- Prevents OOM killer from terminating containers mid-test

### Option 2: Optimize Container Memory Limits

**For systems with limited RAM (<32GB total)**:

Reduce memory limits in `docker-compose.yml`:

```yaml
# Optimized for 12GB Docker allocation
postgres:
  deploy:
    resources:
      limits:
        memory: 1.5G
      reservations:
        memory: 768M

qdrant:
  deploy:
    resources:
      limits:
        memory: 3G  # Reduced from 4G
      reservations:
        memory: 1.5G  # Reduced from 2G

redis:
  deploy:
    resources:
      limits:
        memory: 768M
      reservations:
        memory: 384M

embedding-service:
  deploy:
    resources:
      limits:
        memory: 3G  # Reduced from 4G
      reservations:
        memory: 1.5G  # Reduced from 2G

unstructured-service:
  deploy:
    resources:
      limits:
        memory: 3G  # Reduced from 4G
      reservations:
        memory: 1.5G  # Reduced from 2G

reranker-service:
  deploy:
    resources:
      limits:
        memory: 1.5G  # Reduced from 2G
      reservations:
        memory: 768M

# Total with optimization: ~12GB limits, ~6GB reservations
```

### Option 3: Use Test-Specific Profile

**Create `docker-compose.test.yml` override**:

```yaml
# docker-compose.test.yml - Optimized for testing only
services:
  postgres:
    deploy:
      resources:
        limits:
          memory: 1.5G
        reservations:
          memory: 768M

  qdrant:
    deploy:
      resources:
        limits:
          memory: 2G  # Sufficient for test vectors
        reservations:
          memory: 1G

  redis:
    deploy:
      resources:
        limits:
          memory: 512M
        reservations:
          memory: 256M

  # Disable AI services for faster, lighter tests
  embedding-service:
    profiles: [disabled]

  reranker-service:
    profiles: [disabled]

  unstructured-service:
    profiles: [disabled]

# Use: docker compose -f docker-compose.yml -f docker-compose.test.yml up
```

---

## Verification Commands

### Check Docker Desktop Memory Allocation
```powershell
# PowerShell - Check Docker Desktop settings
Get-Content "$env:APPDATA\Docker\settings.json" | Select-String "memoryMiB"
```

### Check Current Container Memory Usage
```bash
# While tests running
docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}"
```

### Monitor Memory During Tests
```bash
# Watch memory usage in real-time
docker stats

# In another terminal, run tests
cd apps/api && dotnet test
```

---

## Impact Assessment

### Current State (Hypothesis)
- **Docker Memory**: Likely 8GB (Windows default)
- **Required for Tests**: 17GB+ (limits) or 8.5GB+ (reservations)
- **Result**: OOM → Container killed → Test crash

### After Fix (Expected)
**Option 1: 16-20GB Docker Memory**
- ✅ All services run within limits
- ✅ No OOM killer activation
- ✅ Tests complete successfully
- ❌ Requires 32GB+ total system RAM

**Option 2: Optimized Limits (12GB Docker)**
- ✅ Works with 16GB total system RAM
- ✅ Reduces memory pressure
- ⚠️ May reduce performance slightly
- ✅ More realistic for CI/CD environments

**Option 3: Test-Specific Profile**
- ✅ Minimal memory footprint (4-5GB)
- ✅ Fast test startup
- ❌ Disables AI services (some tests may fail)
- ✅ Good for non-AI test runs

---

## Recommendations

### Immediate Action (Choose One)

**If you have 32GB+ system RAM**:
```
→ Increase Docker Desktop to 20GB
→ Restart Docker
→ Re-run tests
```

**If you have 16-24GB system RAM**:
```
→ Increase Docker Desktop to 12-16GB
→ Apply memory optimizations to docker-compose.yml
→ Re-run tests
```

**For CI/CD Environments**:
```
→ Use test-specific profile with reduced limits
→ Document memory requirements in CI config
→ Consider splitting tests across multiple runners
```

### Validation Steps

1. **Before changing memory**:
   ```bash
   # Check current allocation
   powershell -Command "Get-Content '$env:APPDATA\Docker\settings.json' | Select-String memoryMiB"
   ```

2. **After increasing memory**:
   ```bash
   # Verify Docker has enough memory
   docker system info | grep Memory

   # Start services
   cd infra && docker compose --profile dev up -d

   # Monitor during tests
   docker stats &
   cd ../apps/api && dotnet test
   ```

3. **Check for improvement**:
   - No catastrophic crash
   - All 5,414 tests complete
   - Memory usage stays below Docker limit

---

## Prevention Measures

### For CI/CD
1. **Document memory requirements** in CI config
2. **Use test-specific profiles** with reduced limits
3. **Split test suite** if memory constraints exist
4. **Monitor memory metrics** during CI runs

### For Development
1. **Increase Docker Desktop memory** to 16-20GB
2. **Use profiles** to only run needed services
3. **Monitor docker stats** during development
4. **Restart containers** periodically to prevent memory accumulation

### For Production
1. **Use dedicated nodes** with sufficient memory
2. **Set appropriate limits** based on load testing
3. **Configure swap** for memory pressure situations
4. **Monitor memory metrics** with Prometheus/Grafana

---

## Related Documentation

- Docker Compose: `infra/docker-compose.yml`
- Test Infrastructure: `docs/05-testing/backend/INTEGRATION_TEST_OPTIMIZATION.md`
- Issue #2593: TestHost blocking pattern
- Issue #2576: Qdrant health check timeout

---

## Next Steps

1. **Check Docker Desktop Memory Setting**
   - Current allocation vs required (17GB+)
   - Adjust based on system RAM available

2. **Apply Memory Fix**
   - Choose Option 1, 2, or 3 based on system constraints
   - Restart Docker after changes

3. **Re-run Test Suite**
   - Monitor memory usage during execution
   - Verify no catastrophic crash
   - Confirm all tests complete

4. **Document Results**
   - Update this document with actual findings
   - Add memory requirements to testing docs
   - Update CI/CD configuration if needed

---

## Status

**Current**: Investigating memory hypothesis
**Confidence**: HIGH (Docker memory limits insufficient for test requirements)
**Recommended Action**: Increase Docker Desktop memory to 16-20GB
**Expected Outcome**: Eliminates catastrophic crash, reduces test failures

---

**Last Updated**: 2026-01-19
