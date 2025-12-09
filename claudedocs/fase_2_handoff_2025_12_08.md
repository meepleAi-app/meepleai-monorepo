# FASE 2: Infrastructure Monitoring - Handoff Document

**Date**: 2025-12-08
**Context**: Post-FASE 1 completion, ready for FASE 2 implementation
**Branch**: Main (clean state, FASE 1 merged)

---

## 🎉 FASE 1 Status: COMPLETE

**Issue**: #874 (FASE 1: Dashboard Overview)
**PR**: #2019 (merged to main)
**Status**: ✅ Closed (2025-12-08 19:30 UTC)

### Delivered (6h implementation):
- **16 real-time metrics** (30s polling + circuit breaker)
- **Activity feed** (10 events from 4 sources)
- **AdminLayout** (sidebar navigation foundation)
- **4 reusable components**: StatCard, MetricsGrid, ActivityFeed, AdminLayout
- **Testing**: Backend 13/13 (100%), Frontend 46 tests, E2E 10 scenarios, Chromatic 28 stories
- **Code review**: 92/100 (all critical issues fixed)

### Files Changed:
- **36 files**: 22 created, 14 modified
- **+7,291 lines**

### Documentation:
- ✅ `claudedocs/issue_874_admin_dashboard_fase1_implementation.md`
- ✅ Serena memory: `issue_874_complete_2025_12_08`

---

## 🚀 FASE 2: Infrastructure Monitoring - Ready to Start

### Quick Start for Next Session

```bash
# Option 1: Use /sc:implement command
/sc:implement FASE 2 Infrastructure Monitoring (Issue #890) --uc

# Option 2: Direct implementation
git checkout -b feature/fase-2-infrastructure-monitoring
# Then implement issues #890-#902
```

---

## 📋 FASE 2 Scope (13 Issues)

### Root Epic
**Issue #890**: FASE 2: Infrastructure Monitoring - Multi-Service Health Checks
**Labels**: admin-console, fase-2-infrastructure, deferred, priority-low
**Original Estimate**: 80h (2 weeks)
**Hybrid Estimate**: ~40h (4-5 days compressed)

### Backend Issues (#891-#895)

| Issue | Title | Description | Estimate |
|-------|-------|-------------|----------|
| #891 | InfrastructureMonitoringService.cs | Service status aggregation, health checks | 12h |
| #892 | Extend /health endpoints with detailed metrics | Enhanced health check responses | 8h |
| #893 | Prometheus client integration | Query historical metrics from Prometheus | 15h |
| #894 | GET /api/v1/admin/infrastructure/details | New admin endpoint | 5h |
| #895 | Unit tests InfrastructureMonitoringService (90%+) | xUnit tests | 10h |

**Backend Subtotal**: 50h → **25h compressed** (hybrid approach)

### Frontend Issues (#896-#901)

| Issue | Title | Description | Estimate |
|-------|-------|-------------|----------|
| #896 | ServiceHealthMatrix component | Grid of service health cards | 8h |
| #897 | ServiceCard component | Individual service status display | 6h |
| #898 | MetricsChart component (Chart.js) | Historical metrics visualization | 10h |
| #899 | /admin/infrastructure page | Infrastructure monitoring UI | 12h |
| #900 | Jest tests infrastructure components (90%+) | Vitest unit tests | 8h |
| #901 | Grafana embed iframe setup | Embed existing Grafana dashboards | 6h |

**Frontend Subtotal**: 50h → **25h compressed** (reuse FASE 1 patterns)

### Testing Issue (#902)

| Issue | Title | Description | Estimate |
|-------|-------|-------------|----------|
| #902 | E2E test + Load test (100 concurrent) | Playwright E2E + k6 load test | 7h |

**Testing Subtotal**: 7h → **5h** (reuse E2E patterns)

### FASE 2 Total
**Original**: 107h
**Hybrid**: ~55h (using FASE 1 foundation + hybrid approach)

---

