# Admin Pages — Mock Data Removal & Real Data Audit

**Date**: 2026-03-25
**Status**: Reviewed (3 review iterations, 21 issues fixed)
**Scope**: Full audit of 77+ admin pages — remove mock data, implement missing endpoints, honest UI for unimplemented features

---

## Problem Statement

The admin panel has three categories of issues:

1. **Broken routes** — Frontend calls endpoints with wrong paths, causing 404 errors
2. **Mock/placeholder data** — Hardcoded values, fake timeouts, and "Coming Soon" placeholders that mislead admins
3. **Missing backend endpoints** — ~80-90 endpoints referenced by frontend but never implemented in the backend
4. **Hardcoded UI constants** — Model lists, strategy options, and rate limits baked into frontend code instead of served dynamically from the backend

## Approach: Progressive Reality (3 Phases)

### Phase 1: Fix Immediati
Fix broken routes, remove mock data from existing working pages.

### Phase 2: Implement High-Value Endpoints
Create backend endpoints for dynamic config, analytics, batch jobs, database metrics.

### Phase 3: Honest UI for Unimplemented Features
Every admin section without a backend shows a clear empty state per-section — no fake data, no "Coming Soon" without context.

---

## Phase 1: Fix Immediati

### 1.1 Route Mismatch Fixes

Two frontend API client calls use wrong URL paths, causing 404 errors at runtime.

#### Fix 1: Agent Typologies — Wrong Path (admin vs user endpoint)

**File**: `apps/web/src/lib/api/clients/admin/adminAiClient.ts`

The following methods call `/admin/agent-typologies` but the backend registers these endpoints at `/api/v1/agent-typologies` (user-facing, no `/admin` segment). The backend routing is in `AgentTypologyEndpoints.cs`, mapped at `v1Api.MapGroup("/agent-typologies")`.

Methods affected:
- `getAgentTypologies()`
- `getAgentTypologyById(id)`
- `deleteAgentTypology(id)`
- `approveAgentTypology(id)`
- `rejectAgentTypology(id)`

**Decision**: Create dedicated admin endpoints at `/api/v1/admin/agent-typologies` with `RequireAdminSession()` that provide unrestricted visibility (no user-scoped filtering). The existing user-facing endpoints at `/api/v1/agent-typologies` remain unchanged.

**Backend fix**: New `AdminAgentTypologyEndpoints.cs` routing file. The existing user-facing endpoints only have GET and Propose operations — there are no `Delete`, `Approve`, or `Reject` commands. The admin endpoints require:

- **Existing handlers (reuse)**: `GetAllAgentTypologiesQuery`, `GetAgentTypologyByIdQuery` — can be called directly but without user-scoping filter
- **Existing commands (reuse)**: `DeleteAgentTypologyCommand`, `ApproveAgentTypologyCommand`, `RejectAgentTypologyCommand` — handlers and validators already exist in `KnowledgeBase/Application/Commands/`

**Frontend fix**: Update `adminAiClient.ts` to call `/api/v1/admin/agent-typologies`.

**Verification**: Call each admin endpoint and confirm 200 response (not 404). Verify user endpoints still work independently.

#### Fix 2: Prompt Audit Log — Missing `/admin` Segment

**File**: `apps/web/src/lib/api/clients/admin/adminContentClient.ts`

Frontend calls `GET /api/v1/prompts/{id}/audit-log` but backend registers `GET /api/v1/admin/prompts/{id}/audit-log`.

**Fix**: Change frontend path to include `/admin`.

**Verification**: Call endpoint and confirm 200 response.

### 1.2 Frontend Mock Data Removal

Five files contain hardcoded data or fake behavior that must be replaced.

#### 1.2.1 BulkExportTab.tsx — Fake Export with setTimeout

**File**: `apps/web/src/app/admin/(dashboard)/monitor/BulkExportTab.tsx`
**Lines**: 44-53

**Current**: `setTimeout(600ms)` simulates an export operation, then shows toast "API endpoint not yet connected".

**Fix**:
- Disable export buttons (disabled state + tooltip "Export non disponibile")
- Show empty state below each export card: "Endpoint di export non implementato"
- Remove the fake `handleExport` function entirely

#### 1.2.2 LlmConfigTab.tsx — Hardcoded Default Values

**File**: `apps/web/src/app/admin/(dashboard)/ai/LlmConfigTab.tsx`
**Lines**: 303-308

