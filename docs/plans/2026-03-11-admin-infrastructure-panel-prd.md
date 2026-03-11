# Admin Infrastructure Panel — PRD

> **Epic:** Admin Infrastructure Management — Database, Services, Logs & Operations Console

**Goal:** Provide administrators with a unified control panel to manage domain data operations, monitor service health with Grafana integration, view aggregated logs, and control service lifecycle (restart/stop) — all from the admin dashboard.

**Architecture:** Frontend-first for Phase 1 (130+ admin backend endpoints already exist). Phase 2 adds Docker socket proxy for container control. Phase 3 adds log aggregation. Grafana iframe embedding leverages existing 14 pre-provisioned dashboards.

**Tech Stack:** .NET 9 (MediatR, EF Core) | Next.js 16 (React 19, Tailwind 4, shadcn/ui) | Docker Socket Proxy (Tecnativa) | Grafana 11.4 (embed) | Serilog + HyperDX

**Branch Strategy:** `main-dev` → `feature/issue-XXXX-*` per task

---

## Current State Analysis

### Backend: ~130 Admin Endpoints (35 files)

| Domain | Endpoints | Frontend UI |
|--------|-----------|-------------|
| Users (CRUD, bulk, suspend, lockout, impersonate) | 23 | Partial |
| Games/PDF (wizard, import, PDF mgmt, reindex) | 14 | Partial |
| KB/Qdrant (collections, search, rebuild) | 8 | Partial |
| Agents (definitions, metrics, debug, test) | 14 | Present |
| Jobs/Queue (enqueue, cancel, retry, reorder) | 6 | **Missing** |
| Resources (cache, DB, vectors metrics + actions) | 6 | **Missing** |
| Email (queue, dead-letter, retry, templates) | 13 | Partial |
| OpenRouter (status, quota, costs) | 5 | Present |
| Emergency Controls (activate/deactivate) | 3 | **Missing** |
| Audit Log (query + export) | 2 | **Missing** |
| Service Health + Restart | 3 | Basic only |

### Grafana: Ready for Embedding

- 14 dashboards pre-provisioned in `infra/dashboards/`
- `GF_SECURITY_ALLOW_EMBEDDING: "true"` already configured
- `GF_AUTH_ANONYMOUS_ENABLED: "true"` with Viewer role
- Port: `localhost:3001`
- Dashboards: API Performance, RAG Operations, LLM Costs, Infrastructure, Cache, Error Monitoring, AI Quality, HTTP Retry, Ingestion Services, Notification Metrics, Quality Gauges, RAG Evaluation, 2FA Security, Infrastructure Monitoring

### Docker Control: Gap

- Only cAdvisor has docker.sock (read-only)
- API container has NO docker socket access
- No docker-socket-proxy service exists
- `POST /admin/operations/restart-service` exists but only does graceful .NET shutdown

### Logs: Gap

- Serilog → Console + HyperDX (OTLP)
- No Loki, no file logging, no API to expose logs to frontend
- HyperDX has all logs but no embed/API integration from admin dashboard

---

## Architecture

```
/admin/monitor (System Section — existing route)
├── /operations    → Domain Operations Console (NEW)
│   ├── Resources tab (cache, DB, vectors — metrics + actions)
│   ├── Jobs tab (queue manager with CRUD)
│   ├── Emergency tab (override controls)
│   └── Audit tab (searchable audit trail + export)
│
├── /services      → Service Dashboard & Control (NEW)
│   ├── Enhanced health grid (sparklines via Grafana)
│   ├── Service restart controls (Docker proxy)
│   ├── Grafana dashboard embed (iframe selector)
│   └── DB stats overview
│
└── /logs          → Unified Log Viewer (NEW)
    ├── Application logs (HyperDX embed)
    ├── Container logs (Docker proxy)
    └── Audit trail (existing endpoint)
```

### Navigation Changes

Update `admin-dashboard-navigation.ts` System section sidebar:
```typescript
// Current System sidebar items include: Alerts, Cache, Infrastructure, etc.
// Add new items:
{ label: 'Operations Console', href: '/admin/monitor/operations', icon: Terminal }
{ label: 'Service Control', href: '/admin/monitor/services', icon: Server }
{ label: 'Log Viewer', href: '/admin/monitor/logs', icon: ScrollText }
{ label: 'Grafana', href: '/admin/monitor/grafana', icon: BarChart3 }
```

---

## Phase 1: Domain Operations Console (Frontend Only)

**Route:** `/admin/monitor/operations`
**Backend:** All endpoints already exist
**Effort:** 8-10 issues

### 1A. Resources Dashboard Tab

Connect to existing endpoints:
- `GET /admin/resources/cache/metrics` → Cache stats card (hit rate, size, evictions)
- `GET /admin/resources/database/metrics` → DB stats card (connections, query time, size)
- `GET /admin/resources/database/tables/top` → Top tables table (name, rows, size)
- `POST /admin/resources/database/vacuum` → Vacuum button with Level 2 confirmation
- `GET /admin/resources/vectors/metrics` → Vector stats card (collections, points, size)
- `POST /admin/resources/vectors/rebuild` → Rebuild button with Level 2 confirmation

