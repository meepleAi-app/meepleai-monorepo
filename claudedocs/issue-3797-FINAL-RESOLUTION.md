# Issue #3797 - FINAL RESOLUTION

## 🎯 Problem Summary
Next.js dynamic routes (`/verify-email`, `/library`) timeout after 15-20 seconds in Docker environment.

---

## ✅ ACTUAL ROOT CAUSE

**Port conflict with zombie Node.js process on Windows host**

### Discovery Process

1. **Initial Hypothesis** ❌
   - Suspected: Middleware fetch without timeout
   - Applied fix: AbortController with 5s timeout
   - Result: Problem persisted

2. **API Testing** ✅
   - Tested: API `/api/v1/auth/me` from web container
   - Result: Responds in **50ms** (fast and healthy)
   - Conclusion: Backend API is NOT the problem

3. **Container Testing** ✅
   - Tested: Routes from INSIDE web container
   - Result: `/login` responds **instantly** with 200 OK
   - Conclusion: Next.js server works perfectly

4. **Port Investigation** 🎯
   - Discovered: TWO processes on port 3000
     - PID 61940: `node.exe` on `0.0.0.0:3000` (HOST)
     - PID 17208: `com.docker.backend` on `127.0.0.1:3000` (Docker)
   - Root cause: Zombie `node.exe` intercepts ALL requests to port 3000

### Why This Happened

```
Windows Host Routing:
Request http://localhost:3000/verify-email
  ↓
Port 3000 resolution → BOTH processes listening
  ↓
node.exe (0.0.0.0:3000) ← Takes priority over 127.0.0.1:3000
  ↓
Zombie process doesn't respond
  ↓
Timeout after 15-20s ❌
```

---

## ✅ Solution Applied

### Primary Fix: Kill Zombie Process

```bash
Stop-Process -Id 61940 -Force
```

**Result**: Immediate resolution - all routes now respond in ~280ms

### Secondary Fix: Middleware Timeout (Preventive)

Although not the root cause, the middleware timeout fix is **valuable to keep**:
- Prevents future issues if API becomes slow
- Adds graceful degradation
- Improves error logging

**File**: `apps/web/middleware.ts`
**Change**: Added `AbortController` with 5s timeout to session validation fetch

---

## 📊 Validation Results

### Performance After Fix

| Route | Response Time | Status |
|-------|---------------|--------|
| `/login` | 280ms | ✅ 200 OK |
| `/verify-email?token=test` | 280ms | ✅ 200 OK |
| `/library` | <300ms | ✅ 200 OK |
| API `/api/v1/auth/me` | 50ms | ✅ 401 (expected) |

### Docker Container Health

```
NAME           STATUS
meepleai-web   Up (healthy) - port 3000
meepleai-api   Up (healthy) - port 8080
meepleai-postgres   Up (healthy) - port 5432
```

---

## 🔍 Investigation Learnings

### What We Ruled Out

| Hypothesis | Evidence | Conclusion |
|------------|----------|------------|
| Middleware fetch timeout | API responds in 50ms | ❌ Not the cause |
| Next.js memory leak | Already on 16.1.1 with fix | ❌ Not the cause |
| Docker networking | Container-to-container fast | ❌ Not the cause |
| Standalone mode config | Files correctly copied | ❌ Not the cause |
| Node.js fetch API conflict | Node 20.11.1 < 20.16 | ❌ Not the cause |

### What We Found

✅ **Port conflict** with zombie `node.exe` process on Windows host
✅ **Process intercepts requests** before reaching Docker
✅ **Simple solution**: Kill the conflicting process

---

## 🛡️ Preventive Measures

### 1. Check for Port Conflicts Before Starting Docker

```bash
# Windows: Check what's using port 3000
netstat -ano | findstr :3000

# Identify process
Get-Process -Id <PID>

# Kill if necessary
Stop-Process -Id <PID> -Force
```

### 2. Use Dedicated Ports for Docker

Consider using different ports for Docker vs dev:
- Docker: `3000` (production-like)
- Dev: `3001` (local development)

**docker-compose.yml**:
```yaml
web:
  ports:
    - "127.0.0.1:3000:3000"  # Keep as is for prod parity
```

**package.json**:
```json
"dev": "next dev -p 3001"  # Change dev port to 3001
```

### 3. Cleanup Script

Create `tools/cleanup-ports.ps1`:
```powershell
# Check and kill processes on development ports
$ports = @(3000, 8080, 5432, 6379)
foreach ($port in $ports) {
    $proc = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($proc) {
        Stop-Process -Id $proc.OwningProcess -Force
        Write-Host "Killed process on port $port"
    }
}
```

---

## 📝 Changes Made

### 1. apps/web/middleware.ts
- ✅ Added `AbortController` with 5s timeout (lines 98-139)
- ✅ Improved error handling and logging
- Status: **Keep** (good defensive practice)

### 2. System Cleanup
- ✅ Terminated zombie node.exe process (PID 61940)
- Status: **Completed**

### 3. Documentation
- ✅ Created 4 analysis documents in `claudedocs/`
- Status: **Completed**

---

## 🎯 Final Recommendations

### Immediate Actions
✅ **Resolved** - No further action needed for this issue

### Long-term Improvements

1. **Port Management**
   - Document port usage in README
   - Add port cleanup to setup scripts
   - Consider port separation (dev vs Docker)

2. **Developer Workflow**
   - Add pre-docker-start port check
   - Document how to identify/kill zombie processes
   - Add to troubleshooting guide

3. **Monitoring**
   - Keep middleware timeout fix (defense-in-depth)
   - Add startup validation for port availability
   - Log port conflicts in docker-compose startup

---

## 📚 Issue Closure Checklist

- ✅ Root cause identified (port conflict)
- ✅ Solution applied (kill zombie process)
- ✅ Validation completed (280ms response time)
- ✅ Secondary fix applied (middleware timeout prevention)
- ✅ Documentation created
- ✅ Preventive measures recommended
- ⏳ **Ready to close Issue #3797**

---

## 💡 Key Takeaway

**Always check for port conflicts FIRST** when troubleshooting Docker networking issues on Windows/WSL2. Zombie processes from previous dev sessions can intercept requests before they reach Docker containers.

---

**Status**: ✅ **RESOLVED**
**Resolution Time**: ~30 minutes (investigation + fix)
**Performance**: Dynamic routes now load in < 300ms
**Next Steps**: Close GitHub issue #3797 with findings