## 🏗️ Technical Context Discovered

### Existing Infrastructure (Verified Working)

#### Health Checks (Already Implemented)
- **Endpoint**: `GET /health`
  ```json
  {
    "status": "Healthy",
    "checks": [
      {"name": "postgres", "status": "Healthy", "duration": 15.9, "tags": ["db", "sql"]},
      {"name": "redis", "status": "Healthy", "duration": 0.9, "tags": ["cache"]},
      {"name": "qdrant", "status": "Healthy", ...}
    ]
  }
  ```
- **Readiness**: `GET /health/ready` (db, cache, vector checks)
- **Liveness**: `GET /health/live` (app running check)
- **Configuration**: `Program.cs:298-328`

#### Services Running & Accessible
- ✅ **Prometheus**: http://localhost:9090 (healthy)
- ✅ **Grafana**: http://localhost:3001 (v11.4.0, healthy)
- ✅ **PostgreSQL**: localhost:5432 (healthy via /health)
- ✅ **Redis**: localhost:6379 (healthy via /health)
- ✅ **Qdrant**: localhost:6333 (inferred from health checks)

#### Observability Stack (from CLAUDE.md)
- **Metrics**: Prometheus (port 9090) + `/metrics` endpoint
- **Visualization**: Grafana (port 3001)
- **Dashboards**: `infra/dashboards/` directory
- **Traces**: HyperDX (port 8180, optional)
- **Health**: Existing `/health` endpoints with tags

---

## 💡 Recommended Implementation Approach

### Strategy: Hybrid (Option C from FASE 1)
**Philosophy**: Proper components + focused scope = fast delivery + future-ready

### Phase 1: Backend (15h → 10h compressed)
1. **InfrastructureMonitoringService** (6h → 4h)
   - Aggregate health status from existing `/health` endpoint
   - Add service-specific details (version, uptime, connections)
   - Query Prometheus for historical metrics (CPU, memory, requests/s)
   - HybridCache (5min TTL for infrastructure stats)

2. **Enhanced Health Endpoints** (3h → 2h)
   - Extend existing health checks with detailed metrics
   - Add Prometheus scrape endpoint (`/metrics` already exists)
   - Service discovery (detect which services are running)

3. **New API Endpoint** (3h → 2h)
   - `GET /api/v1/admin/infrastructure/details`
   - Returns: service list, health status, key metrics, Prometheus queries

4. **Tests** (3h → 2h)
   - xUnit tests for InfrastructureMonitoringService
   - Mock Prometheus HTTP client
   - Health check validation tests

### Phase 2: Frontend Components (20h → 12h compressed)
1. **ServiceCard** (4h → 2h)
   - Reuse StatCard pattern from FASE 1
   - Add: status badge (healthy/degraded/down), uptime, version
   - Actions: view logs, restart (future), drill-down

2. **ServiceHealthMatrix** (6h → 4h)
   - Grid layout (3x2 for 6 services)
   - Color-coded: green (healthy), yellow (degraded), red (down)
   - Real-time updates via same 30s polling as dashboard

3. **MetricsChart** (6h → 3h)
   - Chart.js wrapper (line charts for CPU, memory, latency)
   - Time range selector (1h, 6h, 24h, 7d)
   - Query Prometheus via backend proxy

4. **Infrastructure Page** (4h → 3h)
   - Uses AdminLayout (already exists)
   - ServiceHealthMatrix + individual ServiceCards
   - Grafana embed section (iframe)
   - Export metrics button (CSV)

### Phase 3: Grafana Integration (8h → 5h)
1. **Iframe Embed** (3h → 2h)
   - Embed existing Grafana dashboards
   - Handle auth (iframe sandbox restrictions)
   - Responsive sizing

2. **Dashboard Selection** (3h → 2h)
   - Dropdown to select dashboard
   - Pre-configured: System Overview, Database, API Performance

3. **Fallback** (2h → 1h)
   - If Grafana embed fails, show MetricsChart with Prometheus data

