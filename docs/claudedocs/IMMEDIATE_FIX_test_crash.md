# IMMEDIATE FIX: Test Suite Crash (Exit -1073741819)

**Date**: 2026-01-19
**Issue**: Test suite crashes after 26 minutes with ACCESS_VIOLATION
**Root Cause**: PostgreSQL 2GB memory limit insufficient for concurrent test load (needs 3GB)
**Fix Time**: 10 minutes
**Confidence**: 85%

---

## Problem Summary

```
Test Output:
[xUnit.net 00:27:03.13] Catastrophic failure: Test process crashed with exit code -1073741819.
Non superato! - Non superati: 17. Superati: 5369. Totale: 5414. Durata: 26 m

Exit Code: -1073741819 = 0xC0000005 = ACCESS_VIOLATION (Windows)
```

**What Happens:**
1. Test suite runs 5,414 tests with high concurrency
2. PostgreSQL handles 50-100 concurrent connections
3. Memory usage peaks at ~2.9GB (calculation in research doc)
4. Container limit is 2GB
5. Docker OOM killer terminates PostgreSQL container
6. Test process loses DB connection → crashes with ACCESS_VIOLATION

---

## Step-by-Step Fix

### Step 1: Configure WSL2 Memory (Windows)

**Time:** 2 minutes

1. Open PowerShell as Administrator

2. Create `.wslconfig` file:
   ```powershell
   notepad C:\Users\$env:USERNAME\.wslconfig
   ```

3. Add this content:
   ```ini
   [wsl2]
   memory=16GB
   processors=4
   swap=4GB
   localhostForwarding=true
   ```

4. Save and close notepad

5. Shutdown WSL2:
   ```powershell
   wsl --shutdown
   ```

**Why:** WSL2 needs 16GB to accommodate all Docker services (10.5GB) + OS overhead

### Step 2: Use Test-Specific Docker Compose

**Time:** 1 minute

**Option A: Use test override** (Recommended)
```bash
cd infra
docker compose down
docker compose -f docker-compose.yml -f docker-compose.test.yml --profile dev up -d
```

**Option B: Modify main docker-compose.yml**
```bash
cd infra
nano docker-compose.yml
```

Find the `postgres` service and change:
```yaml
# Line ~28-35
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 4G      # Change from 2G to 4G
    reservations:
      cpus: '1.0'
      memory: 2G      # Change from 1G to 2G
```

Also update shared_buffers:
```yaml
# Line ~11
environment:
  POSTGRES_SHARED_BUFFERS: 1GB      # Change from 512MB
  POSTGRES_EFFECTIVE_CACHE_SIZE: 3GB  # Change from 1536MB
```

Save and exit (Ctrl+O, Enter, Ctrl+X)

### Step 3: Restart Docker

**Time:** 2 minutes

1. Restart Docker Desktop:
   - Right-click Docker Desktop tray icon
   - Select "Restart"
   - Wait for Docker to fully restart (~1-2 minutes)

2. Verify WSL2 configuration:
   ```powershell
   wsl -l -v
   # Should show Docker desktop distributions running
   ```

### Step 4: Start Services

**Time:** 2 minutes

```bash
cd infra

# Stop any running containers
docker compose down

# Start with test configuration
docker compose -f docker-compose.yml -f docker-compose.test.yml --profile dev up -d

# Wait for health checks (30-60 seconds)
docker compose ps
```

**Expected output:**
```
NAME                STATUS              PORTS
meepleai-postgres   Up (healthy)        5432/tcp
meepleai-qdrant     Up (healthy)        6333-6334/tcp
meepleai-redis      Up (healthy)        6379/tcp
```

### Step 5: Verify Memory Allocation

**Time:** 1 minute

```bash
# Check container memory limits
docker inspect meepleai-postgres | grep -A 2 "Memory"

# Should show:
# "Memory": 3221225472,  # 3GB in bytes

# Monitor real-time usage
docker stats --no-stream
```

**Expected:** PostgreSQL shows 3GB limit

### Step 6: Run Tests

**Time:** 25-30 minutes

```bash
cd apps/api

# Run full test suite
dotnet test --logger "console;verbosity=minimal" 2>&1 | tee test-output.log

# In another terminal, monitor memory:
docker stats
```

**Watch for:**
- PostgreSQL memory stays under 3GB
- No "Catastrophic failure" message
- All tests complete
- Exit code 0 (success)

### Step 7: Verify Fix

