# Metrics Implementation Limitation - Next.js Runtime Isolation

## Issue

Prometheus metrics endpoint (`/metrics`) implemented for middleware session cache monitoring has a **technical limitation** due to Next.js runtime architecture.

**Status**: Metrics endpoint exists and responds, but counters remain at 0.

---

## Root Cause

Next.js uses **separate runtime environments**:
- **Edge Runtime**: Middleware execution (lightweight, V8 isolate)
- **Node.js Runtime**: API Routes execution (full Node.js)

**Problem**: These runtimes **do not share memory/global state**. Variables in middleware are invisible to API routes.

```typescript
// middleware.ts (Edge Runtime)
sessionMetrics.cacheHit++;  // Increments in Edge Runtime memory

// /metrics/route.ts (Node.js Runtime)
getPrometheusMetrics()      // Reads from Node.js Runtime memory (different!)
```

---

## Current Implementation

**Files**:
- `apps/web/src/lib/metrics/session-cache-metrics.ts` - Metrics manager
- `apps/web/middleware.ts` - Calls `recordCacheHit()`, etc.
- `apps/web/src/app/metrics/route.ts` - Exposes `/metrics` endpoint

**Status**: Code is correct, but runtime isolation prevents data sharing.

---

## Alternative Solutions

### Option 1: Backend API Metrics (Recommended) ✅

**Use existing backend metrics** - `/api/v1/metrics` already tracks:
- Session validation calls
- Response times
- Error rates
- Authentication success/failure

**Pros**:
- Already implemented and working
- More reliable (no runtime isolation)
- Covers same monitoring needs
- OpenTelemetry integration

**Cons**:
- Doesn't track frontend cache specifically
- Backend-centric view

**Implementation**: Already exists! Use Prometheus scrape of `api:8080/metrics`

### Option 2: Redis Shared State

**Store metrics in Redis** - both runtimes can access.

**Implementation**:
```typescript
// middleware.ts
await redis.incr('middleware:cache_hit');

// /metrics/route.ts
const cacheHit = await redis.get('middleware:cache_hit');
```

**Pros**:
- Solves runtime isolation
- Persistent across restarts
- Scalable (multi-instance)

**Cons**:
- Adds Redis dependency for metrics
- Performance overhead (network calls)
- Complexity increase

**Effort**: 2-3 hours implementation + testing

### Option 3: Server-Side Logging + Export

**Log metrics in middleware**, parse logs externally.

**Implementation**:
```typescript
// middleware.ts
console.log('[METRICS] cache_hit=1');

// Prometheus log exporter parses logs
```

**Pros**:
- Simple implementation
- No shared state needed
- Works with existing logging

**Cons**:
- Requires log parsing infrastructure
- Less real-time
- Log volume increase

**Effort**: 1-2 hours (configure log exporter)

### Option 4: HTTP Header Pass-Through

**Pass metrics via response headers**, collect server-side.

**Implementation**:
```typescript
// middleware.ts
response.headers.set('X-Cache-Hit', '1');

// Collector service reads headers from responses
```

**Pros**:
- No shared state
- Works across runtimes

**Cons**:
- Requires collector service
- Headers overhead
- Complex architecture

**Effort**: 3-4 hours

---

## Recommended Approach

### Immediate: Use Backend API Metrics ✅

**Action**: Document that middleware monitoring is available via backend API metrics.

**Prometheus queries**:
```promql
# Session validation rate
rate(meepleai_api_session_validation_total[5m])

# Validation errors
rate(meepleai_api_session_validation_errors_total[5m])

# API response time (includes middleware calls)
histogram_quantile(0.95, meepleai_api_http_request_duration_seconds)
```

**Benefits**:
- Zero additional work
- Already production-ready
- Comprehensive monitoring

### Future: Consider Redis if Needed

**When**:
- Need frontend-specific cache metrics
- Multi-instance deployment
- Want persistent metrics

**Effort**: Low priority (backend metrics sufficient)

---

## Current Status

**Middleware improvements**: ✅ All working
- Timeout prevention: ✅
- Cache TTL optimization: ✅
- Performance validated: ✅

**Metrics**:
- Code structure: ✅ Clean architecture
- Endpoint: ✅ Responds correctly
- Data collection: ⚠️ Runtime isolation limitation
- Alternative: ✅ Backend metrics available

---

## Decision

**Keep current implementation** with documentation:
- Code is clean and well-architected
- Easy to activate later (Option 2: Redis)
- Backend metrics cover monitoring needs
- No blocking issues

**Mark as**: Known limitation, alternative available

---

**Last Updated**: 2026-02-07
**Related**: Issue #3797
**See Also**: Backend API metrics documentation
