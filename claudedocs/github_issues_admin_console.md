# GitHub Issues - Admin Console Implementation

**Total Issues**: 48 (15 FASE 1 + 13 FASE 2 + 12 FASE 3 + 8 FASE 4)

**Labels to Create**:
- `admin-console` (all issues)
- `fase-1-dashboard` (issues 1-15)
- `fase-2-infrastructure` (issues 16-28)
- `fase-3-management` (issues 29-40)
- `fase-4-advanced` (issues 41-48)
- `backend` (backend tasks)
- `frontend` (frontend tasks)
- `testing` (testing tasks)
- `mvp` (FASE 1-2 issues)

---

## FASE 1: Dashboard Overview (Issues #1-15)

### Epic Issue #1: FASE 1 - Dashboard Overview

**Title**: FASE 1: Dashboard Overview - Centralized Admin Dashboard

**Description**:
Implement centralized admin dashboard with system status, key metrics, activity feed, and quick actions.

**User Stories**:
- US-1: As admin, I want to see overall system status at a glance
- US-2: As admin, I want to navigate easily between admin sections

**Acceptance Criteria**:
- [ ] Dashboard shows 12+ real-time metrics (polling 30s)
- [ ] Activity feed with last 10 events
- [ ] Performance: Load time <1s, Time to Interactive <2s
- [ ] Test coverage: Backend 90%+, Frontend 90%+
- [ ] E2E test complete: login → dashboard → navigation
- [ ] Accessibility: WCAG AA compliance
- [ ] Responsive: Desktop (1920x1080) + Tablet (768x1024)

**Effort**: 80h (2 weeks)

**Dependencies**: None (can start immediately)

**Labels**: `admin-console`, `fase-1-dashboard`, `mvp`, `epic`

---

### Backend Tasks (Issues #2-7)

#### Issue #2: AdminDashboardService - Core Service Implementation

**Title**: [Backend] AdminDashboardService.cs - GetSystemStatsAsync()

**Description**:
Create AdminDashboardService with GetSystemStatsAsync() method to aggregate metrics from existing services.

**Tasks**:
- [ ] Create `Services/AdminDashboardService.cs`
- [ ] Implement GetSystemStatsAsync() - aggregate from UserManagementService, SessionManagementService, AiRequestLogService, CacheService
- [ ] Create interface `IAdminDashboardService`
- [ ] Register service in DI container (Program.cs)

**Implementation Notes**:
```csharp
public class AdminDashboardService : IAdminDashboardService
{
    private readonly UserManagementService _userService;
    private readonly SessionManagementService _sessionService;
    private readonly AiRequestLogService _aiService;
    private readonly HybridCacheService _cacheService;

    public async Task<SystemStatsDto> GetSystemStatsAsync()
    {
        // Aggregate metrics from services
        var userStats = await _userService.GetStatsAsync();
        var sessionStats = await _sessionService.GetStatsAsync();
        var aiStats = await _aiService.GetStatsAsync();
        var cacheStats = await _cacheService.GetStatsAsync();

        return new SystemStatsDto
        {
            ActiveUsers = userStats.ActiveCount,
            TotalUsers = userStats.TotalCount,
            ActiveSessions = sessionStats.ActiveCount,
            // ... 12+ metrics total
        };
    }
}
```

**Effort**: 6h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `backend`, `mvp`

**Depends On**: None

---

#### Issue #3: AdminDashboardService - Metrics Aggregation

**Title**: [Backend] Aggregate metrics from existing services (Users, Sessions, AI, Cache)

**Description**:
Implement metric aggregation logic to collect data from 4+ existing services for dashboard display.

**Services to Integrate**:
- UserManagementService (active users, total users, new users today)
- SessionManagementService (active sessions, avg duration)
- AiRequestLogService (requests/min, avg response time, error rate)
- HybridCacheService (hit rate, evictions, memory usage)

**Tasks**:
- [ ] Implement parallel aggregation (Task.WhenAll)
- [ ] Handle service failures gracefully (partial stats if service down)
- [ ] Calculate derived metrics (% changes, trends)
- [ ] Add Serilog logging for aggregation

**Performance Requirements**:
- Total aggregation time <200ms
- Parallel service calls

**Effort**: 8h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `backend`, `mvp`

**Depends On**: #2

---

#### Issue #4: Dashboard Stats Endpoint

**Title**: [Backend] GET /api/v1/admin/dashboard/stats endpoint

**Description**:
Create RESTful endpoint to expose dashboard statistics.

**Endpoint Specification**:
```
GET /api/v1/admin/dashboard/stats
Authorization: Cookie OR X-API-Key (Admin role required)
Response: 200 OK
{
  "systemStatus": {
    "overallStatus": "healthy",
    "servicesUp": 6,
    "servicesTotal": 6,
    "criticalAlerts": 0,
    "warnings": 3
  },
  "metrics": {
    "activeUsers": 1234,
    "apiRequestsPerMin": 156,
    "activeChats": 89,
    // ... 9 more metrics
  },
  "lastUpdated": "2025-11-11T14:30:00Z"
}
```

**Tasks**:
- [ ] Add endpoint to v1Api group in Program.cs
- [ ] Admin role authorization
- [ ] Call AdminDashboardService.GetSystemStatsAsync()
- [ ] Return SystemStatsDto
- [ ] Add Swagger/OpenAPI documentation

**Effort**: 4h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `backend`, `mvp`

**Depends On**: #2, #3

---

#### Issue #5: Activity Feed Service

**Title**: [Backend] Activity Feed Service - GetRecentActivityAsync()

**Description**:
Create service to retrieve recent admin-relevant activities (user logins, uploads, errors, config changes).

**Data Sources**:
- User login/logout events (from audit logs)
- PDF uploads (from PdfStorageService logs)
- Critical errors (from Serilog/Seq)
- Configuration changes (from ConfigurationService audit)

**Tasks**:
- [ ] Create ActivityFeedService.cs
- [ ] Implement GetRecentActivityAsync(int count = 10)
- [ ] Query multiple sources and merge by timestamp
- [ ] Return ActivityEventDto list (sorted by timestamp desc)
- [ ] Include event type, user, description, timestamp

**Implementation Note**:
```csharp
public class ActivityEventDto
{
    public string EventType { get; set; } // "user_login", "pdf_upload", "error", "config_change"
    public string UserEmail { get; set; }
    public string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } // "info", "warning", "error"
}
```

**Effort**: 6h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `backend`, `mvp`

**Depends On**: None

---

#### Issue #6: HybridCache Setup for Dashboard Stats

**Title**: [Backend] HybridCache configuration for dashboard stats (1min TTL)

**Description**:
Configure HybridCache for dashboard statistics to improve performance and reduce load on source services.