**Current**: ReadOnlyRow components display hardcoded values:
- Default Failure Threshold: "5"
- Default Open Duration: "30s"
- Default Success Threshold: "3"
- Default Daily Budget: "$10.00"
- Default Monthly Budget: "$100.00"

**Fix**: The backend `LlmSystemConfig` entity already contains these exact fields (`CircuitBreakerFailureThreshold`, `CircuitBreakerOpenDurationSeconds`, `CircuitBreakerSuccessThreshold`, `DailyBudgetUsd`, `MonthlyBudgetUsd`) with the same default values. The `GET /api/v1/admin/llm/config` endpoint already exists.

Action: Verify that the `LlmSystemConfigDto` response includes these fields. Then update `LlmConfigTab.tsx` to read from the existing API response instead of hardcoded ReadOnlyRow values. Display loading skeleton while fetching.

#### 1.2.3 RateLimitsTab.tsx — Hardcoded Rate Limit Array

**File**: `apps/web/src/app/admin/(dashboard)/config/RateLimitsTab.tsx`
**Lines**: 13-44, 87-89

**Current**: `RATE_LIMIT_CATEGORIES` array with hardcoded values (1000 req/min global, 100/min per user, etc.) plus "Editable configuration coming in future update".

**Fix**: Create backend endpoint `GET /api/v1/admin/config/rate-limits` (see Phase 2, section 2.1). Frontend fetches and displays real values. The "editable" message stays until PUT endpoint is implemented.

#### 1.2.4 GeneralTab.tsx — "Coming Soon" Placeholder

**File**: `apps/web/src/app/admin/(dashboard)/config/GeneralTab.tsx`
**Lines**: 46-51

**Current**: Dashed border box with "Coming Soon" and "General settings configuration will be available in a future update."

**Fix**: Replace with `EmptyFeatureState` component (Phase 3) with specific description of what general settings will include.

#### 1.2.5 ReportsTab.tsx — "Coming Soon" Message

**File**: `apps/web/src/app/admin/(dashboard)/analytics/ReportsTab.tsx`
**Line**: 56

**Current**: "Report generation coming soon."

**Fix**: Replace with `EmptyFeatureState` component with issue link.

### 1.3 Backend Stub Fixes

#### 1.3.1 Agent Models List — Static Mock Data

**Endpoint**: `GET /api/v1/admin/agents/models`

**Current**: Returns 5 hardcoded model objects with fake metrics (invocations, latency, cost).

**Fix**: Query real data from `AgentTestResult` table aggregated by model. Return actual invocation counts, average latency, total cost per model. If no data exists for a model, return zeros (honest).

**Implementation**: The mock data is currently inline in the routing lambda at `AdminAgentAnalyticsEndpoints.cs` (lines 58-73) — there is no MediatR handler. Create a proper `GetAgentModelsQuery` + `GetAgentModelsQueryHandler` in `KnowledgeBase/Application/Queries/` (where `AgentTestResult` lives), then replace the inline lambda with `await mediator.Send(new GetAgentModelsQuery(), ct)`.

#### 1.3.2 Vector Store Metrics — Returns Zeros

**Endpoint**: `GET /api/v1/admin/resources/vectors/metrics`

**Current**: `GetVectorStoreMetricsQueryHandler` returns hardcoded zeros after Qdrant removal.

**Fix**: Query pgvector metadata. Return:
- Total vectors count: `SELECT count(*) FROM document_embeddings`
- Index size: `SELECT pg_relation_size('document_embeddings_embedding_idx')`
- Dimensions: from embedding configuration
- Collections/tables using pgvector

#### 1.3.3 Database Growth Trends — Hardcoded Zeros

**Endpoint**: `GET /api/v1/admin/resources/database/metrics` (growth portion)

**Current**: `growthLast7Days = 0L; growthLast30Days = 0L; growthLast90Days = 0L;`

**Fix**: Implement historical tracking (see Phase 2, section 2.5 — DatabaseMetricsSnapshot).

---

## Phase 2: Implement High-Value Endpoints

### 2.1 Dynamic Configuration System

Extend the **existing** `SystemConfiguration` bounded context to serve configuration values currently hardcoded in frontend.

#### Backend — Existing Infrastructure

The `SystemConfiguration` BC already exists with:
- **Entity**: `SystemConfiguration.cs` (Key, Value, Category, Description, CreatedAt, UpdatedAt)
- **Table**: `system_configurations` (already in `InitialCreate` migration)
- **Queries**: `GetAllConfigsQuery`, `GetConfigByKeyQuery`, `GetConfigCategoriesQuery`
- **Commands**: `UpdateConfigValueCommand`, `BulkUpdateConfigsCommand`, `RollbackConfigCommand`

