# Admin Infrastructure Panel — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a unified admin infrastructure management panel with 4 modules: Domain Operations Console, Service Dashboard, Log Viewer, and Container Management.

**Architecture:** Frontend-first — 130+ admin backend endpoints already exist. Phase 1 is pure frontend (API client + UI). Later phases add Docker socket proxy infrastructure.

**Tech Stack:** Next.js 16 (React 19, Tailwind 4, shadcn/ui, Zustand) | .NET 9 (MediatR, EF Core) | Vitest + Playwright | Docker Socket Proxy (Tecnativa)

**Epic:** #124 | **Branch Strategy:** `main-dev` → `feature/issue-XXXX-*` per task

---

## Dependency Graph

```
Phase 1: Domain Operations Console (FRONTEND ONLY)
  #125 API Client ──────┐
  #126 Page + Nav setup ─┤──→ #127 Resources Tab
                         ├──→ #128 Queue Manager Tab
                         ├──→ #129 Emergency Tab
                         └──→ #130 Audit Tab
                                    │
                                    ↓
                              #131 Tests

Phase 2: Service Dashboard (FRONTEND + GRAFANA)
  #132 Enhanced HealthMatrix ──→ #133 Restart UI
  #134 Grafana Embed ──────────→ #135 DB Stats
                                    │
                                    ↓
                              #136 Tests

Phase 3: Log Viewer + Docker Infra (FULL STACK)
  #137 Docker Socket Proxy ──→ #138 IDockerProxyService
                                    │
                                    ↓
                              #139 Container API Endpoints
                                    │
  #140 Log Viewer Page ─────────────┤
  #141 Container Logs UI ───────────┘
                                    │
                                    ↓
                              #142 Tests

Phase 4: Container Dashboard (FRONTEND)
  #143 Container Grid ──→ #145 Restart All
                              │
                              ↓
                         #144 Tests
```

---

## What Exists (DO NOT rebuild)

| Component | Path | What it does |
|---|---|---|
| Monitor hub page | `apps/web/src/app/admin/(dashboard)/monitor/page.tsx` | 7-tab hub with `AdminHubTabBar`, `?tab=` URL persistence |
| NavConfig | `apps/web/src/app/admin/(dashboard)/monitor/NavConfig.tsx` | Registers action bar buttons (Refresh, Clear Cache) |
| Admin navigation | `apps/web/src/config/admin-dashboard-navigation.ts` | System section with 14 sidebar items |
| ServiceHealthMatrix | `apps/web/src/components/admin/ServiceHealthMatrix.tsx` | Glassmorphic service cards grid |
| InfrastructureTab | `apps/web/src/app/admin/(dashboard)/monitor/InfrastructureTab.tsx` | Fetches health, renders ServiceHealthMatrix |
| CacheTab | `apps/web/src/app/admin/(dashboard)/monitor/CacheTab.tsx` | 4 MetricCard components from Prometheus |
| Admin API client | `apps/web/src/lib/api/clients/adminClient.ts` | 3000+ lines, `createAdminClient()` factory pattern |
| Admin schemas | `apps/web/src/lib/api/schemas/admin.schemas.ts` | Zod schemas for all admin DTOs |
| `GET /admin/infrastructure/details` | `MonitoringEndpoints.cs` | Health + Prometheus metrics |
| `GET /admin/resources/*` | `AdminResourcesEndpoints.cs` | Cache, DB, vector metrics + vacuum |
| `GET/POST /admin/queue/*` | `AdminQueueEndpoints.cs` | PDF processing queue (18 endpoints!) |
| `GET/POST /admin/llm/emergency/*` | `AdminEmergencyControlsEndpoints.cs` | LLM emergency overrides |
| `GET /admin/audit-log/*` | `AdminAuditLogEndpoints.cs` | Paginated audit log + CSV export |

### Backend DTO Reference (exact response shapes)

**DatabaseMetricsDto:**
```typescript
{ sizeBytes, sizeFormatted, growthLast7Days, growthLast30Days, growthLast90Days,
  activeConnections, maxConnections, transactionsCommitted, transactionsRolledBack, measuredAt }
```

**CacheMetricsDto:**
```typescript
{ usedMemoryBytes, usedMemoryFormatted, maxMemoryBytes, maxMemoryFormatted,
  memoryUsagePercent, totalKeys, keyspaceHits, keyspaceMisses, hitRate,
  evictedKeys, expiredKeys, measuredAt }
```