### Phase 4: Testing (12h → 8h)
1. **Unit Tests** (5h → 3h)
   - ServiceCard, ServiceHealthMatrix, MetricsChart (Vitest)
   - InfrastructureMonitoringService (xUnit)
   - 90%+ coverage target

2. **Chromatic Stories** (3h → 2h)
   - ServiceCard (8 stories): healthy/degraded/down states, responsive
   - ServiceHealthMatrix (6 stories): all healthy, mixed states, responsive
   - MetricsChart (5 stories): different time ranges, data patterns

3. **E2E Test** (3h → 2h)
   - Navigate infrastructure → verify services → drill-down → export CSV
   - Grafana embed verification
   - Performance check (<1s load)

4. **Load Test** (1h → 1h)
   - k6 script: 100 concurrent users, 5min duration
   - Target: P95 <500ms
   - Monitor: CPU, memory, DB connections

---

## 🎯 Services to Monitor

| Service | Port | Health Check | Metrics Source |
|---------|------|--------------|----------------|
| **PostgreSQL** | 5432 | `/health` (db tag) | Prometheus `pg_stat_database` |
| **Redis** | 6379 | `/health` (cache tag) | Prometheus `redis_*` |
| **Qdrant** | 6333 | `/health` (vector tag) | Prometheus `qdrant_*` |
| **API** | 8080 | `/health/live` | Prometheus `http_*`, `/metrics` |
| **Prometheus** | 9090 | `/-/healthy` | Self-monitoring |
| **Grafana** | 3001 | `/api/health` | Prometheus `grafana_*` |

**Optional** (if running):
- HyperDX (8180)
- n8n (5678)
- Ollama (11434)

---

## 🛠️ Technical Implementation Notes

### Prometheus Integration

**Query Examples** (for MetricsChart):
```promql
# CPU Usage
rate(process_cpu_seconds_total[5m])

# Memory Usage
process_resident_memory_bytes

# API Request Rate
rate(http_requests_total[5m])

# Database Connections
pg_stat_database_numbackends
```

**C# Prometheus Client**:
```csharp
// Use HttpClient to query Prometheus API
var query = "rate(http_requests_total[5m])";
var response = await _httpClient.GetAsync($"http://localhost:9090/api/v1/query?query={query}");
```

### Grafana Embed

**Iframe URL Pattern**:
```
http://localhost:3001/d/{dashboard-uid}/dashboard-name?orgId=1&from=now-6h&to=now&kiosk&theme=light
```

**Parameters**:
- `kiosk`: Hide Grafana UI chrome
- `theme=light/dark`: Match app theme
- `from/to`: Time range
- `refresh=30s`: Auto-refresh (optional)

**Security**: Grafana anonymous access must be enabled or use API token

---

## 📚 Reusable Patterns from FASE 1

### Backend Patterns
```csharp
// Query pattern
public record GetInfrastructureDetailsQuery() : IQuery<InfrastructureDetailsDto>;

// Handler pattern
public class GetInfrastructureDetailsQueryHandler : IQueryHandler<...>
{
    private readonly IHttpClientFactory _httpClientFactory; // For Prometheus
    private readonly HybridCache _cache; // 5min TTL
    // ... implementation
}

// Endpoint pattern
group.MapGet("/admin/infrastructure/details", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
{
    var (authorized, _, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var query = new GetInfrastructureDetailsQuery();
    var result = await mediator.Send(query, ct).ConfigureAwait(false);
    return Results.Ok(result);
});
```