#### What's New

**Existing handler**: `GetAllConfigsQueryHandler` already supports category filtering via `GetAllConfigsQuery(Category: "models")` which calls `_configurationRepository.GetByCategoryAsync()`. No new handler needed.

**Add routing file**: `AdminConfigEndpoints.cs` with category-specific convenience endpoints that call `GetAllConfigsQuery` with the appropriate category parameter:

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/v1/admin/config/models` | Calls `GetAllConfigsQuery(Category: "models")` |
| `GET` | `/api/v1/admin/config/strategies` | Calls `GetAllConfigsQuery(Category: "strategies")` |
| `GET` | `/api/v1/admin/config/rerankers` | Calls `GetAllConfigsQuery(Category: "rerankers")` |
| `GET` | `/api/v1/admin/config/rate-limits` | Calls `GetAllConfigsQuery(Category: "rate-limits")` |
| `PUT` | `/api/v1/admin/config/{category}/{key}` | Calls existing `UpdateConfigValueCommand` |

**Caching**: HybridCache with 15-minute TTL, invalidated on PUT.

**Seed data** (via migration data seed, NOT table creation): Insert current hardcoded values into existing `system_configurations` table:
- `PROVIDER_MODELS` from playground/page.tsx, strategy/page.tsx → category "models"
- `STRATEGY_OPTIONS` from playground/page.tsx → category "strategies"
- `RERANKER_MODELS` from strategy/page.tsx → category "rerankers"
- `AVAILABLE_MODELS` from ab-testing/new/page.tsx → category "models"
- `CACHE_TTL_OPTIONS` from strategy/page.tsx → category "strategies"
- `RATE_LIMIT_CATEGORIES` from RateLimitsTab.tsx → category "rate-limits"

#### Frontend

**New hook**: `useAdminConfig(category: string)`

Note: `GetAllConfigsQuery` returns a `PagedConfigurationResult` wrapper. The endpoint should unwrap and return only the `.Items` list directly to simplify frontend consumption.

```typescript
function useAdminConfig(category: string) {
  return useQuery({
    queryKey: ['admin', 'config', category],
    queryFn: () => adminClient.config.getByCategory(category),
    staleTime: 15 * 60 * 1000, // match backend cache TTL
    select: (data) => data.items ?? data, // handle paginated or flat response
  });
}
```

**Files to update** (replace hardcoded constants with `useAdminConfig`):
1. `agents/definitions/playground/page.tsx` — PROVIDER_MODELS, STRATEGY_OPTIONS
2. `agents/ab-testing/new/page.tsx` — AVAILABLE_MODELS
3. `agents/strategy/page.tsx` — PROVIDER_MODELS, RERANKER_MODELS, CACHE_TTL_OPTIONS
4. `config/RateLimitsTab.tsx` — RATE_LIMIT_CATEGORIES

### 2.2 Analytics Endpoints

Backend endpoints for admin analytics pages that currently have no data source.

| Endpoint | Query Source | Response |
|----------|-------------|----------|
| `GET /api/v1/admin/analytics/overview` | Users, Games, Documents, ChatThreads | Total counts, today's new, growth % |
| `GET /api/v1/admin/analytics/chat` | ChatThread + ChatMessage | Messages/day, avg session length, top agents |
| `GET /api/v1/admin/analytics/pdf` | PdfDocument + ProcessingJob | Uploads/day, avg processing time, success rate |
| `GET /api/v1/admin/analytics/model-performance` | AgentTestResult | Latency P50/P95, cost/query, accuracy by model |
| `GET /api/v1/admin/analytics/mau` | UserSession | MAU, DAU, retention rate, active user trend |

All responses cached with HybridCache (5-minute TTL for real-time, 1-hour for historical).

**Cross-BC Data Access Strategy**: Analytics queries need data from multiple bounded contexts (ChatThread/ChatMessage from KnowledgeBase, PdfDocument/ProcessingJob from DocumentProcessing, UserSession from Authentication).

The existing `MeepleAiDbContext` already exposes all required `DbSet`s (`ChatThreads`, `PdfDocuments`, `UserSessions`, etc.) in a single context. Rather than creating a separate `AnalyticsReadDbContext` (which would require duplicate `DbSet` registrations and DI complexity), analytics handlers inject the existing `MeepleAiDbContext` and use `.AsNoTracking()` on all queries. This is consistent with the existing cross-BC read patterns already in the codebase (e.g., dashboard handlers already query across BCs).

Each analytics handler lives in `Administration/Application/Queries/Analytics/` and uses `_db.Set<T>().AsNoTracking()` for all read operations.

### 2.3 Batch Jobs — Frontend Path Fix

The batch jobs system is **already fully implemented** in the backend:
- Entity: `BatchJob.cs` in Administration BC
- Table: `batch_jobs` (in `InitialCreate` migration)
- Endpoints: `BatchJobEndpoints.cs` at `/api/v1/admin/operations/batch-jobs`

**Problem**: All 6 frontend methods in `adminSystemClient.ts` call `/api/v1/admin/batch-jobs` (missing `/operations/` segment), causing 404 errors:

| Method | Line | Wrong Path | Correct Path |
|--------|------|-----------|-------------|
| `createBatchJob()` | 167 | `/api/v1/admin/batch-jobs` | `/api/v1/admin/operations/batch-jobs` |
| `getBatchJob(id)` | 172 | `/api/v1/admin/batch-jobs/${id}` | `/api/v1/admin/operations/batch-jobs/${id}` |
| `getAllBatchJobs()` | 191 | `/api/v1/admin/batch-jobs` | `/api/v1/admin/operations/batch-jobs` |
| `deleteBatchJob(id)` | 198 | `/api/v1/admin/batch-jobs/${id}` | `/api/v1/admin/operations/batch-jobs/${id}` |
| `cancelBatchJob(id)` | 202 | `/api/v1/admin/batch-jobs/${id}/cancel` | `/api/v1/admin/operations/batch-jobs/${id}/cancel` |
| `retryBatchJob(id)` | 206 | `/api/v1/admin/batch-jobs/${id}/retry` | `/api/v1/admin/operations/batch-jobs/${id}/retry` |

**Fix**: Update all 6 method paths in `adminSystemClient.ts` to include `/operations/`. No new backend work needed.

### 2.4 Cache Metrics Verification

`GET /api/v1/admin/resources/cache/metrics` — already implemented in backend (Redis INFO parsing).

**Action**: Verify frontend `adminSystemClient.ts` calls the correct path and the response shape matches the frontend type definition. Fix any mismatches.

### 2.5 Database Growth Tracking

**New Entity**: `DatabaseMetricsSnapshot`

```
Properties:
- Id: Guid
- RecordedAt: DateTime
- TotalSizeBytes: long
- TableCount: int
- IndexSizeBytes: long
- ActiveConnections: int
```

**Scheduled Job**: Daily snapshot via `BackgroundService` (runs at 02:00 UTC, using `PeriodicTimer` in `ExecuteAsync` — matching the project pattern in `InvitationCleanupService`, `StalePdfRecoveryService`, etc.), queries `pg_database_size()` and stores in `database_metrics_snapshots`.

**Duplicate/concurrency guard**: Before inserting, check `WHERE DATE(recorded_at) = CURRENT_DATE`. If a snapshot already exists for today, skip insertion. This prevents duplicate entries on service restarts and handles multi-instance deployments gracefully.

**Growth Calculation**: `GetDatabaseMetricsQueryHandler` queries snapshots to compute growth. The existing handler uses a manually-managed `DbConnection` (explicit `Open()`/`Close()`) for raw SQL queries. To avoid connection ownership conflicts with EF Core, the snapshot query must also use raw SQL on the same connection:

```sql
SELECT total_size_bytes FROM database_metrics_snapshots
WHERE recorded_at >= @date ORDER BY recorded_at ASC LIMIT 1
```

Growth values:
- `growthLast7Days`: current size - snapshot from 7 days ago
- `growthLast30Days`: current size - snapshot from 30 days ago
- `growthLast90Days`: current size - snapshot from 90 days ago

The `DbSet<DatabaseMetricsSnapshotEntity>` registration in `MeepleAiDbContext` is required only so EF Core can generate the migration schema. The `GetDatabaseMetricsQueryHandler` must continue to use raw SQL via `GetDbConnection()` for all snapshot reads — no `_db.DatabaseMetricsSnapshots` calls in the handler.

**Migration**: `AddDatabaseMetricsSnapshots` — creates only the `database_metrics_snapshots` table. The `system_configurations` and `batch_jobs` tables already exist in `InitialCreate`.

---

## Phase 3: Honest UI for Unimplemented Features

### 3.1 EmptyFeatureState Component

**File**: `apps/web/src/components/admin/EmptyFeatureState.tsx`

Shared component for admin pages/sections without backend support.

```typescript
interface EmptyFeatureStateProps {
  icon?: LucideIcon;
  title: string;
  description: string;
  issueNumber?: number;      // links to GitHub issue
  issueLabel?: string;       // e.g. "Tracked in issue #896"
  className?: string;
}
```

**Visual design**:
- Rounded border (dashed, border-border/40)
- Icon (default: Construction icon from lucide)
- Title (font-medium, text-foreground)
- Description (text-sm, text-muted-foreground)
- Optional GitHub issue link (text-xs, underline, opens in new tab)
- Padding: p-8 for full-section, p-6 for in-card

### 3.2 Pages to Update

Each page below: replace mock/placeholder content with `EmptyFeatureState` per section.

| Section | Page Path | Empty State Title | Issue |
|---------|-----------|-------------------|-------|
| N8N Workflows | `config/n8n/page.tsx` | "Integrazione N8N" | #60 |
| Infrastructure Status | `monitor/infrastructure/page.tsx` | "Monitoraggio Infrastruttura" | #896 |
| Container Management | `monitor/containers/page.tsx` | "Gestione Container" | #896 |
| Service Status | `monitor/services/page.tsx` | "Stato Servizi" | #896 |
| Log Viewer | `monitor/logs/page.tsx` | "Visualizzatore Log" | — |
| LLM Emergency | (within ai page) | "Controlli di Emergenza LLM" | #125 |
| Report Generation | `analytics/ReportsTab.tsx` | "Generazione Report" | #920 |
| Financial Ledger | (if page exists) | "Registro Finanziario" | #3722 |
| Cost Calculator | (if page exists) | "Calcolatore Costi" | #3725 |
| Resource Forecast | (if page exists) | "Previsione Risorse" | #3726 |
| OpenRouter Usage | (within ai page) | "Utilizzo OpenRouter" | — |
| Alert History | `monitor/AlertHistoryTab.tsx` | "Storico Allarmi" | — |
| Bulk Export | `monitor/BulkExportTab.tsx` | "Export Massivo" | — |
| Testing Dashboards | `monitor/testing/page.tsx` | "Dashboard Testing" | — |
| General Settings | `config/GeneralTab.tsx` | "Impostazioni Generali" | — |

### 3.3 Frontend Error Handling Improvement

For pages that DO call real APIs but may get 404 (because endpoint exists in client but not backend), improve error handling:

**Pattern**: Wrap API calls with graceful fallback:

```typescript
const { data, error, isLoading } = useQuery({
  queryKey: ['admin', 'feature-x'],
  queryFn: () => adminClient.featureX.getData(),
  retry: false, // don't retry 404s
});

