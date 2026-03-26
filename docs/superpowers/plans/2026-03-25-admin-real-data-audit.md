# Admin Real Data Audit — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all mock/placeholder data from 77+ admin pages, wire to real backend endpoints, and show honest empty states for unimplemented features.

**Architecture:** 3-phase progressive approach. Phase 1 fixes broken routes and removes fake data from existing pages. Phase 2 implements new backend endpoints (config, analytics, database tracking) and wires frontend. Phase 3 creates an `EmptyFeatureState` component and applies it to ~15 unimplemented pages.

**Tech Stack:** .NET 9 (MediatR CQRS, EF Core, PostgreSQL), Next.js 16 (React Query, Tailwind 4, shadcn/ui)

**Spec:** `docs/superpowers/specs/2026-03-25-admin-real-data-audit-design.md`

---

## Phase 1: Fix Immediati

### Task 1: Fix Agent Typology Route — Backend Admin Endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/AdminAgentTypologyEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs` (register new endpoint group)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Routing/AdminAgentTypologyEndpointsTests.cs`

**Reference files:**
- `apps/api/src/Api/Routing/AgentTypologyEndpoints.cs` (existing user endpoints pattern)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/DeleteAgentTypologyCommand.cs` (reuse)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ApproveAgentTypologyCommand.cs` (reuse)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/RejectAgentTypologyCommand.cs` (reuse)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentTypologiesQuery.cs` (reuse)

- [ ] **Step 1: Write integration test for admin GET all typologies**

Test that `GET /api/v1/admin/agent-typologies` returns 200 with `RequireAdminSession()`.

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task GetAllTypologies_AsAdmin_Returns200()
{
    // Arrange: seed an agent typology
    // Act: GET /api/v1/admin/agent-typologies with admin session
    // Assert: 200 OK, response contains seeded typology
}
```

- [ ] **Step 2: Run test — verify it fails (404 — endpoint doesn't exist yet)**

Run: `dotnet test --filter "GetAllTypologies_AsAdmin_Returns200"`
Expected: FAIL with 404

- [ ] **Step 3: Create `AdminAgentTypologyEndpoints.cs`**

Create routing file at `apps/api/src/Api/Routing/AdminAgentTypologyEndpoints.cs`. Pattern: follow `AgentTypologyEndpoints.cs` but use `RequireAdminSession()` and call existing queries/commands via MediatR:

**Important**: The existing queries require `UserRole` and `UserId` parameters. For admin endpoints, pass `UserRole: "Admin"` and the admin's UserId from the session. The correct query class names are:

- `GET /admin/agent-typologies` → `mediator.Send(new GetAllAgentTypologiesQuery(UserRole: "Admin", UserId: session.User.Id))`
- `GET /admin/agent-typologies/{id}` → `mediator.Send(new GetTypologyByIdQuery(TypologyId: id, UserRole: "Admin", UserId: session.User.Id))` (note: class is `GetTypologyByIdQuery`, NOT `GetAgentTypologyByIdQuery`)
- `DELETE /admin/agent-typologies/{id}` → `mediator.Send(new DeleteAgentTypologyCommand(id))`
- `POST /admin/agent-typologies/{id}/approve` → `mediator.Send(new ApproveAgentTypologyCommand(id))`
- `POST /admin/agent-typologies/{id}/reject` → `mediator.Send(new RejectAgentTypologyCommand(id))`

Register in `Program.cs` under the admin route group: `adminApi.MapGroup("/agent-typologies").MapAdminAgentTypologyEndpoints();`

- [ ] **Step 4: Run test — verify it passes**

Run: `dotnet test --filter "GetAllTypologies_AsAdmin_Returns200"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/AdminAgentTypologyEndpoints.cs apps/api/src/Api/Program.cs apps/api/tests/
git commit -m "feat(admin): add admin-scoped agent typology endpoints"
```

---

### Task 2: Fix Agent Typology Route — Frontend Client Path

**Files:**
- Modify: `apps/web/src/lib/api/clients/admin/adminAiClient.ts` (lines 449-485)

- [ ] **Step 1: Fix all 5 agent typology method paths**

In `adminAiClient.ts`, update all agent typology methods to use `/api/v1/admin/agent-typologies` path:

- `getAgentTypologies()` (line ~449): change path from `/admin/agent-typologies` to `/api/v1/admin/agent-typologies`
- `getAgentTypologyById(id)` (line ~469): same fix
- `deleteAgentTypology(id)` (line ~473): same fix
- `approveAgentTypology(id)` (line ~477): same fix
- `rejectAgentTypology(id)` (line ~481): same fix

Note: Check how other methods in the same file construct URLs. If there's a base URL prefix already applied, only fix the relative path portion.

- [ ] **Step 2: Verify by searching for remaining wrong paths**

Run: `grep -n "agent-typologies" apps/web/src/lib/api/clients/admin/adminAiClient.ts`
Expected: All paths should contain `/api/v1/admin/agent-typologies`

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/admin/adminAiClient.ts
git commit -m "fix(admin): correct agent typology API paths in frontend client"
```