**VectorStoreMetricsDto:**
```typescript
{ totalCollections, totalVectors, indexedVectors, memoryBytes, memoryFormatted,
  collections: [{ collectionName, vectorCount, indexedCount, vectorDimensions,
                   distanceMetric, memoryBytes, memoryFormatted }], measuredAt }
```

**TableSizeDto:**
```typescript
{ tableName, sizeBytes, sizeFormatted, rowCount, indexSizeBytes,
  indexSizeFormatted, totalSizeBytes, totalSizeFormatted }
```

**ProcessingJobDto (Queue):**
```typescript
{ id, pdfDocumentId, pdfFileName, userId, status, priority, currentStep,
  createdAt, startedAt, completedAt, errorMessage, retryCount, maxRetries, canRetry }
```

**QueueStatusDto:**
```typescript
{ queueDepth, backpressureThreshold, isUnderPressure, isPaused,
  maxConcurrentWorkers, estimatedWaitMinutes }
```

**ActiveOverrideInfo (Emergency):**
```typescript
{ action, reason, adminUserId, targetProvider, activatedAt, expiresAt, remainingMinutes }
// Actions: 'force-ollama-only' | 'reset-circuit-breaker' | 'flush-quota-cache'
```

**AuditLogDto:**
```typescript
{ id, adminUserId, action, resource, resourceId, result, details,
  ipAddress, createdAt, userName, userEmail }
```

### Key Patterns to Follow

**Tab component pattern** (from InfrastructureTab):
```tsx
export function MyTab() {
  const [data, setData] = useState<MyType | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.admin.myMethod()
      .then(result => setData(result))
      .catch(() => setData(null))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold">Title</h2>
        <p className="mt-1 text-sm text-muted-foreground">Description</p>
      </div>
      {/* Content */}
    </div>
  );
}
```

**Test pattern** (from InfrastructureTab tests):
```tsx
const mockMethod = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({ api: { admin: { myMethod: mockMethod } } }));
```

**API client pattern** (from adminClient.ts):
```typescript
async getResourceDatabaseMetrics() {
  return httpClient.get('/api/v1/admin/resources/database/metrics', DatabaseMetricsSchema);
}
```

**Destructive action pattern** — backend uses `?confirmed=true` query param:
```typescript
async vacuumDatabase(fullVacuum = false) {
  return httpClient.post(`/api/v1/admin/resources/database/vacuum?confirmed=true&fullVacuum=${fullVacuum}`);
}
```

---

## Phase 1: Domain Operations Console

### Architecture Decision: New Pages vs New Tabs

The current monitor page has 7 tabs. Adding 4 more would make 11 tabs — too many.

**Decision:** Create **new pages** under `/admin/monitor/` for each module, and add them as **sidebar items** in the System section navigation. This keeps the existing monitor hub untouched.

### Step 1: #125 — Admin API Client Extensions
**Branch:** `feature/issue-125-admin-api-client`
**Files to modify:** `apps/web/src/lib/api/clients/adminClient.ts`, `apps/web/src/lib/api/schemas/admin.schemas.ts`