**Components:**
- `ResourceStatsCard` — reusable KPI card (value, trend, icon, status badge)
- `TopTablesTable` — sortable table (name, row count, size, last vacuum)
- `ActionButton` — button with confirmation dialog for destructive ops

### 1B. Job Queue Manager Tab

Connect to existing endpoints:
- `GET /admin/jobs/` → Job list with columns: id, type, status, created, priority
- `POST /admin/jobs/enqueue` → "New Job" dialog with type selector
- `DELETE /admin/jobs/{id}` → Delete with confirmation
- `POST /admin/jobs/{id}/cancel` → Cancel button (inline)
- `POST /admin/jobs/{id}/retry` → Retry button (inline)
- `PUT /admin/jobs/reorder` → Drag-to-reorder (dnd-kit or manual priority input)

**Components:**
- `JobQueueTable` — table with status badges, action buttons per row
- `EnqueueJobDialog` — form dialog for creating new jobs
- `JobStatusBadge` — color-coded status (Pending/Running/Complete/Failed/Cancelled)

### 1C. Emergency Controls Tab

Connect to existing endpoints:
- `GET /admin/emergency/active` → Active overrides list
- `POST /admin/emergency/activate` → Activate dialog (Level 2 confirmation, reason required)
- `POST /admin/emergency/deactivate` → Deactivate button with confirmation

**Components:**
- `EmergencyOverrideCard` — red-bordered card showing active overrides
- `ActivateOverrideDialog` — form with reason, scope, duration fields
- `EmergencyBanner` — top banner when any override is active

### 1D. Audit Trail Viewer Tab

Connect to existing endpoints:
- `GET /admin/audit-log` → Searchable table with filters
- `GET /admin/audit-log/export` → Export button (CSV/JSON download)

**Components:**
- `AuditLogTable` — table: timestamp, user, action, entity, details (expandable)
- `AuditLogFilters` — user search, action type multi-select, date range picker
- `AuditLogExport` — export button with format selector

### 1E. Admin API Client Extensions

Add methods to `apps/web/src/lib/api/index.ts`:
```typescript
// Resources
getResourceCacheMetrics(): Promise<CacheMetrics>
getResourceDatabaseMetrics(): Promise<DatabaseMetrics>
getResourceDatabaseTopTables(): Promise<TopTable[]>
vacuumDatabase(): Promise<void>
getResourceVectorMetrics(): Promise<VectorMetrics>
rebuildVectors(): Promise<void>

// Jobs
getJobQueue(): Promise<Job[]>
enqueueJob(params: EnqueueJobParams): Promise<Job>
deleteJob(id: string): Promise<void>
cancelJob(id: string): Promise<void>
retryJob(id: string): Promise<void>
reorderJobs(ids: string[]): Promise<void>

// Emergency
getActiveEmergencyOverrides(): Promise<EmergencyOverride[]>
activateEmergencyOverride(params: ActivateParams): Promise<void>
deactivateEmergencyOverride(id: string): Promise<void>

// Audit
getAuditLog(filters: AuditFilters): Promise<PaginatedResult<AuditEntry>>
exportAuditLog(filters: AuditFilters, format: 'csv' | 'json'): Promise<Blob>
```

---

## Phase 2: Service Dashboard & Control

**Route:** `/admin/monitor/services`
**Effort:** 6-8 issues

### 2A. Enhanced Service Health Grid

Evolve existing `ServiceHealthMatrix` component:
- Add auto-refresh (30s interval, configurable)
- Add status history sparkline per service (via Prometheus/Grafana embed or polling)
- Add uptime % badge (last 24h)
- Add response time trend indicator (up/down/stable arrow)
- Add "last incident" timestamp

### 2B. Service Restart Controls

- UI for `POST /admin/operations/restart-service` (already exists)
- Confirmation dialog Level 2 (type service name to confirm)
- Post-restart: poll health endpoint every 5s until healthy or timeout 60s
- Cooldown enforcement: disable button for 5min after restart
- "Restart All" button: sequential restart (infra → AI → app) with progress

### 2C. Grafana Dashboard Integration

- Dashboard selector dropdown (14 dashboards from `infra/dashboards/`)
- Iframe embed with kiosk mode: `http://localhost:3001/d/{uid}/{slug}?orgId=1&kiosk`
- Responsive iframe height (600px desktop, 400px mobile)
- Loading skeleton while iframe loads
- Fallback message if Grafana is unreachable

### 2D. DB Stats Overview

Connect to existing endpoints:
- `GET /admin/resources/database/metrics` → Connection pool gauge, avg query time
- `GET /admin/resources/database/tables/top` → Top 10 tables by size
- `POST /admin/resources/database/vacuum` → Maintenance action button

---

## Phase 3: Unified Log Viewer

**Route:** `/admin/monitor/logs`
**Effort:** 6-8 issues

### 3A. Application Logs (HyperDX Embed)

- Iframe embed of HyperDX search UI (if available)
- Fallback: use `GET /admin/audit-log` with enhanced filtering
- Quick filters: Error only, Last hour, Current user