**Tasks**:
- [ ] Configure HybridCache entry for "admin:dashboard:stats"
- [ ] Set TTL to 1 minute (balance freshness vs performance)
- [ ] Cache invalidation on critical config changes
- [ ] Add cache hit/miss metrics

**Cache Strategy**:
```csharp
var stats = await _cache.GetOrCreateAsync(
    "admin:dashboard:stats",
    async entry =>
    {
        entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
        return await AggregateStatsAsync();
    }
);
```

**Performance Target**:
- Cached response: <10ms
- Cache miss (first load): <200ms

**Effort**: 3h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `backend`, `performance`, `mvp`

**Depends On**: #2, #3

---

#### Issue #7: Unit Tests - AdminDashboardService (90% coverage)

**Title**: [Testing] Unit tests for AdminDashboardService (90%+ coverage)

**Description**:
Write comprehensive unit tests for AdminDashboardService using xUnit + Moq.

**Test Cases**:
- [ ] GetSystemStatsAsync returns correct aggregated stats
- [ ] Handles service failures gracefully (partial stats)
- [ ] Parallel aggregation works correctly
- [ ] Cache hit/miss scenarios
- [ ] Metrics calculation (derived metrics, trends)
- [ ] Activity feed ordering and filtering

**Coverage Target**: 90%+

**Test Files**:
- `tests/Api.Tests/Services/AdminDashboardServiceTests.cs`

**Effort**: 3h

**Assignee**: Backend developer OR QA

**Labels**: `admin-console`, `fase-1-dashboard`, `testing`, `mvp`

**Depends On**: #2, #3, #5

---

### Frontend Tasks (Issues #8-13)

#### Issue #8: AdminLayout Component - Shared Layout

**Title**: [Frontend] AdminLayout component (sidebar, header, breadcrumbs)

**Description**:
Create shared layout component for all admin pages with sidebar navigation, header, and breadcrumbs.

**Components to Create**:
- `components/admin/AdminLayout.tsx` (main layout wrapper)
- `components/admin/AdminSidebar.tsx` (collapsible sidebar)
- `components/admin/AdminHeader.tsx` (header with user menu)
- `components/admin/AdminBreadcrumbs.tsx` (breadcrumb navigation)

**Features**:
- [ ] Collapsible sidebar (desktop: default expanded, mobile: default collapsed)
- [ ] Navigation menu with icons (Dashboard, Infrastructure, Users, API Keys, Configuration, Reports, Alerts)
- [ ] Badge counts on menu items (e.g., "Alerts (3)")
- [ ] Breadcrumb navigation (Home > Admin > Dashboard)
- [ ] User menu (profile, settings, logout)
- [ ] Responsive design (mobile sidebar overlay)

**Design Reference**: Follow existing admin pages style (AdminLayout should match `/admin/users`, `/admin/analytics`)

**Effort**: 10h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `frontend`, `mvp`

**Depends On**: None

---

#### Issue #9: StatCard Component - Reusable Metric Card

**Title**: [Frontend] StatCard reusable component for metric display

**Description**:
Create reusable component for displaying single metric with icon, value, label, and optional trend.

**Component API**:
```tsx
interface StatCardProps {
  icon: ReactNode;
  label: string;
  value: string | number;
  trend?: { value: number; direction: 'up' | 'down' };
  severity?: 'success' | 'warning' | 'error' | 'info';
  onClick?: () => void;
}

<StatCard
  icon={<UsersIcon />}
  label="Active Users"
  value={1234}
  trend={{ value: 5.2, direction: 'up' }}
  severity="success"
/>
```

**Features**:
- [ ] Icon + Label + Value display
- [ ] Optional trend indicator (up/down arrow with percentage)
- [ ] Color coding by severity (success: green, warning: yellow, error: red)
- [ ] Hover effect and optional click handler
- [ ] Loading skeleton state
- [ ] Responsive sizing

**Effort**: 4h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `frontend`, `component`, `mvp`

**Depends On**: None

---

#### Issue #10: MetricsGrid Component - 4x3 Grid Layout

**Title**: [Frontend] MetricsGrid component (4x3 responsive grid)

**Description**:
Create grid component to display 12 StatCards in 4x3 layout (responsive: 4 cols desktop, 2 cols tablet, 1 col mobile).

**Component API**:
```tsx
interface MetricsGridProps {
  metrics: Array<{
    id: string;
    icon: ReactNode;
    label: string;
    value: string | number;
    trend?: { value: number; direction: 'up' | 'down' };
  }>;
  loading?: boolean;
}
```

**Features**:
- [ ] Responsive grid (4 cols desktop, 2 cols tablet, 1 col mobile)
- [ ] Loading skeleton for all 12 cards
- [ ] Smooth transitions on value updates
- [ ] Empty state if no metrics

**Effort**: 6h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `frontend`, `component`, `mvp`

**Depends On**: #9

---

#### Issue #11: ActivityFeed Component - Event Stream

**Title**: [Frontend] ActivityFeed component for recent events

**Description**:
Create component to display recent admin activities in timeline format.

**Component API**:
```tsx
interface ActivityFeedProps {
  events: Array<{
    id: string;
    eventType: string;
    userEmail: string;
    description: string;
    timestamp: string;
    severity: 'info' | 'warning' | 'error';
  }>;
  maxEvents?: number; // default 10
  loading?: boolean;
}
```

**Features**:
- [ ] Timeline layout with icons per event type
- [ ] Color coding by severity
- [ ] Relative timestamps ("2 mins ago", "5 mins ago")
- [ ] User avatar + email
- [ ] Scrollable list (max 10 visible)
- [ ] "View All Activity" link to full audit log page

**Effort**: 5h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `frontend`, `component`, `mvp`

**Depends On**: None

---

#### Issue #12: Dashboard Page - Main Implementation

**Title**: [Frontend] /pages/admin/index.tsx - Dashboard page implementation

**Description**:
Implement main admin dashboard page integrating all components (AdminLayout, MetricsGrid, ActivityFeed).

**Page Structure**:
```tsx
<AdminLayout>
  <h1>Admin Dashboard</h1>

  {/* System Status Cards (4 cards) */}
  <SystemStatusSection />

  {/* Metrics Grid (12 cards) */}
  <MetricsGrid metrics={metrics} />

  {/* Activity Feed */}
  <ActivityFeed events={recentActivity} />

  {/* Quick Actions */}
  <QuickActionsSection />
</AdminLayout>
```

**Features**:
- [ ] Page layout with sections
- [ ] API integration (`GET /api/v1/admin/dashboard/stats`)
- [ ] Polling mechanism (refresh every 30s)
- [ ] Error handling (service unavailable message)
- [ ] Loading states for all sections
- [ ] Quick action buttons (Manage Users, View Analytics, Configuration, View Alerts)