---

### Task 3: Fix Prompt Audit Log Route

**Files:**
- Modify: `apps/web/src/lib/api/clients/admin/adminContentClient.ts` (line ~219)

- [ ] **Step 1: Fix prompt audit log path**

In `adminContentClient.ts`, find the `getPromptAuditLogs()` method (around line 206-223). Change the path from `/api/v1/prompts/{id}/audit-log` to `/api/v1/admin/prompts/{id}/audit-log`.

- [ ] **Step 2: Verify**

Run: `grep -n "audit-log" apps/web/src/lib/api/clients/admin/adminContentClient.ts`
Expected: Path contains `/admin/prompts/`

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/admin/adminContentClient.ts
git commit -m "fix(admin): correct prompt audit log API path"
```

---

### Task 4: Fix Batch Jobs Frontend Paths

**Files:**
- Modify: `apps/web/src/lib/api/clients/admin/adminSystemClient.ts` (lines 166-207)

- [ ] **Step 1: Fix all 6 batch job method paths**

In `adminSystemClient.ts`, update all batch job methods to include `/operations/`:

| Method | Line | Change |
|--------|------|--------|
| `createBatchJob()` | ~167 | `/admin/batch-jobs` → `/admin/operations/batch-jobs` |
| `getBatchJob(id)` | ~172 | `/admin/batch-jobs/${id}` → `/admin/operations/batch-jobs/${id}` |
| `getAllBatchJobs()` | ~191 | `/admin/batch-jobs` → `/admin/operations/batch-jobs` |
| `deleteBatchJob(id)` | ~198 | `/admin/batch-jobs/${id}` → `/admin/operations/batch-jobs/${id}` |
| `cancelBatchJob(id)` | ~202 | `/admin/batch-jobs/${id}/cancel` → `/admin/operations/batch-jobs/${id}/cancel` |
| `retryBatchJob(id)` | ~206 | `/admin/batch-jobs/${id}/retry` → `/admin/operations/batch-jobs/${id}/retry` |

- [ ] **Step 2: Verify no remaining wrong paths**

Run: `grep -n "admin/batch-jobs" apps/web/src/lib/api/clients/admin/adminSystemClient.ts`
Expected: All matches contain `/operations/batch-jobs`

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/admin/adminSystemClient.ts
git commit -m "fix(admin): correct batch jobs API paths (add /operations/ segment)"
```

---

### Task 5: Remove BulkExportTab Fake setTimeout

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/BulkExportTab.tsx` (lines 44-53)

- [ ] **Step 1: Remove fake handleExport and disable buttons**

In `BulkExportTab.tsx`:
1. Remove the `handleExport` function that uses `setTimeout(600ms)`
2. Add `disabled` prop to all export buttons
3. Add a tooltip or small text: "Export non disponibile — endpoint non implementato"

- [ ] **Step 2: Verify the page renders without errors**

Run: `cd apps/web && pnpm build` (check no TS errors)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/BulkExportTab.tsx
git commit -m "fix(admin): remove fake export setTimeout, disable export buttons"
```

---

### Task 6: Wire LlmConfigTab to Real API Data

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/ai/LlmConfigTab.tsx` (lines 302-308)

**Reference:** The `GET /api/v1/admin/llm/config` endpoint already returns `LlmSystemConfigDto` with fields `CircuitBreakerFailureThreshold`, `CircuitBreakerOpenDurationSeconds`, `CircuitBreakerSuccessThreshold`, `DailyBudgetUsd`, `MonthlyBudgetUsd`.

- [ ] **Step 1: Verify the existing API response includes circuit breaker fields**

Search the backend for the DTO: `grep -rn "CircuitBreakerFailureThreshold" apps/api/src/Api/BoundedContexts/SystemConfiguration/`
Expected: Fields exist in both entity and DTO

- [ ] **Step 2: Update LlmConfigTab to read from API response**

In `LlmConfigTab.tsx`, the component likely already fetches LLM config data. Find the existing `useQuery` call and replace the hardcoded `ReadOnlyRow` values (lines 302-308) with values from the API response:

```tsx
<ReadOnlyRow label="Default Failure Threshold" value={String(config?.circuitBreakerFailureThreshold ?? '—')} />
<ReadOnlyRow label="Default Open Duration" value={`${config?.circuitBreakerOpenDurationSeconds ?? '—'}s`} />
<ReadOnlyRow label="Default Success Threshold" value={String(config?.circuitBreakerSuccessThreshold ?? '—')} />
<ReadOnlyRow label="Default Daily Budget" value={config?.dailyBudgetUsd != null ? `$${config.dailyBudgetUsd.toFixed(2)}` : '—'} />
<ReadOnlyRow label="Default Monthly Budget" value={config?.monthlyBudgetUsd != null ? `$${config.monthlyBudgetUsd.toFixed(2)}` : '—'} />
```

- [ ] **Step 3: Verify build passes**

Run: `cd apps/web && pnpm build`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/ai/LlmConfigTab.tsx
git commit -m "fix(admin): wire LlmConfigTab to real API data instead of hardcoded values"
```