- [ ] **1.1** Add Zod schemas in `admin.schemas.ts`:
  ```typescript
  // Resources
  export const DatabaseMetricsSchema = z.object({
    sizeBytes: z.number(), sizeFormatted: z.string(),
    growthLast7Days: z.number(), growthLast30Days: z.number(), growthLast90Days: z.number(),
    activeConnections: z.number(), maxConnections: z.number(),
    transactionsCommitted: z.number(), transactionsRolledBack: z.number(),
    measuredAt: z.string().datetime(),
  });
  export type DatabaseMetrics = z.infer<typeof DatabaseMetricsSchema>;

  export const CacheMetricsSchema = z.object({
    usedMemoryBytes: z.number(), usedMemoryFormatted: z.string(),
    maxMemoryBytes: z.number(), maxMemoryFormatted: z.string(),
    memoryUsagePercent: z.number(), totalKeys: z.number(),
    keyspaceHits: z.number(), keyspaceMisses: z.number(), hitRate: z.number(),
    evictedKeys: z.number(), expiredKeys: z.number(), measuredAt: z.string().datetime(),
  });

  export const TableSizeSchema = z.object({
    tableName: z.string(), sizeBytes: z.number(), sizeFormatted: z.string(),
    rowCount: z.number(), indexSizeBytes: z.number(), indexSizeFormatted: z.string(),
    totalSizeBytes: z.number(), totalSizeFormatted: z.string(),
  });

  export const VectorStoreMetricsSchema = z.object({
    totalCollections: z.number(), totalVectors: z.number(), indexedVectors: z.number(),
    memoryBytes: z.number(), memoryFormatted: z.string(),
    collections: z.array(z.object({
      collectionName: z.string(), vectorCount: z.number(), indexedCount: z.number(),
      vectorDimensions: z.number(), distanceMetric: z.string(),
      memoryBytes: z.number(), memoryFormatted: z.string(),
    })),
    measuredAt: z.string().datetime(),
  });

  // Queue
  export const ProcessingJobSchema = z.object({
    id: z.string().uuid(), pdfDocumentId: z.string().uuid(), pdfFileName: z.string(),
    userId: z.string().uuid(), status: z.string(), priority: z.number(),
    currentStep: z.string().nullable(), createdAt: z.string().datetime(),
    startedAt: z.string().datetime().nullable(), completedAt: z.string().datetime().nullable(),
    errorMessage: z.string().nullable(), retryCount: z.number(), maxRetries: z.number(),
    canRetry: z.boolean(),
  });

  export const PaginatedQueueSchema = z.object({
    jobs: z.array(ProcessingJobSchema), total: z.number(),
    page: z.number(), pageSize: z.number(), totalPages: z.number(),
  });

  export const QueueStatusSchema = z.object({
    queueDepth: z.number(), backpressureThreshold: z.number(),
    isUnderPressure: z.boolean(), isPaused: z.boolean(),
    maxConcurrentWorkers: z.number(), estimatedWaitMinutes: z.number(),
  });

  // Emergency
  export const ActiveOverrideSchema = z.object({
    action: z.string(), reason: z.string(), adminUserId: z.string().uuid(),
    targetProvider: z.string().nullable(), activatedAt: z.string().datetime(),
    expiresAt: z.string().datetime(), remainingMinutes: z.number(),
  });

  // Audit
  export const AuditLogEntrySchema = z.object({
    id: z.string().uuid(), adminUserId: z.string().uuid().nullable(),
    action: z.string(), resource: z.string(), resourceId: z.string().nullable(),
    result: z.string(), details: z.string().nullable(), ipAddress: z.string().nullable(),
    createdAt: z.string().datetime(), userName: z.string().nullable(), userEmail: z.string().nullable(),
  });

  export const AuditLogListSchema = z.object({
    entries: z.array(AuditLogEntrySchema), totalCount: z.number(),
    limit: z.number(), offset: z.number(),
  });
  ```