if (error?.status === 404) {
  return <EmptyFeatureState title="..." description="Endpoint non ancora disponibile" />;
}
```

This ensures that any unimplemented endpoint shows an honest empty state instead of a broken page.

---

## Technical Details

### Database Migrations

**Already existing tables** (in `InitialCreate`, no changes needed):
- `system_configurations` — used for dynamic config (Phase 2.1 seed data only)
- `batch_jobs` — used for batch job management (Phase 2.3, already complete)

**New migration**: `AddDatabaseMetricsSnapshots`

```
Table created:
1. database_metrics_snapshots
   - id (uuid, PK)
   - recorded_at (timestamptz, indexed, unique per day)
   - total_size_bytes (bigint)
   - table_count (int)
   - index_size_bytes (bigint)
   - active_connections (int)
```

**Seed data migration**: `SeedSystemConfigurationDefaults`

Inserts hardcoded frontend constants into existing `system_configurations` table:
- Category "models": provider model lists (OpenRouter, Ollama, Anthropic, OpenAI)
- Category "strategies": RAG strategy options
- Category "rerankers": reranker model options
- Category "rate-limits": API rate limit values

### Backend File Structure

**Existing files (modify only)**:
- `Routing/AdminAgentAnalyticsEndpoints.cs` — replace inline mock with MediatR call
- `Routing/AdminConfigEndpoints.cs` — add category-based config route handlers to existing `MapAdminConfigEndpoints()`
- `BoundedContexts/Administration/Application/Queries/Resources/GetDatabaseMetricsQueryHandler.cs` — add raw SQL growth calculation
- `Infrastructure/MeepleAiDbContext.cs` — add `DbSet<DatabaseMetricsSnapshotEntity>`

**New files**:
```
BoundedContexts/Administration/
├── Domain/
│   └── Entities/
│       └── DatabaseMetricsSnapshot.cs (domain entity)
├── Application/
│   ├── Queries/
│   │   └── Analytics/
│   │       ├── GetOverviewAnalyticsQuery.cs
│   │       ├── GetChatAnalyticsQuery.cs
│   │       ├── GetPdfAnalyticsQuery.cs
│   │       ├── GetModelPerformanceQuery.cs
│   │       └── GetMauQuery.cs
│   └── Services/
│       └── DatabaseMetricsSnapshotService.cs (BackgroundService)