---

### Task 7: Replace Agent Models Inline Mock with Real Query

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAgentModelsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAgentModelsQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminAgentAnalyticsEndpoints.cs` (lines 58-73)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Queries/GetAgentModelsQueryHandlerTests.cs`

- [ ] **Step 1: Write unit test for the new query handler**

```csharp
[Fact]
[Trait("Category", "Unit")]
public async Task Handle_WithTestResults_ReturnsAggregatedModelMetrics()
{
    // Arrange: seed AgentTestResult records for 2 different models
    // Act: Send GetAgentModelsQuery
    // Assert: Returns 2 models with correct invocation counts, avg latency, total cost
}
```

- [ ] **Step 2: Run test — verify it fails**

Run: `dotnet test --filter "Handle_WithTestResults_ReturnsAggregatedModelMetrics"`
Expected: FAIL (class doesn't exist)

- [ ] **Step 3: Create `GetAgentModelsQuery` record**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAgentModelsQuery.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

internal record GetAgentModelsQuery() : IQuery<IReadOnlyList<AgentModelMetricsDto>>;

internal record AgentModelMetricsDto(
    string Provider,
    string ModelName,
    int Invocations,
    double AverageLatencyMs,
    decimal TotalCost,
    double AverageConfidence);
```

- [ ] **Step 4: Create `GetAgentModelsQueryHandler`**

Query `AgentTestResult` table grouped by model. Use `_db.Set<AgentTestResultEntity>().AsNoTracking()`. Aggregate: COUNT, AVG(latency), SUM(cost), AVG(confidence).

- [ ] **Step 5: Replace inline mock in routing**

In `AdminAgentAnalyticsEndpoints.cs` (lines 58-73), replace the hardcoded anonymous object array with:

```csharp
var result = await mediator.Send(new GetAgentModelsQuery(), ct);
return Results.Ok(result);
```

- [ ] **Step 6: Run test — verify it passes**

Run: `dotnet test --filter "Handle_WithTestResults_ReturnsAggregatedModelMetrics"`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAgentModelsQuery*.cs
git add apps/api/src/Api/Routing/AdminAgentAnalyticsEndpoints.cs
git add apps/api/tests/
git commit -m "feat(admin): replace hardcoded agent models mock with real DB aggregation"
```

---

### Task 8: Fix Vector Store Metrics (pgvector)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Resources/GetVectorStoreMetricsQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Queries/GetVectorStoreMetricsQueryHandlerTests.cs`

- [ ] **Step 1: Write test for real pgvector metrics**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Handle_ReturnsPgvectorMetrics_NotZeros()
{
    // Arrange: seed a document_embedding row
    // Act: Send GetVectorStoreMetricsQuery
    // Assert: totalVectors > 0
}
```

- [ ] **Step 2: Run test — verify it fails**

Expected: FAIL (handler returns zeros)

- [ ] **Step 3: Update handler to query pgvector**

Replace hardcoded zeros with raw SQL queries:

```csharp
var totalVectors = await connection.ExecuteScalarAsync<long>(
    "SELECT count(*) FROM document_embeddings");
var indexSize = await connection.ExecuteScalarAsync<long>(
    "SELECT pg_relation_size('document_embeddings_embedding_idx')");
```

Use the same `GetDbConnection()` pattern as `GetDatabaseMetricsQueryHandler`.

- [ ] **Step 4: Run test — verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Resources/GetVectorStoreMetricsQueryHandler.cs
git add apps/api/tests/
git commit -m "fix(admin): return real pgvector metrics instead of hardcoded zeros"
```

---

### Task 9: Phase 1 Commit — All Route & Mock Fixes

- [ ] **Step 1: Verify all Phase 1 changes build**

```bash
cd apps/api/src/Api && dotnet build
cd ../../../web && pnpm build
```

- [ ] **Step 2: Run existing tests to verify no regressions**

```bash
cd apps/api/src/Api && dotnet test --filter "Category=Unit"
```

- [ ] **Step 3: Commit Phase 1 summary (if any unstaged changes)**

---

## Phase 2: Implement High-Value Endpoints

### Task 10: Migration — Database Metrics Snapshots Table

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/Administration/DatabaseMetricsSnapshotEntity.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/DatabaseMetricsSnapshot.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (add DbSet)

- [ ] **Step 1: Create domain entity**

```csharp
// File: apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/DatabaseMetricsSnapshot.cs
namespace Api.BoundedContexts.Administration.Domain.Entities;

public sealed class DatabaseMetricsSnapshot
{
    public Guid Id { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public long TotalSizeBytes { get; private set; }
    public int TableCount { get; private set; }
    public long IndexSizeBytes { get; private set; }
    public int ActiveConnections { get; private set; }

    public static DatabaseMetricsSnapshot Create(
        long totalSizeBytes, int tableCount, long indexSizeBytes, int activeConnections)
    {
        return new DatabaseMetricsSnapshot
        {
            Id = Guid.NewGuid(),
            RecordedAt = DateTime.UtcNow,
            TotalSizeBytes = totalSizeBytes,
            TableCount = tableCount,
            IndexSizeBytes = indexSizeBytes,
            ActiveConnections = activeConnections
        };
    }
}
```

- [ ] **Step 2: Create EF entity mapping**

```csharp
// File: apps/api/src/Api/Infrastructure/Entities/Administration/DatabaseMetricsSnapshotEntity.cs
namespace Api.Infrastructure.Entities.Administration;

public sealed class DatabaseMetricsSnapshotEntity
{
    public Guid Id { get; set; }
    public DateTime RecordedAt { get; set; }
    public long TotalSizeBytes { get; set; }
    public int TableCount { get; set; }
    public long IndexSizeBytes { get; set; }
    public int ActiveConnections { get; set; }
}
```

Follow the existing entity pattern — check how `AuditLogEntity.cs` is configured in `MeepleAiDbContext.OnModelCreating()`.

- [ ] **Step 3: Add DbSet to MeepleAiDbContext**

In `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`, add:

```csharp
public DbSet<DatabaseMetricsSnapshotEntity> DatabaseMetricsSnapshots => Set<DatabaseMetricsSnapshotEntity>();
```

Add entity configuration in `OnModelCreating`:

```csharp
modelBuilder.Entity<DatabaseMetricsSnapshotEntity>(e =>
{
    e.ToTable("database_metrics_snapshots");
    e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id");
    e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
    e.Property(x => x.TotalSizeBytes).HasColumnName("total_size_bytes");
    e.Property(x => x.TableCount).HasColumnName("table_count");
    e.Property(x => x.IndexSizeBytes).HasColumnName("index_size_bytes");
    e.Property(x => x.ActiveConnections).HasColumnName("active_connections");
    e.HasIndex(x => x.RecordedAt);
});
```

- [ ] **Step 4: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddDatabaseMetricsSnapshots
```

- [ ] **Step 5: Review generated migration SQL**

Read the generated migration file and verify it only creates `database_metrics_snapshots` table.

- [ ] **Step 6: Apply migration locally**

```bash
dotnet ef database update
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/Administration/DatabaseMetricsSnapshotEntity.cs
git add apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/DatabaseMetricsSnapshot.cs
git add apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(admin): add database_metrics_snapshots table for growth tracking"
```

---

### Task 11: Seed System Configuration Defaults

**Files:**
- Create: migration seed file via `dotnet ef migrations add`

**Reference:** Read the hardcoded constants from:
- `apps/web/src/app/admin/(dashboard)/agents/definitions/playground/page.tsx` — PROVIDER_MODELS, STRATEGY_OPTIONS
- `apps/web/src/app/admin/(dashboard)/agents/ab-testing/new/page.tsx` — AVAILABLE_MODELS
- `apps/web/src/app/admin/(dashboard)/agents/strategy/page.tsx` — PROVIDER_MODELS, RERANKER_MODELS, CACHE_TTL_OPTIONS
- `apps/web/src/app/admin/(dashboard)/config/RateLimitsTab.tsx` — RATE_LIMIT_CATEGORIES

- [ ] **Step 1: Read all hardcoded constants from frontend files**

Extract the exact JSON values from each frontend file listed above.

- [ ] **Step 2: Create seed migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add SeedSystemConfigurationDefaults
```

In the generated migration `Up()` method, use `migrationBuilder.InsertData()` to insert rows into `system_configurations` table:

```csharp
migrationBuilder.InsertData(
    table: "system_configurations",
    columns: new[] { "id", "key", "value", "category", "description", "is_active", "created_at", "updated_at" },
    values: new object[] { Guid.NewGuid(), "openrouter", "[{\"value\":\"anthropic/claude-3.5-sonnet\",...}]", "models", "OpenRouter provider models", true, DateTime.UtcNow, DateTime.UtcNow }
);
```

Insert one row per category/key combination:
- `models/openrouter` — OpenRouter models JSON
- `models/ollama` — Ollama models JSON
- `models/anthropic` — Anthropic models JSON
- `models/openai` — OpenAI models JSON
- `models/ab-testing` — A/B testing available models JSON
- `strategies/rag` — RAG strategy options JSON
- `strategies/cache-ttl` — Cache TTL options JSON
- `rerankers/default` — Reranker models JSON
- `rate-limits/api` — API rate limits JSON
- `rate-limits/ai` — AI rate limits JSON
- `rate-limits/websocket` — WebSocket rate limits JSON

- [ ] **Step 3: Apply migration**

```bash
dotnet ef database update
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(admin): seed system configuration defaults (models, strategies, rate-limits)"
```

---

### Task 12: Add Config Category Endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminConfigEndpoints.cs` (add to existing `MapAdminConfigEndpoints()`)

**Reference:** `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Queries/GetAllConfigsQuery.cs` — record signature with `Category` parameter

- [ ] **Step 1: Write integration test**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task GetConfigByCategory_Models_ReturnsSeededData()
{
    // Arrange: seed system_configurations with category "models"
    // Act: GET /api/v1/admin/config/models
    // Assert: 200, response contains model configs
}
```

- [ ] **Step 2: Run test — verify it fails**

- [ ] **Step 3: Add category routes to AdminConfigEndpoints.cs**

In the existing `MapAdminConfigEndpoints()` method, add new routes after the existing pdf-limits routes:

**Important**: The existing `AdminConfigEndpoints.cs` receives the `v1Api` group (not a `/admin/config` subgroup), so routes need full paths. Also, explicitly pass `ActiveOnly: true` to ensure intent is clear:

```csharp
group.MapGet("/admin/config/models", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetAllConfigsQuery(Category: "models", ActiveOnly: true, PageSize: 100), ct);
    return Results.Ok(result.Items);
}).RequireAdminSession();

