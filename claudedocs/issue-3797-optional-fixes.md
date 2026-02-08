# Issue #3797 - Optional Performance Optimizations

These fixes are **optional** and should be applied **only if** the primary middleware timeout fix is insufficient.

---

## Optional Fix 1: Healthcheck API Endpoint

### Problem
API healthcheck tests `/` (root) instead of `/api/v1/auth/me` (endpoint used by middleware).
API could be "healthy" but `/auth/me` endpoint slow/broken.

### Solution
Test the actual endpoint used by middleware in healthcheck.

### File: infra/docker-compose.yml

**Line 713-718**: Update API healthcheck

```yaml
healthcheck:
  test: ["CMD-SHELL", "curl --fail --max-time 3 http://localhost:8080/api/v1/auth/me || exit 1"]
  interval: 10s
  timeout: 5s  # ✅ ADD: Explicit timeout
  retries: 12
  start_period: 20s
```

### Implementation
```bash
cd infra
# Edit docker-compose.yml line 714
docker compose down
docker compose up -d postgres qdrant redis
docker compose up -d api
docker compose up -d web
```

### Validation
```bash
# Should show healthy status within 20s
docker compose ps api
```

---

## Optional Fix 2: Increase Session Cache TTL

### Problem
Session cache expires after 30s, causing frequent middleware fetch calls.
More fetches = more opportunities for timeout.

### Solution
Increase cache TTL from 30s to 2 minutes.

### File: apps/web/middleware.ts

**Line 78**: Update cache TTL

```typescript
// Before:
const SESSION_CACHE_TTL_MS = 30 * 1000; // 30 seconds

// After:
const SESSION_CACHE_TTL_MS = 120 * 1000; // 2 minutes
```

### Trade-offs
✅ **Pros**: Fewer API calls, better performance, lower timeout risk
⚠️ **Cons**: Session invalidation delayed up to 2 minutes

### Implementation
```bash
cd apps/web
# Edit middleware.ts line 78
docker compose build web --no-cache
docker compose restart web
```

---

## Optional Fix 3: Docker Network Diagnostics

### When to Use
If timeout fix works but performance still slow (>1s for dynamic routes).

### Test DNS Resolution
```bash
# Test if "api" hostname resolves correctly
docker compose exec web sh -c "ping -c 3 api"

# Expected output: 3 packets transmitted, 3 received
```

### Test API Connectivity
```bash
# Test API endpoint from web container
docker compose exec web sh -c "time curl -v http://api:8080/api/v1/auth/me"

# Expected: Response time < 500ms
```

### If Tests Fail
Check Docker network configuration:
```bash
docker network inspect meepleai_default
```

Possible fixes:
1. Use explicit IP instead of hostname
2. Add `extra_hosts` in docker-compose.yml
3. Use `host.docker.internal` for Docker Desktop

---

## Optional Fix 4: Circuit Breaker Pattern (Advanced)

### When to Use
If middleware timeout occurs frequently (>10% of requests).

### Implementation
Add circuit breaker to skip validation when API consistently fails.

### Pseudo-code
```typescript
const CIRCUIT_BREAKER_THRESHOLD = 5; // failures before opening
const CIRCUIT_BREAKER_TIMEOUT = 60000; // 1 minute recovery

let consecutiveFailures = 0;
let circuitBreakerOpenUntil = 0;

async function isSessionCookieValid(...) {
  // Check circuit breaker
  if (Date.now() < circuitBreakerOpenUntil) {
    console.warn('[middleware] Circuit breaker OPEN - skipping validation');
    return false; // Fail-safe: deny access
  }

  try {
    const valid = await fetchValidation();
    consecutiveFailures = 0; // Reset on success
    return valid;
  } catch (error) {
    consecutiveFailures++;

    if (consecutiveFailures >= CIRCUIT_BREAKER_THRESHOLD) {
      circuitBreakerOpenUntil = Date.now() + CIRCUIT_BREAKER_TIMEOUT;
      console.error('[middleware] Circuit breaker OPENED after', consecutiveFailures, 'failures');
    }

    return false;
  }
}
```

### Trade-offs
✅ **Pros**: Prevents cascade failures, improves resilience
⚠️ **Cons**: Complexity, may mask real issues, requires monitoring

---

## Performance Benchmarks

### Before Fix
- Dynamic routes: 15-20s timeout
- Static routes: < 1s
- Middleware fetch: Indefinite hang

### After Primary Fix (Timeout)
- Dynamic routes: < 3s (5s timeout max)
- Static routes: < 1s
- Middleware fetch: 5s timeout guaranteed

### After Optional Fixes (All)
- Dynamic routes: < 1s (cache hits)
- Static routes: < 500ms
- Middleware fetch: Rare (120s cache)
- Healthcheck: Validates correct endpoint

---

## Monitoring Commands

### Real-time Logs
```bash
# Terminal 1: Web container logs
docker compose logs -f web | grep "middleware"

# Terminal 2: API container logs
docker compose logs -f api | grep "auth/me"

# Terminal 3: Combined view
docker compose logs -f web api | grep -E "(middleware|auth/me)"
```

### Performance Testing
```bash
# Test dynamic route response time
time curl -v http://localhost:3000/verify-email?token=test

# Test static route response time
time curl -v http://localhost:3000/login

# Test API endpoint directly
time curl -v http://localhost:8080/api/v1/auth/me
```

### Success Metrics
- ✅ Dynamic routes: < 3s average
- ✅ Cache hit rate: > 80% after warmup
- ✅ API response time: < 500ms p99
- ✅ Zero infinite timeouts in logs