**Effort**: 8h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `frontend`, `mvp`

**Depends On**: #8, #10, #11

---

#### Issue #13: API Integration + Polling Logic

**Title**: [Frontend] Dashboard API integration + 30s polling

**Description**:
Implement API client integration for dashboard with automatic refresh every 30s.

**Implementation**:
```tsx
// Use React Query for polling
const { data, isLoading, error } = useQuery(
  ['admin', 'dashboard', 'stats'],
  () => api.admin.getDashboardStats(),
  {
    refetchInterval: 30000, // 30s
    refetchOnWindowFocus: true,
    staleTime: 25000, // Consider stale after 25s
  }
);
```

**Tasks**:
- [ ] Add `api.admin.getDashboardStats()` to API client
- [ ] Setup React Query polling (30s interval)
- [ ] Handle loading states (skeleton during fetch)
- [ ] Handle errors (show toast notification)
- [ ] Pause polling when tab hidden (Page Visibility API)

**Performance Considerations**:
- Pause polling when tab not visible
- Debounce rapid refetches
- Cancel in-flight requests on unmount

**Effort**: 4h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `frontend`, `mvp`

**Depends On**: #12

---

### Testing Tasks (Issues #14-16)

#### Issue #14: Jest Unit Tests - Frontend Components (90% coverage)

**Title**: [Testing] Jest tests for dashboard components (90%+ coverage)

**Description**:
Write comprehensive Jest + React Testing Library tests for all dashboard components.

**Test Files**:
- `__tests__/components/admin/AdminLayout.test.tsx`
- `__tests__/components/admin/StatCard.test.tsx`
- `__tests__/components/admin/MetricsGrid.test.tsx`
- `__tests__/components/admin/ActivityFeed.test.tsx`
- `__tests__/pages/admin/index.test.tsx`

**Test Cases (per component)**:
- [ ] Renders correctly with props
- [ ] Handles loading states
- [ ] Handles error states
- [ ] User interactions (click, hover)
- [ ] Responsive behavior (desktop, tablet, mobile)
- [ ] Accessibility (ARIA labels, keyboard navigation)

**Coverage Target**: 90%+

**Effort**: 3h

**Assignee**: Frontend developer OR QA

**Labels**: `admin-console`, `fase-1-dashboard`, `testing`, `mvp`

**Depends On**: #8, #9, #10, #11, #12

---

#### Issue #15: E2E Playwright Test - Dashboard Flow

**Title**: [Testing] E2E test - Dashboard complete flow

**Description**:
Write Playwright E2E test for complete dashboard user journey.

**Test Scenario**:
```typescript
test('Admin dashboard flow', async ({ page }) => {
  // 1. Login as admin
  await page.goto('/login');
  await page.fill('[name="email"]', 'admin@meepleai.dev');
  await page.fill('[name="password"]', 'Demo123!');
  await page.click('button[type="submit"]');

  // 2. Navigate to dashboard
  await page.goto('/admin');
  await expect(page.locator('h1')).toContainText('Admin Dashboard');

  // 3. Verify 12 metrics visible
  const metricCards = page.locator('[data-testid="stat-card"]');
  await expect(metricCards).toHaveCount(12);

  // 4. Verify activity feed
  const activities = page.locator('[data-testid="activity-item"]');
  await expect(activities).toHaveCount(10);

  // 5. Wait for auto-refresh (30s)
  await page.waitForTimeout(31000);

  // 6. Verify metrics updated
  await expect(metricCards.first()).toHaveAttribute('data-updated', 'true');

  // 7. Click quick action (navigate to users)
  await page.click('[data-testid="quick-action-users"]');
  await expect(page).toHaveURL('/admin/users');
});
```

**Test Coverage**:
- [ ] Login → Dashboard navigation
- [ ] All metrics displayed (12 cards)
- [ ] Activity feed populated (10 events)
- [ ] Auto-refresh after 30s
- [ ] Quick actions navigation

**Effort**: 5h

**Assignee**: QA OR Frontend developer

**Labels**: `admin-console`, `fase-1-dashboard`, `testing`, `e2e`, `mvp`

**Depends On**: #12, #13

---

#### Issue #16: Performance + Accessibility Tests

**Title**: [Testing] Performance test (dashboard load <1s) + Accessibility audit (WCAG AA)

**Description**:
Validate dashboard performance and accessibility compliance.

**Performance Tests**:
- [ ] Dashboard load time <1s (P95)
- [ ] Time to Interactive <2s (P95)
- [ ] Lighthouse performance score >90
- [ ] Core Web Vitals: LCP <2.5s, FID <100ms, CLS <0.1

**Accessibility Tests**:
- [ ] Lighthouse accessibility score 100
- [ ] WCAG AA compliance (axe-core)
- [ ] Keyboard navigation (Tab, Enter, Escape)
- [ ] Screen reader support (ARIA labels)
- [ ] Color contrast ratio ≥4.5:1

**Tools**:
- Lighthouse CI
- axe-core (via Playwright)
- WebPageTest

**Effort**: 3h

**Assignee**: QA

**Labels**: `admin-console`, `fase-1-dashboard`, `testing`, `performance`, `accessibility`, `mvp`

**Depends On**: #12, #13

---

## FASE 2: Infrastructure Monitoring (Issues #17-29)

### Epic Issue #17: FASE 2 - Infrastructure Monitoring

**Title**: FASE 2: Infrastructure Monitoring - Multi-Service Health Checks

**Description**:
Implement real-time infrastructure monitoring with health checks for all backend services (PostgreSQL, Redis, Qdrant, n8n, Seq, Jaeger) and Prometheus/Grafana integration.

**User Stories**:
- US-3: As admin, I want to monitor health of all backend services
- US-4: As admin, I want to visualize Prometheus metrics in console

**Acceptance Criteria**:
- [ ] Health matrix for 6+ services (PG, Redis, Qdrant, n8n, Seq, Jaeger)
- [ ] Real-time status updates (polling 30s)
- [ ] Historical metrics charts (24h window)
- [ ] Grafana dashboards embedded
- [ ] Prometheus query builder functional
- [ ] Test coverage: Backend 90%+, Frontend 90%+
- [ ] E2E test: Monitor service health → drill-down → export metrics
- [ ] Performance: Load test 100 concurrent users OK

**Effort**: 80h (2 weeks)