group.MapGet("/admin/config/strategies", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetAllConfigsQuery(Category: "strategies", ActiveOnly: true, PageSize: 100), ct);
    return Results.Ok(result.Items);
}).RequireAdminSession();

group.MapGet("/admin/config/rerankers", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetAllConfigsQuery(Category: "rerankers", ActiveOnly: true, PageSize: 100), ct);
    return Results.Ok(result.Items);
}).RequireAdminSession();

group.MapGet("/admin/config/rate-limits", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetAllConfigsQuery(Category: "rate-limits", ActiveOnly: true, PageSize: 100), ct);
    return Results.Ok(result.Items);
}).RequireAdminSession();
```

- [ ] **Step 4: Run test — verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/AdminConfigEndpoints.cs apps/api/tests/
git commit -m "feat(admin): add config category endpoints (models, strategies, rerankers, rate-limits)"
```

---

### Task 13: Frontend Config Hook + Wire Pages

**Files:**
- Create: `apps/web/src/hooks/useAdminConfig.ts`
- Create: `apps/web/src/lib/api/clients/admin/adminConfigClient.ts`
- Modify: `apps/web/src/app/admin/(dashboard)/agents/definitions/playground/page.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/agents/ab-testing/new/page.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/agents/strategy/page.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/config/RateLimitsTab.tsx`

