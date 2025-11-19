# 🔀 [Performance] Request Deduplication Cache

**Priority**: 🟢 MEDIUM
**Complexity**: Medium
**Estimated Time**: 4-6 hours
**Dependencies**: None

## 🎯 Objective

Implement request deduplication to prevent identical simultaneous requests from hitting the backend multiple times, reducing server load and improving performance.

## 📋 Context

**Source**: Code Review Backend-Frontend Interactions
**Issue**: Identical requests (e.g., user profile fetch) can fire simultaneously
**Impact**: Low-Medium - Reduces unnecessary backend calls, improves performance
**Use Case**: React components mounting simultaneously and fetching same data

## 🔧 Problem Scenario

```typescript
// Current behavior ❌
Component A mounts → GET /api/v1/users/profile
Component B mounts → GET /api/v1/users/profile  (duplicate!)
Component C mounts → GET /api/v1/users/profile  (duplicate!)

// Result: 3 identical backend calls

// Desired behavior ✅
Component A mounts → GET /api/v1/users/profile (initiates request)
Component B mounts → Reuses pending request from A
Component C mounts → Reuses pending request from A

// Result: 1 backend call, 3 components get same data
```

## ✅ Task Checklist

### Implementation
- [ ] Create `RequestCache` class with TTL-based deduplication
- [ ] Generate cache key from: method + URL + body hash
- [ ] Store pending promises in WeakMap/Map
- [ ] Implement TTL cleanup (default: 100ms)
- [ ] Add size limit (max 100 concurrent requests)
- [ ] LRU eviction when limit reached

### Integration
- [ ] Integrate `dedupe()` in HttpClient
- [ ] Opt-in for GET requests (default)
- [ ] Opt-out via `RequestOptions.skipDedup`
- [ ] POST/PUT/DELETE opt-out by default (not idempotent)
- [ ] Cache key includes authentication context

### Configuration
- [ ] Environment variable `NEXT_PUBLIC_REQUEST_DEDUP_ENABLED`
- [ ] Configurable TTL (default: 100ms)
- [ ] Configurable max cache size (default: 100)
- [ ] Per-endpoint override configuration

### Observability
- [ ] Log cache hits/misses with correlation ID
- [ ] Prometheus metrics:
  - `http_client_dedup_cache_hits_total`
  - `http_client_dedup_cache_misses_total`
  - `http_client_dedup_cache_size` (gauge)
- [ ] DevTools integration for debugging

### Testing
- [ ] Unit tests for cache key generation
- [ ] Test simultaneous identical requests
- [ ] Test cache expiration (TTL)
- [ ] Test cache size limit
- [ ] Test opt-out mechanism
- [ ] Integration tests with React components
- [ ] Test with different auth contexts

### Documentation
- [ ] Document deduplication strategy
- [ ] Explain cache key generation
- [ ] Document when deduplication applies
- [ ] Add troubleshooting guide
- [ ] Examples with React Query/SWR

## 📁 Files to Create/Modify

```
apps/web/src/lib/api/core/requestCache.ts (NEW)
apps/web/src/lib/api/core/httpClient.ts (MODIFY)
apps/web/src/lib/api/__tests__/requestCache.test.ts (NEW)
apps/web/src/lib/api/__tests__/httpClient.test.ts (MODIFY)
docs/04-frontend/request-deduplication.md (NEW)
apps/web/.env.example (MODIFY)
```

## 💡 Implementation Example

```typescript
// requestCache.ts
class RequestCache {
  private pending = new Map<string, Promise<any>>();
  private ttl: number;
  private maxSize: number;

  constructor(ttl = 100, maxSize = 100) {
    this.ttl = ttl;
    this.maxSize = maxSize;
  }

  private generateKey(method: string, url: string, body?: unknown): string {
    const bodyHash = body ? hashObject(body) : '';
    return `${method}:${url}:${bodyHash}`;
  }

  async dedupe<T>(
    key: string,
    fetcher: () => Promise<T>,
  ): Promise<T> {
    // Check if already pending
    const cached = this.pending.get(key);
    if (cached) {
      metrics.increment('http_client_dedup_cache_hits_total');
      return cached;
    }

    // Evict LRU if cache full
    if (this.pending.size >= this.maxSize) {
      const firstKey = this.pending.keys().next().value;
      this.pending.delete(firstKey);
    }

    // Execute fetcher and cache promise
    const promise = fetcher();
    this.pending.set(key, promise);
    metrics.increment('http_client_dedup_cache_misses_total');

    // Remove from cache after TTL
    setTimeout(() => this.pending.delete(key), this.ttl);

    return promise;
  }
}

// httpClient.ts usage
async get<T>(path: string, schema?: z.ZodSchema<T>, options?: RequestOptions): Promise<T | null> {
  if (options?.skipDedup) {
    return this.fetchImpl(path, { method: 'GET', ...options });
  }

  const cacheKey = this.requestCache.generateKey('GET', path);
  return this.requestCache.dedupe(cacheKey, () =>
    this.fetchImpl(path, { method: 'GET', ...options })
  );
}
```

## 🔗 References

- [React Query Deduplication](https://tanstack.com/query/latest/docs/react/guides/request-deduplication)
- [SWR Deduplication](https://swr.vercel.app/docs/advanced/performance#deduplication)
- [Axios Request Deduplication](https://github.com/berndzahradnik/axios-deduplicator)

## 📊 Acceptance Criteria

- ✅ Identical simultaneous requests deduplicated
- ✅ Cache key includes method + URL + body + auth
- ✅ TTL-based automatic cleanup (100ms default)
- ✅ Size limit with LRU eviction
- ✅ Opt-in for GET, opt-out for POST/PUT/DELETE
- ✅ Prometheus metrics for cache hits/misses
- ✅ Test coverage >= 95%
- ✅ Documentation complete
- ✅ No breaking changes

## ⚙️ Configuration

```env
NEXT_PUBLIC_REQUEST_DEDUP_ENABLED=true
NEXT_PUBLIC_REQUEST_DEDUP_TTL=100
NEXT_PUBLIC_REQUEST_DEDUP_MAX_SIZE=100
```

## 📈 Expected Impact

- **Reduced Backend Load**: ~20-30% for frequently accessed endpoints
- **Faster UI**: Components get data from shared request
- **Better UX**: Consistent data across components
- **Lower Costs**: Fewer database queries

## 🏷️ Labels

`priority: medium`, `type: enhancement`, `area: frontend`, `effort: medium`, `performance`, `sprint: 5`
