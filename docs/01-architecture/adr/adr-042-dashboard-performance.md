# ADR-042: Dashboard Performance Architecture

## Status

**Accepted** (2026-02-09)

## Context

The Dashboard Hub (Epic #3901) is the primary entry point for admin users, aggregating data from multiple bounded contexts (Administration, GameManagement, UserLibrary, SessionTracking, KnowledgeBase). Performance is critical as this page loads on every admin login and is polled every 30 seconds for real-time updates.

Issue #3981 tracks the formal measurement and validation of performance targets established in Epic #3901.

## Decision

### Performance Targets

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Cached API response (p99) | < 500ms | Testcontainers + Stopwatch |
| Uncached API response | < 2s | Testcontainers + Stopwatch |
| Cache hit rate | > 80% | Redis metrics in test |
| Lighthouse Performance | > 90 | Lighthouse CI (3-run avg) |
| Lighthouse Accessibility | > 95 | Lighthouse CI |
| LCP (Largest Contentful Paint) | < 2.5s | Lighthouse CI |
| FID (First Input Delay) | < 100ms | Lighthouse CI |
| CLS (Cumulative Layout Shift) | < 0.1 | Lighthouse CI |

### Caching Strategy

**Backend (HybridCache)**:
- **Distributed cache**: Redis with 5-minute TTL per user (`dashboard:{userId}`)
- **Local cache**: In-memory with 1-minute TTL (reduces Redis round-trips)
- **Invalidation**: Tag-based (`["dashboard-api", "user:{userId}"]`), triggered by `DashboardCacheInvalidationEventHandler` on config changes
- **Compression**: Disabled (`DisableCompression`) for latency optimization

**Frontend (TanStack Query)**:
- **Stale time**: 25 seconds (data considered fresh)
- **Polling interval**: 30 seconds (real-time updates)
- **GC time**: 5 minutes (cached data retention)
- **Stale-while-revalidate**: Serves cached data immediately, refetches in background

### Query Execution

The `GetDashboardQueryHandler` executes 6 data-fetching tasks in parallel using `Task.WhenAll`:
1. Library stats (game count, favorites)
2. Recent activity (last 7 days)
3. Session stats (active sessions)
4. AI usage stats (chat count, agent interactions)
5. System health (uptime, error rate)
6. User preferences

This parallel execution is key to meeting the <2s uncached target.

### SSE Streaming

Real-time dashboard updates use Server-Sent Events (SSE) via `/api/v1/dashboard/stream`:
- 30-second heartbeat interval
- Redis pub/sub for cross-instance delivery
- Automatic reconnection on client disconnect

## Testing Strategy

### Backend Performance Tests
- **File**: `tests/Api.Tests/.../Performance/DashboardEndpointPerformanceTests.cs`
- **Infrastructure**: Testcontainers (PostgreSQL pgvector:pg16, Redis 7.4-alpine)
- **Metrics**: Cached p99 latency, uncached max latency, cache hit rate
- **Execution**: Excluded from CI (`[Trait("Skip", "CI")]`), run in dedicated performance suite

### Frontend Tests
- **Unit**: `dashboard-client.test.tsx` (render performance, loading/error states)
- **Integration**: `dashboard-api-integration.test.tsx` (MSW mocks, TanStack Query behavior, polling)
- **E2E**: 7+ Playwright test files covering performance, accessibility, security, user journeys

### Lighthouse CI
- **Desktop config**: `lighthouserc.json` (3 runs, desktop preset)
- **Mobile config**: `lighthouserc.mobile.json` (3 runs, mobile throttling)
- **URLs audited**: `/admin/dashboard`, `/dashboard`, `/library`, `/shared-games`, `/settings`, `/admin/users`, `/editor`

## Consequences

### Positive
- Sub-second cached responses provide responsive admin experience
- Parallel query execution maximizes throughput
- Multi-layer caching reduces database load
- Comprehensive test coverage prevents performance regressions

### Negative
- Cache invalidation adds complexity to data modification flows
- Testcontainers-based performance tests require Docker and are slower to run
- HybridCache dependency adds infrastructure requirement (Redis)

### Risks
- Cache stampede on cold start (mitigated by staggered TTLs)
- Redis unavailability degrades to uncached responses (graceful fallback)

## References

- Epic #3901: Dashboard Hub Core MVP
- Issue #3907: Dashboard API Implementation
- Issue #3909: Cache Strategy
- Issue #3915: E2E Test Suite
- Issue #3981: Performance Measurement (this ADR)