- [ ] **Step 1: Create adminConfigClient**

```typescript
// File: apps/web/src/lib/api/clients/admin/adminConfigClient.ts
const CATEGORY_PATHS: Record<string, string> = {
  models: '/api/v1/admin/config/models',
  strategies: '/api/v1/admin/config/strategies',
  rerankers: '/api/v1/admin/config/rerankers',
  'rate-limits': '/api/v1/admin/config/rate-limits',
};

export function createAdminConfigClient(httpClient: HttpClient) {
  return {
    getByCategory: (category: string) => {
      const path = CATEGORY_PATHS[category];
      if (!path) throw new Error(`Unknown config category: ${category}`);
      return httpClient.get(path);
    },
    getModels: () => httpClient.get('/api/v1/admin/config/models'),
    getStrategies: () => httpClient.get('/api/v1/admin/config/strategies'),
    getRerankers: () => httpClient.get('/api/v1/admin/config/rerankers'),
    getRateLimits: () => httpClient.get('/api/v1/admin/config/rate-limits'),
  };
}
```

Register in the admin client factory.

- [ ] **Step 2: Create useAdminConfig hook**

```typescript
// File: apps/web/src/hooks/useAdminConfig.ts
import { useQuery } from '@tanstack/react-query';

export function useAdminConfig(category: string) {
  return useQuery({
    queryKey: ['admin', 'config', category],
    queryFn: () => adminClient.config.getByCategory(category),
    staleTime: 15 * 60 * 1000,
    select: (data) => data.items ?? data,
  });
}
```

- [ ] **Step 3: Update playground/page.tsx — replace PROVIDER_MODELS, STRATEGY_OPTIONS**

Remove hardcoded `PROVIDER_MODELS` and `STRATEGY_OPTIONS` constants. Replace with `useAdminConfig('models')` and `useAdminConfig('strategies')`. Add loading state handling.

- [ ] **Step 4: Update ab-testing/new/page.tsx — replace AVAILABLE_MODELS**

Remove `AVAILABLE_MODELS` constant. Use `useAdminConfig('models')` filtered for AB testing models.

- [ ] **Step 5: Update strategy/page.tsx — replace PROVIDER_MODELS, RERANKER_MODELS, CACHE_TTL_OPTIONS**

Remove all 3 hardcoded constants. Use `useAdminConfig('models')`, `useAdminConfig('rerankers')`, `useAdminConfig('strategies')`.

- [ ] **Step 6: Update RateLimitsTab.tsx — replace RATE_LIMIT_CATEGORIES**