Infrastructure/
└── Entities/
    └── Administration/
        └── DatabaseMetricsSnapshotEntity.cs (EF entity mapping)

BoundedContexts/KnowledgeBase/
└── Application/
    └── Queries/
        └── GetAgentModelsQuery.cs (new — replaces inline mock)

Routing/
├── AdminAnalyticsEndpoints.cs (new — analytics aggregation endpoints)
└── AdminAgentTypologyEndpoints.cs (new — admin-scoped, reuses existing commands/queries)
```

### Frontend File Structure

```
apps/web/src/
├── components/admin/
│   └── EmptyFeatureState.tsx (new)
├── hooks/
│   └── useAdminConfig.ts (new)
└── lib/api/clients/admin/
    ├── adminAiClient.ts (fix route paths)
    ├── adminContentClient.ts (fix route path)
    └── adminConfigClient.ts (new - config endpoints)
```

### Caching Strategy

| Endpoint | Cache TTL | Invalidation |
|----------|-----------|--------------|
| Config (models, strategies) | 15 min | On PUT update |
| Analytics (overview, chat, pdf) | 5 min | Time-based |
| MAU/retention | 1 hour | Time-based |
| Database metrics | 5 min | Time-based |
| Batch jobs list | No cache | Real-time (already implemented) |

---

## Testing Strategy

### Phase 1 Tests
- **Route fix verification**: Integration tests calling fixed endpoints, asserting 200 (not 404)
- **Frontend**: Verify no console errors on affected admin pages

### Phase 2 Tests
- **Unit tests**: Each new Query/Command handler tested in isolation
- **Integration tests**: Endpoint tests with real DB (Testcontainers)
  - Config by category: seed data, query by category, update, verify cache invalidation
  - Analytics: seed cross-BC data, query via AnalyticsReadDbContext, verify aggregation correctness
  - Batch jobs: verify frontend client uses correct `/admin/operations/batch-jobs` path
  - Database snapshots: create snapshot, verify duplicate guard, query growth calculation
- **Frontend**: `useAdminConfig` hook test with mocked API

### Phase 3 Tests
- **Component test**: `EmptyFeatureState` renders title, description, issue link
- **Integration**: Pages with 404 endpoints show EmptyFeatureState (not crash)

### Smoke Test
Navigate all 77+ admin pages and verify:
- No 404/500 errors in network tab
- No uncaught exceptions in console
- Every section shows either real data or EmptyFeatureState

---

## Execution Order

```
Phase 1 (Fix Immediati)
├── 1.1 Route mismatch fixes (2 files)
├── 1.2 Frontend mock removal (5 files)
└── 1.3 Backend stub fixes (3 handlers)

Phase 2 (High-Value Endpoints)
├── 2.0 Migrations (AddDatabaseMetricsSnapshots + SeedSystemConfigurationDefaults)
├── 2.1 Config system (5 new endpoints using existing GetAllConfigsQuery + seed + frontend hook)
├── 2.2 Analytics endpoints (5 query handlers using MeepleAiDbContext.AsNoTracking() + routing)
├── 2.3 Batch jobs path verification (frontend path fix only)
├── 2.4 Cache metrics verification
└── 2.5 Database growth tracking (entity + hosted service with duplicate guard)

Phase 3 (Honest UI)
├── 3.1 EmptyFeatureState component
├── 3.2 Update ~15 pages
├── 3.3 404 error handling pattern
└── 3.4 Smoke test all pages
```

---

## Success Criteria

1. **Zero 404 errors** when navigating any admin page
2. **Zero mock/fake data** displayed anywhere in admin
3. **Every section** shows either real API data or an honest `EmptyFeatureState`
4. **Dynamic configuration** served from backend, not hardcoded in frontend
5. **All existing tests pass** after changes
6. **New tests** cover all new endpoints and components
7. **Admin pages load** without console errors or uncaught exceptions