**Success Criteria:**
- ✅ No crash (exit code 0)
- ✅ All 5,414 tests complete
- ✅ PostgreSQL memory < 3GB throughout
- ✅ Test duration ~26 minutes (same as before)

**If still crashes:**
- Check WSL2 memory: `powershell Get-Process vmmem`
- Increase .wslconfig memory to 20GB
- Check Docker Desktop → Settings → Resources
- Review detailed analysis in `docs/04-deployment/CAPACITY_PLANNING.md`

---

## Quick Verification Commands

### Before Fix

```bash
# Check current PostgreSQL limit
docker inspect meepleai-postgres | grep "Memory"
# Should show: 2147483648 (2GB)

# Check WSL2 config
cat C:\Users\$env:USERNAME\.wslconfig
# May not exist or have low memory
```

### After Fix

```bash
# Verify PostgreSQL limit increased
docker inspect meepleai-postgres | grep "Memory"
# Should show: 3221225472 (3GB)

# Verify WSL2 config
cat C:\Users\$env:USERNAME\.wslconfig
# Should show: memory=16GB

# Monitor during tests
docker stats --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}"
```

---

## What This Fixes

### Primary Issue: Test Crash

**Before:**
```
PostgreSQL: 2GB limit
Peak usage: 2.9GB during tests
Result: OOM → Container killed → Test crash (exit -1073741819)
```

**After:**
```
PostgreSQL: 3GB limit
Peak usage: 2.9GB during tests
Result: Within limit → Tests complete successfully
```

### Secondary Benefits

1. **Eliminates flakiness**: No random crashes during high-concurrency tests
2. **Faster CI/CD**: Tests complete reliably without retries
3. **Better debugging**: Tests run to completion, easier to identify real failures
4. **Scalable config**: Can handle future test growth

---

## Alternative Solutions (If Fix Doesn't Work)

### Option 1: Reduce Test Concurrency

**File:** `apps/api/tests/Api.Tests/AssemblyInfo.cs`

```csharp
// Disable parallel execution to reduce memory pressure
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

**Trade-off:** Tests will take 2-3x longer (~60-80 minutes vs 26 minutes)

### Option 2: Reduce PostgreSQL work_mem

**File:** `infra/docker-compose.test.yml`

```yaml
postgres:
  environment:
    POSTGRES_WORK_MEM: 8MB  # Reduce from 16MB
```

**Impact:** Peak drops to 512MB + (8MB × 150) = 1.7GB (fits in 2GB)
**Trade-off:** Queries may be slower (more disk sorting)

### Option 3: Split Test Suite

```bash
# Run in batches to reduce concurrent load
dotnet test --filter "FullyQualifiedName~Authentication"
dotnet test --filter "FullyQualifiedName~GameManagement"
# ... etc
```

**Trade-off:** Manual process, takes longer overall

---

## Rollback Instructions

If fix causes issues:

```bash
# 1. Stop Docker
docker compose down

# 2. Remove .wslconfig
rm C:\Users\$env:USERNAME\.wslconfig

# 3. Revert docker-compose (if modified)
cd infra
git checkout docker-compose.yml

# 4. Restart Docker Desktop

# 5. Use original config
docker compose --profile dev up -d
```

---

## Next Steps After Fix

1. **Verify fix works**: Run full test suite, confirm no crash
2. **Update CI/CD**: Apply same memory config to GitHub Actions
3. **Monitor production**: Plan PostgreSQL upgrade for 1K users (use 3GB)
4. **Document success**: Update this file with results

---

## Related Documentation

- **Full Analysis**: `docs/04-deployment/CAPACITY_PLANNING.md`
- **Test Fixes**: `docs/05-testing/backend/test-fixes-2026-01-19.md`
- **Docker Memory Analysis**: `docs/claudedocs/docker-memory-analysis-2026-01-19.md`

---

## Support

**If this doesn't fix the crash:**

1. Read full capacity planning: `docs/04-deployment/CAPACITY_PLANNING.md`
2. Check WSL2 memory usage: `powershell Get-Process vmmem | Select WorkingSet64`
3. Review test output in detail for other failure patterns
4. Consider Alternative Solutions above

**Success Indicators:**
- Test completes without crash
- PostgreSQL memory < 4GB
- Exit code 0

---

**Created**: 2026-01-19
**Status**: Ready to apply
**Estimated Impact**: Fixes test crash, enables reliable CI/CD