Remove `RATE_LIMIT_CATEGORIES` array. Use `useAdminConfig('rate-limits')`. Keep the "Editable configuration coming in future update" message.

- [ ] **Step 7: Verify build**

Run: `cd apps/web && pnpm build`

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/hooks/useAdminConfig.ts
git add apps/web/src/lib/api/clients/admin/adminConfigClient.ts
git add apps/web/src/app/admin/
git commit -m "feat(admin): replace hardcoded config constants with dynamic API data"
```

---

### Task 14: Analytics Endpoints — Overview + Chat

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetOverviewAnalyticsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetOverviewAnalyticsQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetChatAnalyticsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetChatAnalyticsQueryHandler.cs`
- Create: `apps/api/src/Api/Routing/AdminAnalyticsEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Queries/Analytics/`

- [ ] **Step 1: Write unit test for overview analytics**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Handle_OverviewAnalytics_ReturnsTotalCounts()
{
    // Seed: 5 users, 3 games, 2 documents, 1 chat thread
    // Act: Send GetOverviewAnalyticsQuery
    // Assert: TotalUsers=5, TotalGames=3, TotalDocuments=2, TotalChats=1
}
```

- [ ] **Step 2: Create GetOverviewAnalyticsQuery + Handler**

Handler uses `_db.Set<UserEntity>().AsNoTracking()`, `_db.Set<GameEntity>().AsNoTracking()`, etc. to count entities. Returns totals, today's new, and growth % (today vs yesterday).

- [ ] **Step 3: Create GetChatAnalyticsQuery + Handler**

Handler queries `ChatThreadEntity` and `ChatMessageEntity` with `.AsNoTracking()`. Returns messages/day (last 7 days), average session length, top agents by usage.

- [ ] **Step 4: Create AdminAnalyticsEndpoints.cs routing**

```csharp
group.MapGet("/overview", async (IMediator mediator, CancellationToken ct) =>
    Results.Ok(await mediator.Send(new GetOverviewAnalyticsQuery(), ct)))
    .RequireAdminSession();

group.MapGet("/chat", async (IMediator mediator, CancellationToken ct) =>
    Results.Ok(await mediator.Send(new GetChatAnalyticsQuery(), ct)))
    .RequireAdminSession();
```

Register: `adminApi.MapGroup("/analytics").MapAdminAnalyticsEndpoints();`

- [ ] **Step 5: Run tests**

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/
git add apps/api/src/Api/Routing/AdminAnalyticsEndpoints.cs
git add apps/api/tests/
git commit -m "feat(admin): add overview and chat analytics endpoints"
```

---

### Task 15: Analytics Endpoints — PDF + Model Performance + MAU

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetPdfAnalyticsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetPdfAnalyticsQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetModelPerformanceQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetModelPerformanceQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetMauQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetMauQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminAnalyticsEndpoints.cs` (add 3 routes)

- [ ] **Step 1: Write tests for PDF analytics, model performance, and MAU**

- [ ] **Step 2: Implement GetPdfAnalyticsQueryHandler**

Query `PdfDocumentEntity` + `ProcessingJobEntity` with `.AsNoTracking()`. Return uploads/day, avg processing time, success rate.

- [ ] **Step 3: Implement GetModelPerformanceQueryHandler**

Query `AgentTestResultEntity` grouped by model. Return P50/P95 latency, cost/query, accuracy by model.

- [ ] **Step 4: Implement GetMauQueryHandler**

Query `UserSessionEntity` with `.AsNoTracking()`. Return MAU (distinct users last 30 days), DAU (distinct last 24h), retention rate.

- [ ] **Step 5: Add routes to AdminAnalyticsEndpoints.cs**

```csharp
group.MapGet("/pdf", ...).RequireAdminSession();
group.MapGet("/model-performance", ...).RequireAdminSession();
group.MapGet("/mau", ...).RequireAdminSession();
```

- [ ] **Step 6: Run all analytics tests**

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/
git add apps/api/src/Api/Routing/AdminAnalyticsEndpoints.cs
git add apps/api/tests/
git commit -m "feat(admin): add PDF, model performance, and MAU analytics endpoints"
```

---

### Task 16: Database Growth Tracking — BackgroundService + Handler Update

**Files:**
- Create: `apps/api/src/Api/Infrastructure/BackgroundServices/DatabaseMetricsSnapshotService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Resources/GetDatabaseMetricsQueryHandler.cs` (lines 75-79)
- Modify: `apps/api/src/Api/Program.cs` (register BackgroundService)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Services/DatabaseMetricsSnapshotServiceTests.cs`

**Reference:** `apps/api/src/Api/Infrastructure/BackgroundServices/InvitationCleanupService.cs` (BackgroundService pattern)

- [ ] **Step 1: Write test for the BackgroundService**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task ExecuteAsync_CreatesSnapshot_WhenNoneExistsToday()
{
    // Act: trigger the service's snapshot logic
    // Assert: database_metrics_snapshots has 1 row for today
}

[Fact]
public async Task ExecuteAsync_SkipsSnapshot_WhenOneExistsToday()
{
    // Arrange: insert a snapshot for today
    // Act: trigger snapshot logic
    // Assert: still only 1 row (no duplicate)
}
```