### Frontend Patterns
```typescript
// Component pattern (reuse StatCard structure)
export interface ServiceCardProps {
  serviceName: string;
  status: 'healthy' | 'degraded' | 'down';
  uptime?: string;
  version?: string;
  responseTime?: number;
}

// Polling pattern (reuse from DashboardClient)
const [services, setServices] = useState<ServiceHealth[]>([]);
const [errorCount, setErrorCount] = useState(0);

const fetchInfrastructure = useCallback(async () => {
  try {
    const data = await api.admin.getInfrastructureDetails();
    setServices(data.services);
    setErrorCount(0);
  } catch (err) {
    setErrorCount(prev => prev + 1);
  }
}, []);

// Stop polling after 3 failures (circuit breaker from FASE 1 fix)
useEffect(() => {
  if (errorCount >= 3) return;
  const interval = setInterval(() => void fetchInfrastructure(), 30000);
  return () => clearInterval(interval);
}, [fetchInfrastructure, errorCount]);
```

### Testing Patterns
```typescript
// Chromatic story (reuse from FASE 1)
export const AllHealthy: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/infrastructure/details',
        method: 'GET',
        status: 200,
        response: { services: [...] }
      }
    ]
  }
};

// Vitest test (reuse pattern)
describe('ServiceCard', () => {
  it('shows healthy status with green badge', () => {
    render(<ServiceCard serviceName="PostgreSQL" status="healthy" />);
    expect(screen.getByText('Healthy')).toHaveClass('bg-green-100');
  });
});

// E2E test (reuse from admin-dashboard-fase1.spec.ts)
test('should display infrastructure page with service matrix', async ({ adminPage }) => {
  await page.goto('/admin/infrastructure');
  await expect(page.getByRole('heading', { name: 'Infrastructure Monitoring' })).toBeVisible();
});
```

---

## 🎯 FASE 2 Implementation Plan (Hybrid Approach)

### Recommended Sequence (Based on FASE 1 Success)

**Day 1: Backend Foundation** (8h → 5h compressed)
1. Create `InfrastructureMonitoringService.cs`
   - Method: `GetServiceHealthAsync()` - aggregates from `/health`
   - Method: `GetPrometheusMetricsAsync()` - queries Prometheus API
   - HybridCache (5min TTL)
2. Create DTOs: `InfrastructureDetailsDto`, `ServiceHealthDto`, `PrometheusMetricDto`
3. Create Query/Handler: `GetInfrastructureDetailsQuery`
4. Add endpoint: `GET /api/v1/admin/infrastructure/details`
5. Write 10 xUnit tests

**Day 2: Frontend Components** (12h → 7h compressed)
1. Create `ServiceCard.tsx` (reuse StatCard pattern)
2. Create `ServiceHealthMatrix.tsx` (reuse MetricsGrid pattern)
3. Create `MetricsChart.tsx` (Chart.js wrapper)
4. Add schemas: `InfrastructureDetailsSchema`, `ServiceHealthSchema`
5. Update `adminClient.ts`: `getInfrastructureDetails()` method

**Day 3: Infrastructure Page** (10h → 6h compressed)
1. Create `/admin/infrastructure/page.tsx` + `infrastructure-client.tsx`
2. Integrate ServiceHealthMatrix + Grafana embed
3. Add export metrics button (CSV download)
4. Implement polling (30s + circuit breaker)

**Day 4: Grafana + Testing** (10h → 7h compressed)
1. Grafana iframe embed with auth handling
2. Write 15 Vitest tests (components 90%+)
3. Create 20 Chromatic stories (3 components × responsive)
4. Create E2E test: infrastructure page flow

**Day 5: Load Test + Polish** (7h → 5h)
1. k6 load test script (100 concurrent, 5min)
2. Run full test suite
3. Performance validation
4. Create PR + code review
5. Merge + close issues

**Total**: ~30h (vs 80h original)

---

## 📊 Key Metrics to Display

### Service Health Matrix (6+ Services)

| Service | Status | Uptime | Response Time | Key Metric |
|---------|--------|--------|---------------|------------|
| PostgreSQL | Healthy | 99.9% | 15ms | 42 connections |
| Redis | Healthy | 99.95% | 1ms | 128MB used |
| Qdrant | Healthy | 99.8% | 25ms | 1.2M vectors |
| API | Healthy | 99.7% | 215ms | 3.5K req/day |
| Prometheus | Healthy | 99.99% | 10ms | 45K samples |
| Grafana | Healthy | 99.9% | 50ms | 12 dashboards |