### 3B. Container Logs (Requires Docker Socket Proxy)

**New infrastructure:**
```yaml
# infra/docker-compose.yml — new service
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
```

**New backend endpoints:**
```
GET  /admin/containers                        → List containers (name, status, uptime)
GET  /admin/containers/{name}/logs?tail=200   → Container logs (last N lines)
POST /admin/containers/{name}/restart         → Restart container
POST /admin/containers/{name}/stop            → Stop container
POST /admin/containers/{name}/start           → Start container
```

**New backend service:** `IDockerProxyService` in Administration bounded context
- Communicates with docker-socket-proxy via HTTP (port 2375)
- Maps container names to user-friendly service names
- Enforces cooldown, audit logging, SuperAdmin auth

### 3C. Log Viewer UI

**Components:**
- `LogViewerPage` — 3 tabs: Application | Container | Audit
- `LogTable` — virtual-scrolled table (timestamp, level badge, service, message)
- `LogDetailPanel` — slide-out panel with full log entry (stack trace, context, correlation ID)
- `LogFilters` — level multi-select, service dropdown, date range, text search
- `LiveModeToggle` — SSE streaming with new entry counter and auto-scroll
- `LogExport` — download filtered range as JSON/CSV

---

## Phase 4: Docker Container Management (Advanced)

**Effort:** 4-5 issues
**Depends on:** Phase 3B (docker-socket-proxy)

### 4A. Container List & Status

- Grid of containers with: name, image, status, uptime, CPU/memory usage
- Color-coded status (running=green, stopped=red, restarting=yellow)
- Auto-refresh every 30s

### 4B. Container Actions

- Start/Stop/Restart per container (SuperAdmin, Level 2 confirmation)
- "Restart All" with dependency-ordered sequence:
  1. Infrastructure (postgres, redis, qdrant)
  2. AI Services (embedding, reranker, smoldocling, unstructured)
  3. Application (api, web)
- Cooldown 5min per container

### 4C. Container Logs Stream

- Real-time log streaming per container via SSE
- Tail last 200 lines on page load
- Auto-scroll with pause on manual scroll
- Filter by log level (if structured)

---

## Non-Functional Requirements

### Security
- **Auth**: Admin role for browse/read, SuperAdmin for restart/delete/vacuum
- **Audit**: Every operation logged (userId, timestamp, entity, action, before/after)
- **Column masking**: PasswordHash, RefreshToken, ApiKeys never exposed
- **Confirmation**: Level 2 for destructive ops (type confirmation text)
- **Rate limit**: 30 operations/minute per admin session
- **Docker proxy**: Minimal permissions (CONTAINERS + POST + LOGS only)

### Performance
- **Query timeout**: 5s for log/audit queries
- **Pagination**: 50 items default, max 200
- **Grafana iframe**: Lazy load with skeleton placeholder
- **Auto-refresh**: 30s for health, 60s for metrics, configurable
- **Virtual scroll**: For log tables with 1000+ entries

### Reliability
- **Restart cooldown**: 5min per service
- **Graceful restart**: Drain connections → health check → restart
- **Fallback**: If Grafana/HyperDX unreachable, show inline message (not error page)
- **Log retention**: 30 days for audit trail, container logs limited by Docker config

---

## Testing Strategy

| Layer | What | Tool | Phase |
|-------|------|------|-------|
| Unit | API client methods, filter logic, badge mapping | Vitest | All |
| Unit | DockerProxyService, LogAggregatorService | xUnit | 3-4 |
| Integration | Docker proxy communication | Testcontainers | 3-4 |
| E2E | Navigate operations console, filter audit log | Playwright | 1 |
| E2E | Grafana embed loads, service grid refreshes | Playwright | 2 |
| E2E | Log viewer filters, live mode toggle | Playwright | 3 |

**Target**: 85%+ coverage per component, all happy paths + error states

---

## Issue Breakdown Summary

| Phase | Issues | Type | Dependencies |
|-------|--------|------|-------------|
| Phase 1: Domain Operations Console | 8-10 | Frontend only | None |
| Phase 2: Service Dashboard & Control | 6-8 | Frontend + Grafana | Grafana running |
| Phase 3: Unified Log Viewer | 6-8 | Full stack + Infra | Docker socket proxy |
| Phase 4: Container Management | 4-5 | Full stack | Phase 3 |
| **Total** | **24-31** | | |

---

## Decision Log

| Decision | Choice | Rationale |
|----------|--------|-----------|
| DB Access | Domain operations, NOT generic SQL | DDD compliance, security, audit trail |
| Grafana | Embed (iframe), not duplicate | 14 dashboards exist, embedding enabled |
| Docker Control | Socket proxy (Tecnativa) | Minimal overhead, granular permissions |
| App Logs | HyperDX embed for MVP | Logs already sent via OTLP, zero backend work |
| Container Logs | Docker proxy API → SSE stream | Real-time, works with any container |
| Health History | Prometheus (existing) via Grafana | Data already collected, no duplication |
| Log Retention | 30 days | User requirement |

---

**Last Updated:** 2026-03-11