- [ ] **Step 2: Create DatabaseMetricsSnapshotService**

Follow `InvitationCleanupService` pattern (inject `TimeProvider` for testability, use `_timeProvider.GetUtcNow()` instead of `DateTime.UtcNow`):

```csharp
// File: apps/api/src/Api/Infrastructure/BackgroundServices/DatabaseMetricsSnapshotService.cs
internal sealed class DatabaseMetricsSnapshotService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DatabaseMetricsSnapshotService> _logger;

    public DatabaseMetricsSnapshotService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<DatabaseMetricsSnapshotService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(stoppingToken);

            // Duplicate guard: check if snapshot exists for today
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT count(*) FROM database_metrics_snapshots WHERE DATE(recorded_at) = @today";
            var param = checkCmd.CreateParameter();
            param.ParameterName = "@today";
            param.Value = now.Date;
            checkCmd.Parameters.Add(param);
            var count = (long)(await checkCmd.ExecuteScalarAsync(stoppingToken))!;
            if (count > 0) { await connection.CloseAsync(); continue; }

            // Query pg_database_size, pg_stat_database, insert snapshot
            // ... (use connection.CreateCommand() for all queries)

            await connection.CloseAsync();
        }
    }
}
```

Register in `Program.cs`: `builder.Services.AddHostedService<DatabaseMetricsSnapshotService>();`

- [ ] **Step 3: Update GetDatabaseMetricsQueryHandler for growth**

In `GetDatabaseMetricsQueryHandler.cs`, replace lines 75-79 (hardcoded zeros) with raw SQL:

```csharp
// Query snapshots for growth calculation
var growthLast7Days = await GetGrowthSince(connection, currentSize, DateTime.UtcNow.AddDays(-7));
var growthLast30Days = await GetGrowthSince(connection, currentSize, DateTime.UtcNow.AddDays(-30));
var growthLast90Days = await GetGrowthSince(connection, currentSize, DateTime.UtcNow.AddDays(-90));

// Helper method (ensure connection is open before calling):
private static async Task<long> GetGrowthSince(DbConnection connection, long currentSize, DateTime since, CancellationToken ct)
{
    if (connection.State != System.Data.ConnectionState.Open)
        await connection.OpenAsync(ct);

    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT total_size_bytes FROM database_metrics_snapshots WHERE recorded_at >= @date ORDER BY recorded_at ASC LIMIT 1";
    var param = cmd.CreateParameter();
    param.ParameterName = "@date";
    param.Value = since;
    cmd.Parameters.Add(param);
    var result = await cmd.ExecuteScalarAsync(ct);
    return result is long pastSize ? currentSize - pastSize : 0L;
}
```

- [ ] **Step 4: Run tests**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/BackgroundServices/DatabaseMetricsSnapshotService.cs
git add apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Resources/GetDatabaseMetricsQueryHandler.cs
git add apps/api/src/Api/Program.cs
git add apps/api/tests/
git commit -m "feat(admin): add database growth tracking with daily snapshots"
```

---

## Phase 3: Honest UI for Unimplemented Features

### Task 17: Create EmptyFeatureState Component

**Files:**
- Create: `apps/web/src/components/admin/EmptyFeatureState.tsx`
- Test: `apps/web/__tests__/components/admin/EmptyFeatureState.test.tsx`

- [ ] **Step 1: Write component test**

```tsx
import { render, screen } from '@testing-library/react';
import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';