### Prometheus Metrics to Chart

**CPU Usage** (all services):
- `rate(process_cpu_seconds_total{job="api"}[5m])`
- Time range: 1h, 6h, 24h, 7d

**Memory Usage**:
- `process_resident_memory_bytes{job="api"}`
- `redis_memory_used_bytes`
- `pg_stat_database_size_bytes`

**Request Rate**:
- `rate(http_requests_total[5m])`
- Grouped by endpoint

**Latency** (P95, P99):
- `histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))`

---

## 🧩 Component Design (Reuse FASE 1)

### ServiceCard (Similar to StatCard)
```tsx
<ServiceCard
  serviceName="PostgreSQL"
  status="healthy"
  uptime="99.9%"
  version="16.1"
  responseTime={15}
  connections={42}
  variant="success"
/>
```

### ServiceHealthMatrix (Similar to MetricsGrid)
```tsx
<ServiceHealthMatrix
  services={[
    { name: "PostgreSQL", status: "healthy", ... },
    { name: "Redis", status: "healthy", ... },
    ...
  ]}
  layout="grid-3x2" // 3 cols, 2 rows
/>
```

### MetricsChart (New - Chart.js)
```tsx
<MetricsChart
  title="API Request Rate"
  prometheusQuery="rate(http_requests_total[5m])"
  timeRange="6h"
  chartType="line"
/>
```

---

## 🔧 Files to Create/Modify

### Backend (Estimated 8 files)

**Create**:
1. `InfrastructureMonitoringService.cs` (service layer)
2. `GetInfrastructureDetailsQuery.cs` + Handler
3. DTOs in `Contracts.cs`: `InfrastructureDetailsDto`, `ServiceHealthDto`, `PrometheusMetricDto`
4. `InfrastructureMonitoringServiceTests.cs` (10 tests)
5. `GetInfrastructureDetailsQueryHandlerTests.cs` (8 tests)

**Modify**:
1. `Program.cs`: Extend health checks (if needed)
2. `AnalyticsEndpoints.cs` or create `InfrastructureEndpoints.cs`
3. DI registration (if needed)

### Frontend (Estimated 15 files)

**Create**:
1. `ServiceCard.tsx` + `.stories.tsx` + `.test.tsx`
2. `ServiceHealthMatrix.tsx` + `.stories.tsx` + `.test.tsx`
3. `MetricsChart.tsx` + `.stories.tsx` + `.test.tsx`
4. `/admin/infrastructure/page.tsx` + `infrastructure-client.tsx`
5. `infrastructure-client.stories.tsx`
6. `infrastructure-client.test.tsx`
7. E2E: `admin-infrastructure.spec.ts`

**Modify**:
1. `admin.schemas.ts`: Add infrastructure schemas
2. `adminClient.ts`: Add `getInfrastructureDetails()` method
3. `components/admin/index.ts`: Export new components
4. `package.json`: Add Chart.js if not present

**Check**: `pnpm list chart.js` to see if Chart.js is installed

---

## 🧪 Testing Strategy

### Unit Tests (Backend - 18 tests)
- **InfrastructureMonitoringService**: 10 tests
  - GetServiceHealth (healthy, degraded, down)
  - GetPrometheusMetrics (success, failure, timeout)
  - Caching behavior (hit, miss, expiration)
- **GetInfrastructureDetailsQueryHandler**: 8 tests
  - Full details, partial service availability
  - Prometheus unavailable fallback

### Unit Tests (Frontend - 30+ tests)
- **ServiceCard**: 12 tests (statuses, variants, accessibility)
- **ServiceHealthMatrix**: 10 tests (grid layout, mixed states, responsive)
- **MetricsChart**: 8 tests (data loading, time ranges, chart rendering)
- **InfrastructureClient**: 10 tests (polling, loading, errors, data display)