**Dependencies**: FASE 1 complete (#1-16)

**Labels**: `admin-console`, `fase-2-infrastructure`, `mvp`, `epic`

---

### Backend Tasks (Issues #18-22)

#### Issue #18: InfrastructureMonitoringService - Core Service

**Title**: [Backend] InfrastructureMonitoringService.cs implementation

**Description**:
Create service to aggregate health check data from all infrastructure services.

**Services to Monitor**:
- PostgreSQL (connection, query performance, database size)
- Redis (memory usage, keys count, hit rate)
- Qdrant (collections count, vectors indexed, query latency)
- n8n (workflows count, executions today, error rate)
- Seq (log ingestion rate, storage size)
- Jaeger (traces count, spans indexed)

**Tasks**:
- [ ] Create InfrastructureMonitoringService.cs
- [ ] Implement GetServiceHealthAsync(serviceName)
- [ ] Implement GetAllServicesHealthAsync()
- [ ] Aggregate health status (healthy, degraded, unhealthy)
- [ ] Add Serilog logging

**Implementation Note**:
```csharp
public class ServiceHealthDto
{
    public string ServiceName { get; set; }
    public string Status { get; set; } // "healthy", "degraded", "unhealthy"
    public Dictionary<string, object> Metrics { get; set; }
    public DateTime LastChecked { get; set; }
}
```

**Effort**: 8h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `backend`, `mvp`

**Depends On**: None

---

#### Issue #19: Extended Health Checks - Detailed Per Service

**Title**: [Backend] Extend /health endpoints with detailed service metrics

**Description**:
Enhance existing health check endpoints to return detailed metrics per service (not just healthy/unhealthy).

**Current State**: `/health`, `/health/ready`, `/health/live` return basic status

**New Endpoints**:
```
GET /health/postgresql - Detailed PostgreSQL health
GET /health/redis - Detailed Redis health
GET /health/qdrant - Detailed Qdrant health
GET /health/n8n - Detailed n8n health
GET /health/seq - Detailed Seq health
GET /health/jaeger - Detailed Jaeger health
```

**Response Example**:
```json
{
  "service": "postgresql",
  "status": "healthy",
  "metrics": {
    "connections": { "active": 23, "max": 100 },
    "databaseSize": "12.4 GB",
    "slowQueries": 2,
    "queryLatency": { "p50": 12, "p95": 45, "p99": 120 }
  },
  "timestamp": "2025-11-11T14:30:00Z"
}
```

**Effort**: 10h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `backend`, `mvp`

**Depends On**: #18

---

#### Issue #20: Prometheus Client Integration

**Title**: [Backend] Prometheus client integration for historical metrics

**Description**:
Integrate Prometheus client to query historical metrics for infrastructure services.

**Tasks**:
- [ ] Add Prometheus.Client NuGet package
- [ ] Create PrometheusQueryService.cs
- [ ] Implement QueryRangeAsync(query, start, end) for time-series data
- [ ] Preset queries for common scenarios (error rate, latency, memory usage)
- [ ] Handle Prometheus connection failures gracefully

**Preset Queries**:
- Error rate (last 24h): `rate(http_requests_total{status=~"5.."}[5m])`
- Memory usage: `process_resident_memory_bytes`
- Request latency P95: `histogram_quantile(0.95, http_request_duration_seconds_bucket)`

**Effort**: 8h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `backend`, `prometheus`, `mvp`

**Depends On**: #18

---

#### Issue #21: Infrastructure Details Endpoint

**Title**: [Backend] GET /api/v1/admin/infrastructure/details endpoint

**Description**:
Create endpoint to expose aggregated infrastructure health and metrics.

**Endpoint Specification**:
```
GET /api/v1/admin/infrastructure/details
Authorization: Admin role required
Response: 200 OK
{
  "services": [
    {
      "name": "postgresql",
      "status": "healthy",
      "metrics": { ... },
      "lastChecked": "2025-11-11T14:30:00Z"
    },
    // ... 5 more services
  ],
  "overallStatus": "healthy",
  "criticalCount": 0,
  "degradedCount": 1
}
```

**Tasks**:
- [ ] Add endpoint to v1Api group
- [ ] Call InfrastructureMonitoringService.GetAllServicesHealthAsync()
- [ ] Include Prometheus historical data if available
- [ ] Add Swagger documentation

**Effort**: 5h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `backend`, `mvp`

**Depends On**: #18, #19, #20

---

#### Issue #22: Unit Tests - InfrastructureMonitoringService

**Title**: [Testing] Unit tests for InfrastructureMonitoringService (90%+ coverage)

**Description**:
Write comprehensive unit tests for InfrastructureMonitoringService using xUnit + Moq.

**Test Cases**:
- [ ] GetAllServicesHealthAsync returns correct status for all services
- [ ] Handles individual service failures (continues checking others)
- [ ] Aggregates overall status correctly (healthy if all healthy, degraded if 1+ degraded)
- [ ] Prometheus queries work correctly
- [ ] Health check timeout handling

**Coverage Target**: 90%+

**Effort**: 4h

**Assignee**: Backend developer OR QA

**Labels**: `admin-console`, `fase-2-infrastructure`, `testing`, `mvp`

**Depends On**: #18, #19, #20

---

### Frontend Tasks (Issues #23-27)

#### Issue #23: ServiceHealthMatrix Component

**Title**: [Frontend] ServiceHealthMatrix component - Service grid layout

**Description**:
Create component to display health status for all services in grid layout.

**Component API**:
```tsx
interface ServiceHealthMatrixProps {
  services: Array<{
    name: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    metrics: Record<string, any>;
  }>;
  loading?: boolean;
  onServiceClick?: (serviceName: string) => void;
}
```

**Features**:
- [ ] Grid layout (2x3 on desktop, 1 col on mobile)
- [ ] ServiceCard for each service
- [ ] Color coding (green: healthy, yellow: degraded, red: unhealthy)
- [ ] Click to drill-down to service details
- [ ] Loading skeleton for all 6 services

**Effort**: 10h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `frontend`, `component`, `mvp`

**Depends On**: None

---

#### Issue #24: ServiceCard Component - Individual Service Display

**Title**: [Frontend] ServiceCard component (health, metrics, actions)

**Description**:
Create card component to display single service health with key metrics and actions.

**Component API**:
```tsx
interface ServiceCardProps {
  service: {
    name: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    metrics: Record<string, any>;
    lastChecked: string;
  };
  onClick?: () => void;
}
```

**Features**:
- [ ] Service icon + name
- [ ] Status badge (color-coded)
- [ ] Key metrics (3-5 most important)
- [ ] Last checked timestamp
- [ ] Actions dropdown (View Details, Export Metrics, Restart Service)
- [ ] Hover effect

**Effort**: 8h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `frontend`, `component`, `mvp`

**Depends On**: #23

---

#### Issue #25: MetricsChart Component - Time-Series Chart

**Title**: [Frontend] MetricsChart component (Chart.js integration)

**Description**:
Create reusable chart component for displaying time-series metrics (last 24h).

**Component API**:
```tsx
interface MetricsChartProps {
  title: string;
  data: Array<{ timestamp: string; value: number }>;
  unit?: string;
  chartType?: 'line' | 'area' | 'bar';
  height?: number;
}
```

**Libraries**:
- Chart.js (or Recharts as alternative)

**Features**:
- [ ] Line chart for time-series
- [ ] Responsive sizing
- [ ] Tooltip with timestamp + value
- [ ] Legend
- [ ] Zoom/pan for long time ranges
- [ ] Loading state (skeleton chart)

**Effort**: 8h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `frontend`, `component`, `charts`, `mvp`

**Depends On**: None

---

#### Issue #26: Infrastructure Page Implementation

**Title**: [Frontend] /pages/admin/infrastructure.tsx - Infrastructure page

**Description**:
Implement main infrastructure monitoring page integrating all components.

**Page Structure**:
```tsx
<AdminLayout>
  <h1>Infrastructure Monitoring</h1>

  {/* Service Health Matrix (6 services) */}
  <ServiceHealthMatrix services={services} />

  {/* Selected Service Details (drill-down) */}
  {selectedService && (
    <ServiceDetailsPanel service={selectedService} />
  )}

  {/* Historical Metrics Charts */}
  <MetricsChartsSection />

  {/* Grafana Embed (optional) */}
  <GrafanaEmbed dashboardUrl={grafanaUrl} />
</AdminLayout>
```

**Features**:
- [ ] Service health matrix (6 services)
- [ ] Drill-down to service details (modal or side panel)
- [ ] Historical metrics charts (last 24h)
- [ ] Export metrics button (CSV/JSON)
- [ ] Grafana embed iframe (optional, if configured)

**Effort**: 6h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `frontend`, `mvp`

**Depends On**: #23, #24, #25

---

#### Issue #27: Jest Tests - Infrastructure Components

**Title**: [Testing] Jest tests for infrastructure components (90%+ coverage)

**Description**:
Write comprehensive Jest tests for all infrastructure monitoring components.

**Test Files**:
- `__tests__/components/admin/ServiceHealthMatrix.test.tsx`
- `__tests__/components/admin/ServiceCard.test.tsx`
- `__tests__/components/admin/MetricsChart.test.tsx`
- `__tests__/pages/admin/infrastructure.test.tsx`

**Test Cases**:
- [ ] Renders correctly with props
- [ ] Handles service health states (healthy, degraded, unhealthy)
- [ ] Drill-down click behavior
- [ ] Chart rendering with data
- [ ] Export metrics functionality
- [ ] Responsive behavior

**Coverage Target**: 90%+

**Effort**: 3h

**Assignee**: Frontend developer OR QA

**Labels**: `admin-console`, `fase-2-infrastructure`, `testing`, `mvp`

**Depends On**: #23, #24, #25, #26

---

### Integration Tasks (Issues #28-29)

#### Issue #28: Grafana Embed Integration

**Title**: [Integration] Grafana embed iframe setup

**Description**:
Integrate existing Grafana dashboards into admin console via iframe embed.

**Tasks**:
- [ ] Configure Grafana allow-iframe setting
- [ ] Create GrafanaEmbed.tsx component
- [ ] Add dashboard URL configuration (appsettings.json)
- [ ] Handle authentication (Grafana API key OR public dashboard)
- [ ] Responsive iframe sizing

**Configuration**:
```json
{
  "Observability": {
    "Grafana": {
      "DashboardUrls": {
        "Infrastructure": "http://localhost:3001/d/infrastructure",
        "API": "http://localhost:3001/d/api-metrics"
      }
    }
  }
}
```

**Effort**: 3h

**Assignee**: DevOps OR Frontend developer

**Labels**: `admin-console`, `fase-2-infrastructure`, `integration`, `grafana`, `mvp`

**Depends On**: #26

---

#### Issue #29: E2E Test + Load Test - Infrastructure Page

**Title**: [Testing] E2E test infrastructure page + Load test 100 concurrent

**Description**:
Write Playwright E2E test for infrastructure monitoring flow + load test with 100 concurrent users.

**E2E Test Scenario**:
```typescript
test('Infrastructure monitoring flow', async ({ page }) => {
  // 1. Navigate to infrastructure page
  await page.goto('/admin/infrastructure');

  // 2. Verify all 6 services displayed
  const serviceCards = page.locator('[data-testid="service-card"]');
  await expect(serviceCards).toHaveCount(6);

  // 3. Click on PostgreSQL service
  await page.click('[data-testid="service-card-postgresql"]');

  // 4. Verify detailed metrics shown
  await expect(page.locator('[data-testid="service-details"]')).toBeVisible();

  // 5. Export metrics to CSV
  await page.click('[data-testid="export-metrics-csv"]');

  // 6. Verify download started
  const download = await page.waitForEvent('download');
  await expect(download.suggestedFilename()).toContain('postgresql_metrics');
});
```

**Load Test**:
- Tool: k6 OR Artillery
- Scenario: 100 concurrent users polling infrastructure endpoint every 30s
- Duration: 5 minutes
- Success Criteria: P95 response time <500ms, error rate <1%

**Effort**: 4h (E2E) + 3h (load test) = 7h total

**Assignee**: QA

**Labels**: `admin-console`, `fase-2-infrastructure`, `testing`, `e2e`, `load-testing`, `mvp`

**Depends On**: #26, #28

---

## FASE 3: Enhanced Management (Issues #30-41)

### Epic Issue #30: FASE 3 - Enhanced Management

**Title**: FASE 3: Enhanced Management - API Keys + User Management + Bulk Ops

**Description**:
Implement advanced UI for API key management, enhanced user management with bulk operations, and user activity timeline.

**User Stories**:
- US-5: As admin, I want to manage API keys with dedicated UI
- US-6: As admin, I want advanced user management features

**Acceptance Criteria**:
- [ ] API key management UI complete (create, list, revoke, stats)
- [ ] User management enhancements (bulk import/export, advanced filters)
- [ ] User activity timeline functional
- [ ] Bulk operations with progress tracking
- [ ] Test coverage: Backend 90%+, Frontend 90%+
- [ ] Security audit passed (no API key leaks, XSS prevention)
- [ ] E2E tests: Create API key → use → revoke flow
- [ ] Stress test: Bulk operation on 1000+ users completed

**Effort**: 80h (2 weeks)

**Dependencies**: FASE 2 complete (#17-29)

**Labels**: `admin-console`, `fase-3-management`, `epic`

---

### Backend Tasks (Issues #31-34)

#### Issue #31: ApiKeyManagementService Enhancements

**Title**: [Backend] ApiKeyManagementService - Add usage statistics

**Description**:
Enhance existing ApiKeyManagementService to track and report API key usage statistics.

**Current State**: ApiKeyManagementService exists, basic CRUD operations

**New Features**:
- [ ] Track API key usage (request count, last used timestamp)
- [ ] GetApiKeyUsageStatsAsync(apiKeyId)
- [ ] GetAllApiKeysWithStatsAsync() - includes usage for each key
- [ ] Usage stored in `api_keys` table (new columns: `usage_count`, `last_used_at`)

**Database Migration**:
```sql
ALTER TABLE api_keys
ADD COLUMN usage_count INT DEFAULT 0,
ADD COLUMN last_used_at TIMESTAMP NULL;
```

**Effort**: 6h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-3-management`, `backend`

**Depends On**: None

---

#### Issue #32: UserManagementService - Bulk Operations

**Title**: [Backend] UserManagementService - Bulk import/export + operations

**Description**:
Add bulk operations to UserManagementService (import from CSV, export to CSV, bulk password reset, bulk role change).

**New Methods**:
- [ ] BulkImportUsersAsync(csvContent) - Parse CSV and create users
- [ ] BulkExportUsersAsync(filters) - Export users to CSV
- [ ] BulkPasswordResetAsync(userIds) - Reset passwords for multiple users
- [ ] BulkRoleChangeAsync(userIds, newRole) - Change roles in bulk

**CSV Format (Import/Export)**:
```csv
email,displayName,role,isActive
user1@example.com,User One,User,true
user2@example.com,User Two,Editor,true
```

**Validation**:
- Email format validation
- Role validation (User, Editor, Admin)
- Duplicate email handling
- Max 1000 users per bulk operation

**Effort**: 8h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-3-management`, `backend`

**Depends On**: None

---

#### Issue #33: CSV Import/Export Utility

**Title**: [Backend] CSV import/export utility for bulk operations

**Description**:
Create reusable CSV parsing and generation utility for bulk operations.

**Utility Class**:
```csharp
public class CsvUtility
{
    public static Task<List<T>> ParseCsvAsync<T>(string csvContent);
    public static Task<string> GenerateCsvAsync<T>(List<T> data);
}
```

**Features**:
- [ ] Generic CSV parser (CsvHelper library)
- [ ] Header mapping
- [ ] Validation (required fields, format)
- [ ] Error reporting (row number, column, error message)
- [ ] Large file support (streaming)

**Effort**: 4h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-3-management`, `backend`, `utility`

**Depends On**: None

---

#### Issue #34: Unit Tests - Bulk Operations

**Title**: [Testing] Unit tests for bulk operations (90%+ coverage)

**Description**:
Write comprehensive unit tests for ApiKeyManagementService and UserManagementService bulk operations.

**Test Cases**:
- [ ] Bulk import users from valid CSV
- [ ] Bulk import with invalid data (validation errors)
- [ ] Bulk export users with filters
- [ ] Bulk password reset (multiple users)
- [ ] Bulk role change (multiple users)
- [ ] API key usage stats calculation
- [ ] Large file handling (1000+ rows)

**Coverage Target**: 90%+

**Effort**: 2h

**Assignee**: Backend developer OR QA

**Labels**: `admin-console`, `fase-3-management`, `testing`

**Depends On**: #31, #32, #33

---

### Frontend Tasks (Issues #35-39)

#### Issue #35: API Keys Management Page

**Title**: [Frontend] /pages/admin/api-keys.tsx - API key management UI

**Description**:
Create new page for comprehensive API key management.

**Page Features**:
- [ ] API keys list table (DataTable component)
- [ ] Filters: Active/Revoked, By User, By Expiration Date
- [ ] Columns: Key (masked), User, Scopes, Created, Last Used, Expires, Status, Actions
- [ ] Create key button → modal
- [ ] Revoke key button (with confirmation)
- [ ] Usage statistics per key (requests count, last used)
- [ ] Bulk operations: Revoke multiple, Export list

**Effort**: 12h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-3-management`, `frontend`

**Depends On**: #31

---

#### Issue #36: API Key Creation Modal

**Title**: [Frontend] API key creation modal with scopes selection

**Description**:
Create modal for API key creation with scopes selection and expiration date.

**Modal Features**:
- [ ] Key name input
- [ ] User selection dropdown (create key for user)
- [ ] Scopes checkboxes (read:users, write:users, read:games, write:games, admin:*)
- [ ] Expiration date picker (30 days, 90 days, 1 year, never)
- [ ] Generate key button
- [ ] Display key ONCE after generation (copy to clipboard)
- [ ] Warning: "Save this key, it won't be shown again"

**Implementation Note**:
```tsx
<Modal open={isOpen} onClose={onClose}>
  <h2>Create API Key</h2>

  <Input label="Key Name" />
  <Select label="User" options={users} />
  <CheckboxGroup label="Scopes" options={scopeOptions} />
  <DatePicker label="Expires At" />

  <Button onClick={handleGenerate}>Generate Key</Button>

  {generatedKey && (
    <Alert severity="warning">
      <p>Save this key, it won't be shown again:</p>
      <Code>{generatedKey}</Code>
      <Button onClick={copyToClipboard}>Copy</Button>
    </Alert>
  )}
</Modal>
```

**Effort**: 6h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-3-management`, `frontend`, `component`

**Depends On**: #35

---

#### Issue #37: Advanced Filters Component (Reusable)

**Title**: [Frontend] FilterPanel component - Reusable advanced filters

**Description**:
Create reusable component for advanced filtering (used in API keys, users, etc.).

**Component API**:
```tsx
interface FilterPanelProps {
  filters: Array<{
    id: string;
    label: string;
    type: 'text' | 'select' | 'date' | 'boolean';
    options?: Array<{ label: string; value: string }>;
  }>;
  values: Record<string, any>;
  onChange: (values: Record<string, any>) => void;
  onReset: () => void;
}
```

**Features**:
- [ ] Multiple filter types (text, select, date, boolean)
- [ ] Collapsible panel (default collapsed)
- [ ] Apply filters button
- [ ] Reset filters button
- [ ] Active filters count badge
- [ ] Responsive layout

**Effort**: 8h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-3-management`, `frontend`, `component`, `reusable`

**Depends On**: None

---

#### Issue #38: User Activity Timeline Component

**Title**: [Frontend] UserActivityTimeline component - User action history

**Description**:
Create component to display user activity history in timeline format.

**Component API**:
```tsx
interface UserActivityTimelineProps {
  userId: string;
  activities: Array<{
    id: string;
    action: string;
    timestamp: string;
    metadata: Record<string, any>;
  }>;
  loading?: boolean;
}
```

**Features**:
- [ ] Vertical timeline layout
- [ ] Activity type icons (login, logout, upload, edit, delete)
- [ ] Relative timestamps
- [ ] Metadata expandable (click to show details)
- [ ] Pagination (load more)
- [ ] Filter by activity type

**Effort**: 10h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-3-management`, `frontend`, `component`

**Depends On**: None

---

#### Issue #39: Bulk Operations UI Component

**Title**: [Frontend] BulkActionBar component - Bulk operations UI

**Description**:
Create reusable component for bulk operations with selection, actions, and progress tracking.

**Component API**:
```tsx
interface BulkActionBarProps {
  selectedCount: number;
  totalCount: number;
  actions: Array<{
    id: string;
    label: string;
    icon: ReactNode;
    onClick: (selectedIds: string[]) => Promise<void>;
    dangerous?: boolean;
  }>;
  onClearSelection: () => void;
}
```

**Features**:
- [ ] Selected count display (e.g., "5 of 100 selected")
- [ ] Select all checkbox
- [ ] Action buttons (with icons)
- [ ] Dangerous actions (red color, confirmation required)
- [ ] Progress bar during bulk operation
- [ ] Success/error toast notifications
- [ ] Cancel operation button

**Effort**: 8h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-3-management`, `frontend`, `component`, `reusable`

**Depends On**: None

---

### Testing Tasks (Issues #40-41)

#### Issue #40: Jest Tests - Management Components

**Title**: [Testing] Jest tests for management components (90%+ coverage)

**Description**:
Write comprehensive Jest tests for all management components.

**Test Files**:
- `__tests__/pages/admin/api-keys.test.tsx`
- `__tests__/components/admin/FilterPanel.test.tsx`
- `__tests__/components/admin/BulkActionBar.test.tsx`
- `__tests__/components/admin/UserActivityTimeline.test.tsx`

**Test Cases**:
- [ ] API keys list renders correctly
- [ ] Create key modal flow
- [ ] Key revocation with confirmation
- [ ] Filters apply correctly
- [ ] Bulk selection and actions
- [ ] User activity timeline displays correctly
- [ ] Progress tracking during bulk operations

**Coverage Target**: 90%+

**Effort**: 4h

**Assignee**: Frontend developer OR QA

**Labels**: `admin-console`, `fase-3-management`, `testing`

**Depends On**: #35, #36, #37, #38, #39

---

#### Issue #41: E2E Test + Security Audit + Stress Test

**Title**: [Testing] E2E API key flow + Security audit + Stress test (1000+ users)

**Description**:
Write E2E test for complete API key management flow, perform security audit, and run stress test for bulk operations.

**E2E Test Scenario**:
```typescript
test('API key management flow', async ({ page }) => {
  // 1. Navigate to API keys page
  await page.goto('/admin/api-keys');

  // 2. Create new API key
  await page.click('[data-testid="create-api-key"]');
  await page.fill('[name="keyName"]', 'Test Key');
  await page.click('[data-testid="generate-key"]');

  // 3. Copy generated key
  const keyValue = await page.locator('[data-testid="generated-key"]').textContent();
  await page.click('[data-testid="copy-key"]');

  // 4. Use key in API request (verify it works)
  const response = await fetch('/api/v1/games', {
    headers: { 'X-API-Key': keyValue }
  });
  expect(response.status).toBe(200);

  // 5. Revoke key
  await page.goto('/admin/api-keys');
  await page.click('[data-testid="revoke-key"]');
  await page.click('[data-testid="confirm-revoke"]');

  // 6. Verify key no longer works
  const response2 = await fetch('/api/v1/games', {
    headers: { 'X-API-Key': keyValue }
  });
  expect(response2.status).toBe(401);
});
```

**Security Audit**:
- [ ] API key not leaked in logs
- [ ] Key masked in UI (show only last 4 chars)
- [ ] XSS prevention (input sanitization)
- [ ] CSRF protection on key creation/revocation
- [ ] Rate limiting on key creation (max 10/day per admin)

**Stress Test**:
- Tool: k6
- Scenario: Bulk user import (1000 users CSV)
- Success Criteria: Completes in <30s, no errors

**Effort**: 2h (E2E) + 5h (security) + 5h (stress) = 12h total

**Assignee**: QA + Security engineer

**Labels**: `admin-console`, `fase-3-management`, `testing`, `security`, `e2e`, `stress-testing`

**Depends On**: #35, #36

---

## FASE 4: Advanced Features (Issues #42-49)

### Epic Issue #42: FASE 4 - Advanced Features

**Title**: FASE 4: Advanced Features - Reporting System + Advanced Alerting

**Description**:
Implement automated reporting system with scheduling and email delivery, plus advanced alerting configuration UI.

**User Stories**:
- US-7: As admin, I want to generate scheduled reports
- US-8: As admin, I want to configure advanced alerts

**Acceptance Criteria**:
- [ ] Report builder with 4+ templates functional
- [ ] Scheduled report generation working (daily/weekly/monthly)
- [ ] Email delivery integrated
- [ ] Alert configuration UI complete
- [ ] Test coverage: Backend 90%+, Frontend 90%+
- [ ] E2E test: Create report → schedule → receive email
- [ ] Email delivery test passed

**Effort**: 40h (1 week)

**Dependencies**: FASE 3 complete (#30-41)

**Labels**: `admin-console`, `fase-4-advanced`, `epic`

---

### Backend Tasks (Issues #43-46)

#### Issue #43: ReportingService - Report Generation Engine

**Title**: [Backend] ReportingService.cs - Report generation + scheduling

**Description**:
Create service for automated report generation with scheduling capabilities.

**Features**:
- [ ] GenerateReportAsync(templateName, parameters)
- [ ] ScheduleReportAsync(reportId, schedule) - cron-based scheduling
- [ ] 4 predefined templates: SystemHealth, UserActivity, AIUsage, ContentMetrics
- [ ] Export formats: PDF, CSV, JSON
- [ ] Background job integration (Hangfire OR Quartz.NET)

**Report Templates**:
1. **SystemHealth**: Uptime, error rate, service health, performance metrics
2. **UserActivity**: Active users, new registrations, login frequency, top users
3. **AIUsage**: AI requests, costs, model usage, quality scores
4. **ContentMetrics**: Games count, PDFs uploaded, RuleSpecs created, vector index size

**Database Tables**:
- `admin_reports` (id, name, template, schedule, config, created_at)
- `admin_report_executions` (id, report_id, status, output_url, executed_at)

**Effort**: 10h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-4-advanced`, `backend`

**Depends On**: None

---

#### Issue #44: Report Templates Implementation

**Title**: [Backend] Report templates - 4 predefined templates

**Description**:
Implement 4 predefined report templates (SystemHealth, UserActivity, AIUsage, ContentMetrics).

**Template Classes**:
```csharp
public interface IReportTemplate
{
    Task<ReportData> GenerateAsync(ReportParameters parameters);
}

public class SystemHealthReportTemplate : IReportTemplate { }
public class UserActivityReportTemplate : IReportTemplate { }
public class AIUsageReportTemplate : IReportTemplate { }
public class ContentMetricsReportTemplate : IReportTemplate { }
```

**Each Template Includes**:
- Data collection logic (query services)
- Data aggregation and calculations
- Chart generation (for PDF reports)
- Summary statistics

**Effort**: 6h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-4-advanced`, `backend`

**Depends On**: #43

---

#### Issue #45: Email Delivery Integration

**Title**: [Backend] Email delivery integration for scheduled reports

**Description**:
Integrate email delivery for sending generated reports to admins.

**Email Service**:
- Use existing email infrastructure (SendGrid OR SMTP)
- EmailService.SendReportAsync(report, recipientEmails)

**Email Template**:
```html
<h2>Admin Report: {ReportName}</h2>
<p>Report generated at: {GeneratedAt}</p>
<p>Report period: {StartDate} - {EndDate}</p>

<h3>Summary</h3>
<ul>
  <li>Total users: {TotalUsers}</li>
  <li>Active sessions: {ActiveSessions}</li>
  <!-- ... -->
</ul>

<p>Attached: {ReportFileName}</p>
```

**Configuration** (`appsettings.json`):
```json
{
  "Reporting": {
    "EmailRecipients": ["admin@meepleai.dev"],
    "EmailFrom": "reports@meepleai.dev"
  }
}
```

**Effort**: 5h

**Assignee**: Backend developer

**Labels**: `admin-console`, `fase-4-advanced`, `backend`, `email`

**Depends On**: #43, #44

---

#### Issue #46: Unit Tests - ReportingService

**Title**: [Testing] Unit tests for ReportingService (90%+ coverage)

**Description**:
Write comprehensive unit tests for ReportingService and report templates.

**Test Cases**:
- [ ] Report generation for each template
- [ ] Scheduled report creation and execution
- [ ] Email delivery success/failure
- [ ] PDF/CSV/JSON export formats
- [ ] Background job scheduling
- [ ] Report execution history

**Coverage Target**: 90%+

**Effort**: 4h

**Assignee**: Backend developer OR QA

**Labels**: `admin-console`, `fase-4-advanced`, `testing`

**Depends On**: #43, #44, #45

---

### Frontend Tasks (Issues #47-48)

#### Issue #47: Report Builder UI

**Title**: [Frontend] /pages/admin/reports.tsx - Report builder page

**Description**:
Create report builder UI with template selection, parameter configuration, and scheduling.

**Page Features**:
- [ ] Report template selection (4 templates)
- [ ] Parameter configuration form (date range, filters)
- [ ] Preview report button
- [ ] Schedule configuration (daily/weekly/monthly, cron expression)
- [ ] Email recipients input
- [ ] Report history table (past executions)
- [ ] Download report button (PDF/CSV/JSON)

**Wizard Steps**:
1. Select template
2. Configure parameters
3. Choose schedule (now, daily, weekly, monthly)
4. Set email recipients
5. Review and create

**Effort**: 6h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-4-advanced`, `frontend`

**Depends On**: #43

---

#### Issue #48: Alert Configuration UI Enhancement

**Title**: [Frontend] Enhanced /pages/admin/alerts.tsx - Alert config UI

**Description**:
Enhance existing alert configuration page with advanced features.

**Current State**: Basic alert list page exists

**Enhancements**:
- [ ] Alert rule builder UI (metric, threshold, severity)
- [ ] Multi-channel notification config (email, Slack, PagerDuty)
- [ ] Alert throttling config (1/hour, escalation)
- [ ] Alert history viewer with ack/resolve states
- [ ] Test alert button (dry-run)
- [ ] Alert templates (common scenarios)

**Effort**: 4h

**Assignee**: Frontend developer

**Labels**: `admin-console`, `fase-4-advanced`, `frontend`

**Depends On**: None (enhances existing page)

---

### Testing Task (Issue #49)

#### Issue #49: E2E Test + Email Delivery Validation

**Title**: [Testing] E2E test report generation flow + Email delivery test

**Description**:
Write E2E test for complete report generation and scheduling flow, plus email delivery validation.

**E2E Test Scenario**:
```typescript
test('Report generation and scheduling', async ({ page }) => {
  // 1. Navigate to reports page
  await page.goto('/admin/reports');

  // 2. Create new report
  await page.click('[data-testid="create-report"]');

  // 3. Select template (SystemHealth)
  await page.click('[data-testid="template-system-health"]');

  // 4. Configure parameters
  await page.fill('[name="dateRange"]', 'last-7-days');

  // 5. Schedule daily at 9am
  await page.selectOption('[name="schedule"]', 'daily');
  await page.fill('[name="scheduleTime"]', '09:00');

  // 6. Set email recipients
  await page.fill('[name="emails"]', 'admin@meepleai.dev');

  // 7. Create report
  await page.click('[data-testid="create-scheduled-report"]');

  // 8. Verify report in history
  await expect(page.locator('[data-testid="report-list"]')).toContainText('System Health Report');

  // 9. Trigger immediate execution
  await page.click('[data-testid="run-now"]');

  // 10. Verify execution status
  await expect(page.locator('[data-testid="execution-status"]')).toContainText('Completed');
});
```

**Email Delivery Test**:
- Use test email service (e.g., Mailhog OR Ethereal)
- Verify email received with correct subject, body, attachment
- Check attachment is valid PDF/CSV/JSON

**Effort**: 3h (E2E) + 2h (email validation) = 5h total

**Assignee**: QA

**Labels**: `admin-console`, `fase-4-advanced`, `testing`, `e2e`, `email`

**Depends On**: #47, #48

---

## Summary

**Total Issues**: 49 (1 setup + 16 FASE 1 + 13 FASE 2 + 12 FASE 3 + 8 FASE 4 - 1 duplicate)

**Labels Created**:
- `admin-console` (all issues)
- `fase-1-dashboard`, `fase-2-infrastructure`, `fase-3-management`, `fase-4-advanced`
- `backend`, `frontend`, `testing`
- `mvp` (FASE 1-2)
- `epic` (4 epic issues)
- `component`, `reusable`, `performance`, `security`, etc.

**Milestones**:
- Milestone 1: FASE 1 Complete (Issues #1-16)
- Milestone 2: FASE 2 Complete (Issues #17-29) - MVP Checkpoint
- Milestone 3: FASE 3 Complete (Issues #30-41)
- Milestone 4: FASE 4 Complete (Issues #42-49) - Full Admin Console

**Total Effort**: 280h (as per original plan)
- FASE 1: 80h (15 issues)
- FASE 2: 80h (13 issues)
- FASE 3: 80h (12 issues)
- FASE 4: 40h (8 issues)