describe('EmptyFeatureState', () => {
  it('renders title and description', () => {
    render(<EmptyFeatureState title="Test Feature" description="Not available yet" />);
    expect(screen.getByText('Test Feature')).toBeInTheDocument();
    expect(screen.getByText('Not available yet')).toBeInTheDocument();
  });

  it('renders GitHub issue link when issueNumber provided', () => {
    render(<EmptyFeatureState title="Test" description="Desc" issueNumber={896} />);
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', expect.stringContaining('896'));
  });

  it('does not render link when no issueNumber', () => {
    render(<EmptyFeatureState title="Test" description="Desc" />);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — verify it fails**

Run: `cd apps/web && pnpm test -- EmptyFeatureState`

- [ ] **Step 3: Create EmptyFeatureState component**

```tsx
// File: apps/web/src/components/admin/EmptyFeatureState.tsx
import { Construction, type LucideIcon } from 'lucide-react';

interface EmptyFeatureStateProps {
  icon?: LucideIcon;
  title: string;
  description: string;
  issueNumber?: number;
  issueLabel?: string;
  className?: string;
}

export function EmptyFeatureState({
  icon: Icon = Construction,
  title,
  description,
  issueNumber,
  issueLabel,
  className,
}: EmptyFeatureStateProps) {
  return (
    <div className={`rounded-lg border border-dashed border-border/40 p-8 text-center ${className ?? ''}`}>
      <Icon className="mx-auto h-8 w-8 text-muted-foreground/50" />
      <p className="mt-3 text-sm font-medium text-foreground">{title}</p>
      <p className="mt-1 text-xs text-muted-foreground">{description}</p>
      {issueNumber && (
        <a
          href={`https://github.com/meepleAi-app/meepleai-monorepo/issues/${issueNumber}`}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-3 inline-block text-xs text-muted-foreground underline hover:text-foreground"
        >
          {issueLabel ?? `Tracked in issue #${issueNumber}`}
        </a>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test — verify it passes**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/EmptyFeatureState.tsx apps/web/__tests__/
git commit -m "feat(admin): add EmptyFeatureState component for unimplemented features"
```

---

### Task 18: Apply EmptyFeatureState to Unimplemented Pages (Batch 1: Config + Monitor)

**Files to modify:**
- `apps/web/src/app/admin/(dashboard)/config/GeneralTab.tsx`
- `apps/web/src/app/admin/(dashboard)/config/n8n/page.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/infrastructure/page.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/containers/page.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/services/page.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/BulkExportTab.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/AlertHistoryTab.tsx`

- [ ] **Step 1: Update GeneralTab.tsx**

Replace "Coming Soon" placeholder with:
```tsx
<EmptyFeatureState
  title="Impostazioni Generali"
  description="La configurazione delle impostazioni generali non è ancora disponibile."
/>
```

- [ ] **Step 2: Update config/n8n/page.tsx**

Replace content with EmptyFeatureState, issueNumber={60}.

- [ ] **Step 3: Update monitor pages (infrastructure, containers, services, logs)**

Each page gets its own EmptyFeatureState with appropriate title and issueNumber={896} where applicable.

- [ ] **Step 4: Update BulkExportTab.tsx**

Add EmptyFeatureState below the disabled export buttons (from Task 5).

- [ ] **Step 5: Update AlertHistoryTab.tsx**

Replace alert history content with EmptyFeatureState.

- [ ] **Step 6: Verify build**

Run: `cd apps/web && pnpm build`

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/admin/
git commit -m "feat(admin): apply EmptyFeatureState to config and monitor pages"
```

---

### Task 19: Apply EmptyFeatureState to Unimplemented Pages (Batch 2: Analytics + AI)

**Files to modify:**
- `apps/web/src/app/admin/(dashboard)/analytics/ReportsTab.tsx`
- Pages for Financial Ledger, Cost Calculator, Resource Forecast (if they exist)
- LLM Emergency controls section (within AI page)
- OpenRouter Usage section (within AI page)
- Testing Dashboards page

- [ ] **Step 1: Update ReportsTab.tsx**

Replace "Report generation coming soon" with:
```tsx
<EmptyFeatureState
  title="Generazione Report"
  description="La generazione automatica di report non è ancora disponibile."
  issueNumber={920}
/>
```

- [ ] **Step 2: Update remaining pages/sections**

For each unimplemented section, add EmptyFeatureState with the correct title, description (in Italian), and issueNumber from the spec table.

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/
git commit -m "feat(admin): apply EmptyFeatureState to analytics and AI pages"
```

---

### Task 20: Add 404 Error Handling Pattern

**Files to modify:** Multiple admin pages that call APIs which may return 404

- [ ] **Step 1: Identify pages making API calls to non-existent endpoints**

Search for `useQuery` calls in admin pages where the backend endpoint doesn't exist. These pages will currently show an error or blank state.

- [ ] **Step 2: Add error boundary pattern**

For each identified page, wrap the useQuery with the 404 fallback pattern:

```tsx
if (error?.status === 404) {
  return <EmptyFeatureState title="..." description="Endpoint non ancora disponibile" />;
}
```

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/
git commit -m "feat(admin): add 404 error handling with EmptyFeatureState fallback"
```

---

### Task 21: Final Verification

- [ ] **Step 1: Run all backend tests**

```bash
cd apps/api/src/Api && dotnet test
```

- [ ] **Step 2: Run all frontend tests**

```bash
cd apps/web && pnpm test
```

- [ ] **Step 3: Build both apps**

```bash
cd apps/api/src/Api && dotnet build
cd ../../../web && pnpm build
```

- [ ] **Step 4: Verify no TypeScript errors**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 5: Commit any final fixes**

---

## Summary

| Phase | Tasks | Key Deliverables |
|-------|-------|-----------------|
| **Phase 1** | Tasks 1-9 | Route fixes, mock removal, real DB queries |
| **Phase 2** | Tasks 10-16 | Config system, analytics endpoints, growth tracking |
| **Phase 3** | Tasks 17-21 | EmptyFeatureState component, ~15 page updates, 404 handling |

**Total tasks:** 21
**Estimated commits:** ~18