### Chromatic Stories (20 stories)
- ServiceCard: 8 (healthy, degraded, down, with/without metrics, responsive)
- ServiceHealthMatrix: 6 (all healthy, mixed, all down, responsive)
- MetricsChart: 6 (different metrics, time ranges, loading/error)

### E2E (5 scenarios)
1. Navigate to infrastructure → verify all services
2. Drill-down service → verify details
3. Export metrics → download CSV
4. Grafana embed → verify iframe loads
5. Complete flow: dashboard → infrastructure → back

### Load Test (k6)
```javascript
// k6 script structure
import http from 'k6/http';

export let options = {
  vus: 100, // 100 concurrent users
  duration: '5m',
  thresholds: {
    http_req_duration: ['p(95)<500'], // P95 <500ms
  },
};

export default function() {
  http.get('http://localhost:8080/api/v1/admin/infrastructure/details');
}
```

---

## 🚨 Potential Challenges & Solutions

### Challenge 1: Prometheus Query Performance
**Issue**: Complex PromQL queries might be slow
**Solution**:
- Cache results (5min TTL)
- Pre-aggregate in Prometheus recording rules
- Fallback to simpler queries if timeout

### Challenge 2: Grafana Embed CORS
**Issue**: Iframe might be blocked by Grafana CORS policy
**Solution**:
- Configure Grafana `allow_embedding = true`
- Use `X-Frame-Options: SAMEORIGIN`
- Fallback to MetricsChart if embed fails

### Challenge 3: Chart.js Bundle Size
**Issue**: Chart.js adds ~200KB to bundle
**Solution**:
- Use tree-shaking (import only needed components)
- Lazy load MetricsChart component
- Consider Recharts as lighter alternative

### Challenge 4: Service Discovery
**Issue**: Not all services always running (HyperDX, n8n, Ollama optional)
**Solution**:
- Graceful degradation (show "Not Running" vs error)
- Query health check, handle timeouts
- Display only running services

---

## 📝 Definition of Done (FASE 2)