- [ ] **1.2** Add methods to `adminClient.ts` (inside `createAdminClient` return object):
  ```typescript
  // === Resources ===
  async getResourceCacheMetrics() {
    return httpClient.get('/api/v1/admin/resources/cache/metrics', CacheMetricsSchema);
  },
  async getResourceDatabaseMetrics() {
    return httpClient.get('/api/v1/admin/resources/database/metrics', DatabaseMetricsSchema);
  },
  async getResourceDatabaseTopTables(limit = 10) {
    return httpClient.get(`/api/v1/admin/resources/database/tables/top?limit=${limit}`, z.array(TableSizeSchema));
  },
  async vacuumDatabase(fullVacuum = false) {
    return httpClient.post(`/api/v1/admin/resources/database/vacuum?confirmed=true&fullVacuum=${fullVacuum}`);
  },
  async clearCache() {
    return httpClient.post('/api/v1/admin/resources/cache/clear?confirmed=true');
  },
  async getResourceVectorMetrics() {
    return httpClient.get('/api/v1/admin/resources/vectors/metrics', VectorStoreMetricsSchema);
  },

  // === Queue ===
  async getProcessingQueue(params?: { status?: string; search?: string; page?: number; pageSize?: number }) {
    const qs = new URLSearchParams();
    if (params?.status) qs.set('status', params.status);
    if (params?.search) qs.set('search', params.search);
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    return httpClient.get(`/api/v1/admin/queue/?${qs}`, PaginatedQueueSchema);
  },
  async getQueueStatus() {
    return httpClient.get('/api/v1/admin/queue/status', QueueStatusSchema);
  },
  async cancelJob(jobId: string) {
    return httpClient.post(`/api/v1/admin/queue/${jobId}/cancel`);
  },
  async retryJob(jobId: string) {
    return httpClient.post(`/api/v1/admin/queue/${jobId}/retry`);
  },
  async removeJob(jobId: string) {
    return httpClient.delete(`/api/v1/admin/queue/${jobId}`);
  },
  async reorderQueue(orderedJobIds: string[]) {
    return httpClient.put('/api/v1/admin/queue/reorder', { orderedJobIds });
  },

  // === Emergency ===
  async getActiveEmergencyOverrides() {
    return httpClient.get('/api/v1/admin/llm/emergency/active', z.array(ActiveOverrideSchema));
  },
  async activateEmergencyOverride(params: { action: string; reason: string; durationMinutes?: number; targetProvider?: string }) {
    return httpClient.post('/api/v1/admin/llm/emergency/activate', params);
  },
  async deactivateEmergencyOverride(action: string) {
    return httpClient.post('/api/v1/admin/llm/emergency/deactivate', { action });
  },

  // === Audit ===
  async getAuditLog(params?: { limit?: number; offset?: number; adminUserId?: string; action?: string; resource?: string; result?: string; startDate?: string; endDate?: string }) {
    const qs = new URLSearchParams();
    Object.entries(params ?? {}).forEach(([k, v]) => { if (v != null) qs.set(k, String(v)); });
    return httpClient.get(`/api/v1/admin/audit-log/?${qs}`, AuditLogListSchema);
  },
  async exportAuditLog(params?: { action?: string; resource?: string; startDate?: string; endDate?: string }) {
    const qs = new URLSearchParams();
    Object.entries(params ?? {}).forEach(([k, v]) => { if (v != null) qs.set(k, String(v)); });
    return httpClient.getBlob(`/api/v1/admin/audit-log/export?${qs}`);
  },
  ```

- [ ] **1.3** Write unit tests for all new methods (mock httpClient, verify URL construction, Zod parsing)

---