### Functionality
- [ ] InfrastructureMonitoringService aggregates 6+ services
- [ ] Prometheus integration retrieves historical metrics
- [ ] Infrastructure page displays ServiceHealthMatrix
- [ ] Grafana dashboards embedded (or fallback chart)
- [ ] Export metrics to CSV functional
- [ ] All 13 sub-issues (#891-#902) resolved

### Performance
- [ ] Infrastructure page load <1s
- [ ] Prometheus queries <500ms (P95)
- [ ] Load test: 100 concurrent users, P95 <500ms
- [ ] Polling overhead <2% CPU

### Quality
- [ ] Backend tests 90%+ (18 xUnit tests)
- [ ] Frontend tests 90%+ (30+ Vitest tests)
- [ ] E2E tests (5 scenarios)
- [ ] Chromatic stories (20 stories)
- [ ] 0 TypeScript errors, 0 new warnings

### Documentation
- [ ] InfrastructureMonitoringService documented (XML comments)
- [ ] Component API docs (Storybook autodocs)
- [ ] Infrastructure page user guide
- [ ] Prometheus query reference

---

## 🎁 Quick Reference for Next Session

### Essential Commands

```bash
# Start FASE 2
git checkout -b feature/fase-2-infrastructure-monitoring

# Check infrastructure
curl http://localhost:9090/-/healthy  # Prometheus
curl http://localhost:3001/api/health  # Grafana
curl http://localhost:8080/health  # API health checks

# Test as you build
cd apps/api && dotnet test --filter "FullyQualifiedName~Infrastructure"
cd apps/web && pnpm test -- --run src/components/admin/__tests__/Service*

# Verify builds
cd apps/api && dotnet build
cd apps/web && pnpm typecheck

# Create PR when done
git add -A
git commit -m "feat(admin): FASE 2 - Infrastructure Monitoring ..."
git push -u origin feature/fase-2-infrastructure-monitoring
gh pr create --title "..." --body-file .pr-body-fase2.md --base main
```

### Key Files to Reference

**FASE 1 Patterns**:
- `apps/web/src/app/admin/dashboard-client.tsx` (polling + circuit breaker)
- `apps/web/src/components/admin/StatCard.tsx` (card component pattern)
- `apps/web/src/components/admin/MetricsGrid.tsx` (grid layout)
- `apps/api/src/Api/Services/AdminStatsService.cs` (parallel queries + caching)

**Health Checks**:
- `apps/api/src/Api/Program.cs:298-328` (existing health configuration)
- Endpoint: `curl http://localhost:8080/health | jq`

**Grafana**:
- URL: http://localhost:3001
- Dashboards: Check `infra/dashboards/` directory
- API: http://localhost:3001/api/

**Prometheus**:
- URL: http://localhost:9090
- Query: http://localhost:9090/api/v1/query?query=up
- Metrics: http://localhost:8080/metrics

---

## 💾 Context Saved

### Serena Memories
- ✅ `issue_874_complete_2025_12_08`: FASE 1 implementation complete
- ✅ `admin_console_implementation_session_2025-11-11`: Full 7-week roadmap

### Documentation
- ✅ `claudedocs/issue_874_admin_dashboard_fase1_implementation.md`
- ✅ `claudedocs/fase_2_handoff_2025_12_08.md` (this file)

### Git State
- **Branch**: `main` (clean)
- **FASE 1**: Merged (PR #2019, Issue #874 closed)
- **FASE 2**: Ready (branch will be created in next session)

---

## 🎯 Success Criteria for FASE 2

### Must-Have (MVP)
- [x] 6+ services monitored (PostgreSQL, Redis, Qdrant, API, Prometheus, Grafana)
- [x] Health status display (healthy/degraded/down with visual indicators)
- [x] Basic metrics (uptime, response time, version)
- [x] Infrastructure page with polling (30s + circuit breaker)
- [x] 90%+ test coverage (backend + frontend)

### Should-Have (Full FASE 2)
- [x] Prometheus integration (historical metrics)
- [x] MetricsChart component (CPU, memory, requests over time)
- [x] Grafana embed (existing dashboards)
- [x] E2E test (complete infrastructure flow)
- [x] Load test (100 concurrent, P95 <500ms)

### Nice-to-Have (Defer to FASE 3)
- [ ] Service drill-down modals (detailed logs, traces)
- [ ] Alert configuration from UI
- [ ] Service restart actions
- [ ] Historical incident timeline

---

## 📈 Expected Outcomes

### Deliverables
- **Backend**: InfrastructureMonitoringService, Prometheus integration, API endpoint
- **Frontend**: 3 components (ServiceCard, ServiceHealthMatrix, MetricsChart), infrastructure page
- **Testing**: 48 tests (18 backend, 30 frontend), 5 E2E scenarios, 20 Chromatic stories, 1 k6 load test

### Performance
- **Infrastructure page load**: <1s (reusing FASE 1 optimizations)
- **Prometheus queries**: <200ms cached, <500ms uncached
- **Load test**: 100 concurrent users, P95 <500ms

### Quality
- **Test coverage**: 90%+ (enforced)
- **Code review**: Target 90+ score (based on FASE 1 patterns)
- **Accessibility**: WCAG AA (reusing FASE 1 components)

---

## 🚀 Recommended Next Steps

### Immediate (Next Session Start)
1. Read this handoff document
2. Read `issue_874_complete_2025_12_08` memory
3. Create branch: `git checkout -b feature/fase-2-infrastructure-monitoring`
4. Start with backend (InfrastructureMonitoringService.cs)

### During Implementation
- **Reuse FASE 1 patterns**: Don't reinvent, adapt
- **Test as you build**: Run tests after each component
- **Commit frequently**: Smaller commits = easier review
- **Use Morphllm** for bulk edits (like updating test fixtures)
- **Parallel development**: Create backend + frontend components concurrently

### Before PR
- [ ] Run full test suite (backend + frontend + E2E)
- [ ] Code review with code-reviewer agent
- [ ] Update all 13 FASE 2 issues (#890-#902)
- [ ] Comprehensive PR description (reuse FASE 1 template)

---

## 🎓 Lessons from FASE 1 to Apply

### What Worked ✅
1. **Hybrid approach**: Save 50% time by building proper foundation focused on core features
2. **Parallel queries**: All backend queries run concurrently
3. **Circuit breaker polling**: Stops after 3 failures (critical fix from code review)
4. **Morphllm for bulk edits**: Updated 6 test instances in one call
5. **Comprehensive stories**: 28 Chromatic stories caught visual regressions

### What to Avoid ❌
1. **Don't wait for full test suite**: Test incrementally, don't batch
2. **Don't skip error handling**: Circuit breaker is critical for production
3. **Don't hardcode locales**: Use variables (deferred but noted)
4. **Don't create inefficient queries**: N+1 query pattern to avoid

### Tools to Use
- **Serena MCP**: Project memory, search patterns
- **Morphllm MCP**: Bulk test updates, pattern edits
- **Sequential MCP**: Code review agent (feature-dev:code-reviewer)
- **TodoWrite**: Task tracking throughout session
- **Chart.js or Recharts**: For metrics visualization

---

## 🎯 Final Checklist for Next Session

Before starting FASE 2 implementation:
- [ ] Read this handoff document completely
- [ ] Review FASE 1 implementation doc (`issue_874_admin_dashboard_fase1_implementation.md`)
- [ ] Verify infrastructure running (Prometheus, Grafana, health endpoints)
- [ ] Create FASE 2 branch
- [ ] Set up TodoWrite task list (22 steps from this doc)

During implementation:
- [ ] Follow hybrid approach (proper foundation + focused scope)
- [ ] Reuse FASE 1 patterns (StatCard → ServiceCard, MetricsGrid → ServiceHealthMatrix)
- [ ] Test incrementally (don't batch testing)
- [ ] Use code-reviewer agent before PR
- [ ] Update all 13 issues on GitHub

After completion:
- [ ] Create comprehensive PR (reuse FASE 1 template)
- [ ] Merge and close all FASE 2 issues
- [ ] Update Serena memory with FASE 2 completion
- [ ] Clean up branch

---

## 📚 Reference Documentation

### Project Docs
- CLAUDE.md: Architecture, stack, testing, observability
- docs/01-architecture/overview/system-architecture.md
- docs/05-operations/: Runbooks, monitoring guides

### FASE 1 Docs
- claudedocs/issue_874_admin_dashboard_fase1_implementation.md
- Serena memory: issue_874_complete_2025_12_08

### External Docs (if needed)
- [Prometheus Query API](https://prometheus.io/docs/prometheus/latest/querying/api/)
- [Grafana Embed](https://grafana.com/docs/grafana/latest/dashboards/share-dashboards-panels/#embed-a-dashboard-or-panel)
- [Chart.js React](https://react-chartjs-2.js.org/)
- [ASP.NET Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)

---

## 🏁 Session Handoff Complete

**FASE 1**: ✅ **100% Complete** (merged, closed, documented)
**FASE 2**: ⏸️ **Ready to Start** (infrastructure verified, plan ready)

**Next Session Command**:
```bash
/sc:implement FASE 2 Infrastructure Monitoring (Issues #890-#902) --uc
```

Or just say: **"Continue FASE 2"** and reference this document!

**Estimated FASE 2 Time**: 4-5 days (30h compressed from 80h using hybrid approach)

---

**Handoff Created**: 2025-12-08
**By**: Claude Code (Sonnet 4.5 1M context)
**For**: FASE 2: Infrastructure Monitoring implementation

🚀 **Ready for seamless continuation!**