### Step 2: #126 — Operations Console Page + Navigation
**Branch:** `feature/issue-126-operations-page`
**Depends on:** None (can parallel with #125)

- [ ] **2.1** Update `admin-dashboard-navigation.ts` — add 4 new sidebar items to System section:
  ```typescript
  // After existing monitor items, before config items:
  { href: '/admin/monitor/operations', label: 'Operations', icon: TerminalIcon },
  { href: '/admin/monitor/services', label: 'Service Control', icon: ServerIcon },
  { href: '/admin/monitor/logs', label: 'Log Viewer', icon: ScrollTextIcon },
  { href: '/admin/monitor/grafana', label: 'Grafana', icon: BarChart3Icon },
  ```

- [ ] **2.2** Create page `apps/web/src/app/admin/(dashboard)/monitor/operations/page.tsx`:
  - Use `AdminHubTabBar` pattern (same as monitor page)
  - 4 tabs: Resources | Queue | Emergency | Audit
  - `?tab=` URL param persistence
  - Each tab lazy-loaded with Suspense + skeleton

- [ ] **2.3** Create `apps/web/src/app/admin/(dashboard)/monitor/operations/NavConfig.tsx`:
  - Register MiniNav/action bar for operations page

- [ ] **2.4** Verify navigation: sidebar links work, active state highlights correctly

---

### Step 3: #127 — Resources Dashboard Tab
**Branch:** `feature/issue-127-resources-tab`
**Depends on:** #125 (API client), #126 (page setup)

- [ ] **3.1** Create `ResourcesTab.tsx` in operations directory:
  - Fetch 3 endpoints in parallel: `getResourceCacheMetrics`, `getResourceDatabaseMetrics`, `getResourceVectorMetrics`
  - Also fetch `getResourceDatabaseTopTables`

- [ ] **3.2** Create `ResourceStatsCard.tsx` component:
  - Props: `title`, `value`, `subtitle`, `icon`, `trend?`, `status?`
  - Glassmorphic card matching existing design tokens
  - Loading skeleton state

- [ ] **3.3** Layout:
  ```
  ┌─────────────┬─────────────┬─────────────┐
  │ Cache        │ Database    │ Vectors      │
  │ Hit: 94.2%   │ Conn: 12/50 │ Coll: 4      │
  │ Keys: 1,234  │ Size: 2.1GB │ Points: 12K  │
  │ Mem: 256MB   │ Txn: 45K    │ Mem: 890MB   │
  └─────────────┴─────────────┴─────────────┘
  ┌─────────────────────────────────────────────┐
  │ Top Tables by Size                           │
  │ Name        │ Rows    │ Size   │ Index Size │
  │ shared_games│ 12,450  │ 45MB   │ 12MB       │
  │ users       │ 3,200   │ 22MB   │ 8MB        │
  └─────────────────────────────────────────────┘
  [Clear Cache]  [Vacuum DB]  ← AlertDialog confirm
  ```

- [ ] **3.4** Action buttons:
  - `Clear Cache` — AlertDialog: "Are you sure? This will clear all cached data." → `clearCache()`
  - `Vacuum Database` — AlertDialog: "VACUUM will reclaim storage. Run FULL vacuum?" with checkbox → `vacuumDatabase(full)`
  - Note: vector rebuild is DISABLED on backend (returns 400) — show disabled button with tooltip

- [ ] **3.5** Auto-refresh: 60s interval with `setInterval` in useEffect, cleanup on unmount

---

### Step 4: #128 — Queue Manager Tab
**Branch:** `feature/issue-128-queue-tab`
**Depends on:** #125, #126

- [ ] **4.1** Create `QueueTab.tsx`:
  - Fetch `getQueueStatus()` + `getProcessingQueue()` in parallel
  - Show queue status banner at top (depth, pressure, paused state)

- [ ] **4.2** Create `QueueStatusBanner.tsx`:
  - Green: "Queue healthy" (depth < threshold, not paused)
  - Yellow: "Under pressure" (isUnderPressure)
  - Red: "Queue paused" (isPaused)
  - Stats: depth, workers, estimated wait

- [ ] **4.3** Create `ProcessingJobTable.tsx`:
  - Columns: File Name, Status (badge), Priority, Created, Duration, Actions
  - Status badges: Pending (yellow), Processing (blue+spin), Completed (green), Failed (red), Cancelled (gray)
  - Actions per row: Cancel (if pending/processing), Retry (if failed+canRetry), Remove (if completed/cancelled)
  - Each action: inline AlertDialog confirmation

- [ ] **4.4** Pagination: page selector + page size selector (20, 50, 100)
- [ ] **4.5** Filters: status dropdown, search input (file name)
- [ ] **4.6** Auto-refresh: 15s (queue changes fast)

---

### Step 5: #129 — Emergency Controls Tab
**Branch:** `feature/issue-129-emergency-tab`
**Depends on:** #125, #126

- [ ] **5.1** Create `EmergencyTab.tsx`:
  - Fetch `getActiveEmergencyOverrides()`
  - Show active overrides as cards or empty "All systems normal" state

- [ ] **5.2** Create `EmergencyOverrideCard.tsx`:
  - Red-bordered card (`border-red-500`)
  - Shows: action name, reason, activated by, remaining time (countdown)
  - "Deactivate" button with confirmation

- [ ] **5.3** Create `ActivateOverrideDialog.tsx`:
  - Action selector: "Force Ollama Only" | "Reset Circuit Breaker" | "Flush Quota Cache"
  - Reason input (required)
  - Duration slider (5-1440 min, default 30)
  - Target provider input (optional, for reset-circuit-breaker)
  - Level 2 confirmation: type action name to confirm

- [ ] **5.4** Empty state: green `CheckCircle` + "No active emergency overrides. All LLM systems operating normally."

---

### Step 6: #130 — Audit Trail Viewer Tab
**Branch:** `feature/issue-130-audit-tab`
**Depends on:** #125, #126

- [ ] **6.1** Create `AuditTab.tsx`:
  - Fetch `getAuditLog({ limit: 50 })`
  - Table with expandable rows

- [ ] **6.2** Create `AuditLogTable.tsx`:
  - Columns: Timestamp, User (name+email), Action (badge), Resource, Result (Success/Failure badge), Details
  - Expandable row: full details JSON, IP address, resource ID

- [ ] **6.3** Create `AuditLogFilters.tsx`:
  - User search input
  - Action type multi-select
  - Resource type dropdown
  - Result: All | Success | Failure
  - Date range picker (from/to)
  - Apply/Reset buttons

- [ ] **6.4** Export button:
  - "Export CSV" calls `exportAuditLog()` with current filters
  - Triggers file download

- [ ] **6.5** Pagination: offset-based (prev/next buttons with total count display)

---

### Step 7: #131 — Phase 1 Tests
**Branch:** `feature/issue-131-operations-tests`
**Depends on:** #127, #128, #129, #130

- [ ] **7.1** Unit tests for each API client method (15+ tests)
- [ ] **7.2** Unit tests for ResourceStatsCard, QueueStatusBanner, ProcessingJobTable, EmergencyOverrideCard, AuditLogTable
- [ ] **7.3** Unit tests for filter components
- [ ] **7.4** Integration test: Operations page renders 4 tabs, switching works
- [ ] **7.5** E2E (Playwright): navigate to /admin/monitor/operations, verify tabs load

---

## Phase 2: Service Dashboard & Control

### Step 8: #132 — Enhanced ServiceHealthMatrix
**Branch:** `feature/issue-132-enhanced-health`

- [ ] **8.1** Create new page: `apps/web/src/app/admin/(dashboard)/monitor/services/page.tsx`
- [ ] **8.2** Enhance `ServiceHealthMatrix` or create `EnhancedServiceHealthMatrix`:
  - Auto-refresh toggle (30s/60s) with visual countdown ring
  - Group services by category: Core Infrastructure, AI Services, External APIs, Monitoring
  - Collapsible category sections
  - Per-service: status badge, response time, uptime indicator

- [ ] **8.3** Use existing `GET /admin/infrastructure/details` (already has all needed data)

---

### Step 9: #133 — Service Restart UI
**Branch:** `feature/issue-133-restart-ui`
**Depends on:** #132

- [ ] **9.1** Add restart button per service (SuperAdmin only — check session role)
- [ ] **9.2** Create `RestartServiceDialog.tsx`:
  - Warning text about downtime (~30-60s)
  - Level 2: type service name to confirm
  - Call `POST /admin/operations/restart-service` (existing endpoint)
- [ ] **9.3** Post-restart feedback: poll health every 5s, show spinner until healthy or 60s timeout
- [ ] **9.4** Cooldown: store restart timestamp in state, disable button for 5min

---

### Step 10: #134 — Grafana Dashboard Embed
**Branch:** `feature/issue-134-grafana-embed`

- [ ] **10.1** Create page: `apps/web/src/app/admin/(dashboard)/monitor/grafana/page.tsx`
- [ ] **10.2** Dashboard config map (from `infra/dashboards/` filenames → Grafana UIDs):
  ```typescript
  const GRAFANA_DASHBOARDS = [
    { uid: 'api-performance', label: 'API Performance', slug: 'api-performance' },
    { uid: 'rag-operations', label: 'RAG Operations', slug: 'ai-rag-operations' },
    // ... 14 dashboards
  ];
  ```
- [ ] **10.3** Dashboard selector dropdown
- [ ] **10.4** Iframe: `<iframe src="${GRAFANA_URL}/d/${uid}/${slug}?orgId=1&kiosk&theme=light" />`
  - Use `NEXT_PUBLIC_GRAFANA_URL` env var (default `http://localhost:3001`)
  - Responsive: `h-[600px] w-full` desktop, `h-[400px]` mobile
  - Time range selector: `&from=now-1h&to=now` (1h, 6h, 24h, 7d)
- [ ] **10.5** Fallback: onError → show "Grafana unavailable" message with retry button
- [ ] **10.6** Add `NEXT_PUBLIC_GRAFANA_URL=http://localhost:3001` to `.env.development.example`

---

### Step 11: #135 — DB Stats Overview
**Branch:** `feature/issue-135-db-stats`
**Depends on:** #125

- [ ] **11.1** Add DB Stats section to services page (or as subtab of Resources):
  - 3 KPI cards: Active Connections (gauge), Avg Query Time, DB Size
  - Top 10 tables table (reuse from ResourcesTab)
  - Vacuum button (reuse from ResourcesTab)

---

### Step 12: #136 — Phase 2 Tests
**Branch:** `feature/issue-136-service-tests`
**Depends on:** #132-#135

- [ ] **12.1** Unit: EnhancedServiceHealthMatrix grouping, auto-refresh
- [ ] **12.2** Unit: RestartServiceDialog confirmation flow, cooldown state
- [ ] **12.3** Unit: GrafanaEmbed iframe src, fallback
- [ ] **12.4** E2E: navigate to /admin/monitor/services, health grid loads
- [ ] **12.5** E2E: navigate to /admin/monitor/grafana, selector works

---

## Phase 3: Log Viewer + Docker Infra

### Step 13: #137 — Docker Socket Proxy Infrastructure
**Branch:** `feature/issue-137-docker-proxy`

- [ ] **13.1** Add to `infra/docker-compose.yml`:
  ```yaml
  docker-socket-proxy:
    image: tecnativa/docker-socket-proxy:latest
    container_name: meepleai-docker-proxy
    restart: unless-stopped
    profiles: [dev, full]
    environment:
      CONTAINERS: 1
      POST: 1
      LOGS: 1
      IMAGES: 0
      VOLUMES: 0
      NETWORKS: 0
      EXEC: 0
      SWARM: 0
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    networks:
      - meepleai-network
    healthcheck:
      test: ["CMD", "wget", "--spider", "-q", "http://localhost:2375/version"]
      interval: 30s
      timeout: 5s
      retries: 3
  ```

- [ ] **13.2** Add env to API service: `DOCKER_PROXY_URL=http://docker-socket-proxy:2375`
- [ ] **13.3** Test: `docker compose --profile dev up docker-socket-proxy` starts, API can reach it
- [ ] **13.4** Document in deployment docs

---

### Step 14: #138 — IDockerProxyService Backend
**Branch:** `feature/issue-138-docker-service`
**Depends on:** #137

- [ ] **14.1** Create interface: `BoundedContexts/Administration/Domain/Services/IDockerProxyService.cs`
- [ ] **14.2** Create implementation: `BoundedContexts/Administration/Infrastructure/External/DockerProxyService.cs`
  - HTTP client to `DOCKER_PROXY_URL`
  - Map container names: `meepleai-postgres` → "PostgreSQL", etc.
  - In-memory cooldown tracking (ConcurrentDictionary)
- [ ] **14.3** Create DTOs: `ContainerInfoDto`, `ContainerLogLineDto`
- [ ] **14.4** Register in DI: `services.AddSingleton<IDockerProxyService, DockerProxyService>()`
- [ ] **14.5** Unit tests with mocked HttpClient

---

### Step 15: #139 — Container API Endpoints
**Branch:** `feature/issue-139-container-endpoints`
**Depends on:** #138

- [ ] **15.1** Create `AdminContainerEndpoints.cs`:
  ```csharp
  GET  /admin/containers              → ListContainers
  GET  /admin/containers/{name}/logs  → GetContainerLogs(?tail=200)
  POST /admin/containers/{name}/restart → RestartContainer (SuperAdmin)
  POST /admin/containers/{name}/stop    → StopContainer (SuperAdmin)
  POST /admin/containers/{name}/start   → StartContainer (SuperAdmin)
  ```
- [ ] **15.2** CQRS: ListContainersQuery, GetContainerLogsQuery, RestartContainerCommand, etc.
- [ ] **15.3** Audit logging on all POST endpoints
- [ ] **15.4** Integration tests

---

### Step 16: #140 — Log Viewer Page
**Branch:** `feature/issue-140-log-viewer`
**Depends on:** #125

- [ ] **16.1** Create page: `apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx`
  - 3 tabs: Application Logs | Container Logs | Audit Trail
- [ ] **16.2** Application Logs tab: HyperDX iframe embed (same pattern as Grafana)
  - `NEXT_PUBLIC_HYPERDX_URL` env var
  - Fallback message if unavailable
- [ ] **16.3** Audit Trail tab: reuse `AuditLogTable` + `AuditLogFilters` from #130
- [ ] **16.4** Container Logs tab: placeholder "Requires Docker Socket Proxy" (content in #141)

---

### Step 17: #141 — Container Logs UI
**Branch:** `feature/issue-141-container-logs`
**Depends on:** #139, #140

- [ ] **17.1** Add API client methods: `getContainerList()`, `getContainerLogs(name, tail)`
- [ ] **17.2** Create `ContainerLogViewer.tsx`:
  - Container selector dropdown
  - Log display: monospace, color-coded by level
  - Tail control: 50/100/200/500
  - Live mode: poll every 3s for new lines
- [ ] **17.3** Export: download displayed logs as `.txt`

---

### Step 18: #142 — Phase 3 Tests
**Branch:** `feature/issue-142-log-tests`
**Depends on:** #138-#141

- [ ] **18.1** Backend: DockerProxyService tests, container endpoint tests
- [ ] **18.2** Frontend: LogViewer page, ContainerLogViewer, ContainerSelector
- [ ] **18.3** E2E: navigate to /admin/monitor/logs, tabs render

---

## Phase 4: Container Dashboard

### Step 19: #143 — Container Dashboard Page
**Branch:** `feature/issue-143-container-dashboard`
**Depends on:** #139

- [ ] **19.1** Create page: `apps/web/src/app/admin/(dashboard)/monitor/containers/page.tsx`
- [ ] **19.2** Container grid: cards per container (name, image, status badge, uptime)
- [ ] **19.3** Action buttons: Restart/Stop/Start + View Logs link
- [ ] **19.4** Auto-refresh 30s

---

### Step 20: #145 — Restart All
**Branch:** `feature/issue-145-restart-all`
**Depends on:** #143

- [ ] **20.1** Restart All button with dependency-ordered sequence
- [ ] **20.2** Progress tracker UI showing current phase
- [ ] **20.3** 15min global cooldown

---

### Step 21: #144 — Phase 4 Tests
**Branch:** `feature/issue-144-container-tests`
**Depends on:** #143, #145

- [ ] **21.1** Unit + E2E tests for container dashboard

---

## Parallelization Strategy

```
Week 1: Phase 1 (all frontend, max parallelism)
├── #125 API Client ──────────── Agent 1
├── #126 Page + Nav ──────────── Agent 2
│   (merge both, then parallel:)
├── #127 Resources Tab ───────── Agent 1
├── #128 Queue Tab ───────────── Agent 2
├── #129 Emergency Tab ───────── Agent 3
└── #130 Audit Tab ───────────── Agent 4
    └── #131 Tests ───────────── Agent 1 (after all merge)

Week 2: Phase 2 (frontend + Grafana)
├── #132 Enhanced Health ─────── Agent 1
├── #134 Grafana Embed ───────── Agent 2
│   (merge, then:)
├── #133 Restart UI ──────────── Agent 1
├── #135 DB Stats ────────────── Agent 2
└── #136 Tests ───────────────── Agent 1

Week 3: Phase 3 (full stack)
├── #137 Docker Proxy (infra) ── Agent 1
├── #140 Log Viewer Page ─────── Agent 2 (no proxy dependency)
│   (merge #137, then:)
├── #138 Docker Service (BE) ─── Agent 1
├── #139 Container Endpoints ─── Agent 1 (sequential)
├── #141 Container Logs UI ───── Agent 2
└── #142 Tests ───────────────── Agent 1

Week 4: Phase 4
├── #143 Container Dashboard ─── Agent 1
├── #145 Restart All ─────────── Agent 1
└── #144 Tests ───────────────── Agent 1
```

**Max parallelism:** 4 agents in Phase 1 (all independent tabs), 2 agents in other phases.

---

## PR Strategy

Each issue = 1 feature branch → 1 PR → merge to `main-dev`.

```bash
# Per issue:
git checkout main-dev && git pull
git checkout -b feature/issue-125-admin-api-client
git config branch.feature/issue-125-admin-api-client.parent main-dev
# ... work ...
# PR to main-dev, code review, merge, cleanup
```

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Queue endpoints are PDF-specific, not generic jobs | Name tab "Processing Queue" not "Job Queue" |
| Vector rebuild is disabled on backend | Show disabled button with tooltip explaining pgvector migration |
| Emergency controls are LLM-specific | Name tab "LLM Emergency" not generic "Emergency" |
| Grafana UIDs unknown until runtime | Add discovery endpoint or hardcode from dashboard JSON filenames |
| Docker socket proxy may not work on Windows dev | Document Linux/WSL requirement, add graceful fallback |
| HyperDX may not be running | Always show fallback message, never crash |

---

**Last Updated:** 2026-03-11
