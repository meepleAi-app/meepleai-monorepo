# #1861 BE — CatalogSyncRun aggregate + 4 endpoints

**Branch**: `feature/issue-1861-catalog-sync-run-be` (parent `main-dev`)
**Parent epic**: #1833 (F4 Ondata Ops)
**Blocks**: #1835 (F4-A6 FE re-skin)
**Effort estimate**: ~19-22h
**Archetype reference**: `MechanicRecalcJob` aggregate (`apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicRecalcJob.cs`) — mirror state machine, factory, reconstitute, RequireStatus pattern.

## Goal

Net-new aggregate `CatalogSyncRun` in BC `SharedGameCatalog` tracciante ogni esecuzione di catalog sync (BGG API auto / CSV import / Manual), con:
- State machine espliciti (`Queued → Running → Success | Failed | TimedOut`)
- Audit completo (StartedAt, CompletedAt, ItemsAdded/Updated/Failed, ErrorCode, ErrorDetail, TriggeredByUserId)
- 4 CQRS endpoints admin per consumo FE (`/status`, `/runs`, `/runs/{id}/logs`, `/trigger`)
- Background service hook esistente (`BggCatalogSyncService` o equivalente) emette eventi lifecycle

## Phase 1 — Domain (TDD, ~3h)

**Files**:
- `Domain/Enums/CatalogSyncProvider.cs` — `BggApi | CsvImport | Manual`
- `Domain/Enums/CatalogSyncStatus.cs` — `Queued | Running | Success | Failed | TimedOut`
- `Domain/Exceptions/InvalidCatalogSyncRunTransitionException.cs` — modellata su `InvalidMechanicRecalcJobTransitionException`
- `Domain/Aggregates/CatalogSyncRun.cs` — aggregate root con:
  - Properties: `Id, Provider, Status, Title, StartedAt, CompletedAt?, ItemsAdded, ItemsUpdated, ItemsFailed, ErrorCode?, ErrorDetail?, TriggeredByUserId?, LogTailJsonPath?`
  - Factory: `Enqueue(provider, title, triggeredBy)` → `Status=Queued`
  - Lifecycle: `MarkRunning() / RecordItemsAdded(n) / RecordItemsUpdated(n) / RecordItemsFailed(n) / Complete() / Fail(code, detail) / TimeOut(detail)`
  - `Reconstitute(...)` per repo hydration

**Tests** (`tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/CatalogSyncRunTests.cs`):
- `Enqueue_ValidArgs_ReturnsQueuedRun`
- `Enqueue_NullTitle_Throws`
- `MarkRunning_FromQueued_TransitionsToRunning_StampsStartedAt`
- `MarkRunning_FromRunning_Throws`
- `Complete_FromRunning_TransitionsToSuccess_StampsCompletedAt`
- `Complete_FromQueued_Throws`
- `Fail_FromRunning_Captures_ErrorCode_Detail_StampsCompletedAt`
- `Fail_FromTerminal_Throws`
- `TimeOut_FromRunning_TransitionsToTimedOut`
- `RecordItems*_FromRunning_Increment_Counters`
- `RecordItems*_FromTerminal_Throws`
- `Reconstitute_HydratesAllProperties_NoEventsRaised`

**Acceptance Phase 1**: 12+ unit tests verdi, no migration, no integration. Commit: `feat(catalog-sync-be): domain CatalogSyncRun aggregate + tests`.

## Phase 2 — Infrastructure: Migration + Repo (~3-4h)

**Files**:
- `Infrastructure/Entities/CatalogSyncRunEntity.cs` — EF entity mirror dell'aggregate (private setters mapped via `ICompiledModel` o convention)
- `Infrastructure/Configurations/CatalogSyncRunConfiguration.cs` — `IEntityTypeConfiguration<CatalogSyncRunEntity>`:
  - Table `catalog_sync_runs`
  - Indici: `IX_catalog_sync_runs_StartedAt DESC`, `IX_catalog_sync_runs_Status` (per filter `Running`)
  - `Provider`, `Status` mappati come `string` (enum-as-string per leggibilità SQL)
- `Domain/Repositories/ICatalogSyncRunRepository.cs`:
  ```csharp
  Task<CatalogSyncRun?> GetByIdAsync(Guid id, CancellationToken ct);
  Task<CatalogSyncRun?> GetCurrentRunningAsync(CancellationToken ct);
  Task<CatalogSyncRun?> GetLatestCompletedAsync(CancellationToken ct);
  Task<(IReadOnlyList<CatalogSyncRun> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct);
  Task<int> CountAllAsync(CancellationToken ct);
  Task AddAsync(CatalogSyncRun run, CancellationToken ct);
  Task UpdateAsync(CatalogSyncRun run, CancellationToken ct);
  ```
- `Infrastructure/Repositories/CatalogSyncRunRepository.cs` — EF impl con `ToDomain()` / `ToEntity()` mappers
- `Migrations/<timestamp>_Add_CatalogSyncRun.cs` — `dotnet ef migrations add Add_CatalogSyncRun`
- DI registration in `Program.cs` o module-level `IServiceCollection` extension

**Tests** (`tests/Api.Tests/Integration/SharedGameCatalog/CatalogSyncRunRepositoryTests.cs`, Testcontainers PG):
- `AddAsync_PersistsAggregate_RoundTripsCorrectly`
- `GetCurrentRunningAsync_ReturnsRunWithRunningStatus`
- `GetCurrentRunningAsync_NoRunningRun_ReturnsNull`
- `GetLatestCompletedAsync_OrdersByStartedAtDesc`
- `GetPagedAsync_Page1_PageSize12_Returns12Items_TotalAccurate`
- `GetPagedAsync_PaginationConsistency_AcrossPages`

**Acceptance Phase 2**: 6+ integration tests verdi, migration SQL reviewed (no destructive ops). Commit: `feat(catalog-sync-be): entity + repo + migration Add_CatalogSyncRun`.

## Phase 3 — Application: 4 CQRS handler (~5h)

**Files** (`Application/Queries/...` e `Application/Commands/...`):

### 3.1 GetCatalogSyncStatusQuery → `GET /status`
- Result DTO: `CatalogSyncStatusResult { Status: "idle" | "running" | "never_run", LastRun?: SummaryDto, CurrentRun?: SummaryDto, Cumulative: { GamesTotal: int }, NextScheduled?: DateTimeOffset, Provider: "BggApi" }`
- Handler: query `GetCurrentRunningAsync` + `GetLatestCompletedAsync` + count `SharedGames` (gamesTotal) + read cron config (next scheduled)

### 3.2 GetCatalogSyncRunsQuery(page, pageSize) → `GET /runs`
- Result DTO: `PagedRunsResult { Items: [RunListItemDto], Total: int, Page: int, PageSize: int, HasMore: bool }`
- RunListItemDto: `{ Id, Provider, Status, Title, StartedAt, Duration, ItemsAdded, ItemsUpdated, ItemsFailed, ErrorCode?, TriggeredByUserId? }`

### 3.3 GetCatalogSyncRunLogsQuery(runId, tail) → `GET /runs/{id}/logs`
- Result DTO: `RunLogsResult { RunId, ErrorCode?, ErrorDetail?, Logs: [string] | [] , LogsAvailable: bool }`
- Handler: load run, se `LogTailJsonPath != null` legge file/blob tail-N, altrimenti restituisce `LogsAvailable=false`. 404 se run inesistente.

### 3.4 TriggerCatalogSyncCommand(provider, triggeredBy) → `POST /trigger`
- Validator FluentValidation: `provider != null`, `triggeredBy != Guid.Empty`
- Handler: check `GetCurrentRunningAsync` → se non null, throw `ConflictException("Sync already running", currentRunId)`. Altrimenti `CatalogSyncRun.Enqueue(...)` → `AddAsync` → enqueue background job tramite `IBackgroundJobClient.Enqueue` (Hangfire) o `IBgCatalogSyncTrigger` interface.
- Audit event `CatalogSyncTriggered` Level=2 via `IMediator.Publish(new CatalogSyncTriggeredEvent(runId, provider, triggeredBy))` (subscriber esistente Administration BC scrive audit row).
- Result DTO: `TriggerResult { RunId, Status: "queued" }`

**Tests** (`tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/`):
- 4 handler unit tests con mock repos
- Validator unit tests (4 scenari edge)
- 2 integration tests handler-driven per scenari E (pagination 30→12) e C (conflict 409)

**Acceptance Phase 3**: 10+ handler tests verdi, DTOs strict-mapped. Commit: `feat(catalog-sync-be): 4 CQRS handlers + DTOs + validators`.

## Phase 4 — Endpoints (~2h)

**File**: `apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs` — estendi mappa esistente:

```csharp
// GET /api/v1/admin/catalog-ingestion/status
group.MapGet("/status", async (HttpContext ctx, IMediator m, CancellationToken ct) => {
    var (ok, _, err) = ctx.RequireAdminSession();
    if (!ok) return err!;
    return Results.Ok(await m.Send(new GetCatalogSyncStatusQuery(), ct));
}).WithName("GetCatalogSyncStatus");

// GET /api/v1/admin/catalog-ingestion/runs?page=N&pageSize=M
// GET /api/v1/admin/catalog-ingestion/runs/{id}/logs?tail=N
// POST /api/v1/admin/catalog-ingestion/trigger
```

- Rate limit `BulkImportAdmin` su `/trigger` (riusa policy esistente)
- OpenAPI summary + descriptions per ogni endpoint
- 409 Conflict mapping per `ConflictException` (verifica middleware globale o handler manuale)

**Tests** (`tests/Api.Tests/Integration/Routing/AdminCatalogIngestionEndpointsTests.cs`):
- Endpoint-level smoke per ognuno dei 4 nuovi handler (200/404/409/202)
- Unauth → 401
- Non-admin → 403

**Acceptance Phase 4**: 8+ endpoint integration tests verdi. Commit: `feat(catalog-sync-be): endpoints + rate limit + openapi`.

## Phase 5 — Background service hook (~3-4h, DECISIONE PENDING)

**Pre-check 2026-06-03**: esiste `BggImportQueueBackgroundService` (Issue #3541) ma è **queue processor continuo** (1 req/sec, retry, stale recovery), NON cron "ogni 6h" come da mockup. Modello "run discreta" semanticamente disallineato.

**Opzione scelta**: (a) Cron wrap — schedula chiamata periodica `enqueue-all-skeletons` ogni 6h via `IHostedService` o Hangfire recurring, ogni esecuzione = 1 `CatalogSyncRun` aggregata che traccia items enqueued + completati.

**Files**:
- `Infrastructure/BackgroundServices/CatalogSyncCronService.cs` — `BackgroundService` con `PeriodicTimer(6h)`, dipende da `ICatalogSyncRunRepository` + `IMediator`
- Lifecycle: `CatalogSyncRun.Enqueue(BggApi, "BGG cron sync") → MarkRunning → send enqueue-all-skeletons command → poll queue stats → Complete` o `Fail`
- Configurazione: `CatalogSync:CronEnabled=true`, `CatalogSync:IntervalHours=6` in appsettings

**Out of scope per #1861**: hook su `BggImportQueueBackgroundService` per legare i single-item BGG processing alla run "parent" (richiede refactor del queue service). Spin sub-issue follow-up se serve granularità per-item nel `LogStream`.

**Tests**: integration con DB reale (Testcontainers) — cron timer mockato, simula 1 tick → run creata + items count corretti tramite snapshot del queue stat.

**Acceptance Phase 5**: cron service registrato in DI, run auto-creata ogni N ore (test con interval=1s in dev), 2-3 integration tests verdi. Commit: `feat(catalog-sync-be): cron service Phase 5 wraps queue processor`.

**Tests**: integration con DB reale (Testcontainers) — `TriggerSync → simula 5 items added → Complete → /runs ritorna run con counters corretti`.

**Acceptance Phase 5**: 2-3 end-to-end integration tests verdi. Commit: `feat(catalog-sync-be): background service hook BGG sync lifecycle emit`.

## Phase 6 — Documentation + cleanup (~1h)

- README BC `SharedGameCatalog` aggiornato con nuova aggregate
- API doc Scalar auto-generata via OpenAPI summaries
- Update CLAUDE.md sezione **DDD Bounded Contexts** se necessario (no, count rimane 18)
- Plan archive: `docs/superpowers/plans/archive/2026-06-03-issue-1861-catalog-sync-run-be.md` (al merge)

## Risks / Open questions

1. **Cron service esistenza**: Phase 5 dipende. Se BGG cron service non esiste, ridotto scope (Phase 1-4 sufficienti per FE #1835 che usa `/trigger` manualmente; cron schedule rimane TODO).
2. **LogTailJsonPath storage**: file system locale vs S3/R2 blob? Per #1861 default = file system locale `data/catalog-sync-logs/{runId}.log`, follow-up issue per migrazione blob.
3. **Cumulative `GamesTotal`**: count `shared_games` con filter `IsDeleted=false`? Conferma in Phase 3.1 handler.
4. **`NextScheduled`**: se Phase 5 deferred, Phase 3.1 ritorna `null` per `nextScheduled`. FE #1835 deve gestire gracefully.

## Verification gates

- Phase 1: `dotnet test --filter "FullyQualifiedName~CatalogSyncRunTests"` → green
- Phase 2: `dotnet test --filter "FullyQualifiedName~CatalogSyncRunRepository"` → green; `dotnet ef migrations script` SQL review OK
- Phase 3: `dotnet test --filter "FullyQualifiedName~CatalogSync"` → all green
- Phase 4: full integration suite green; `curl localhost:8080/scalar/v1` mostra nuovi endpoint
- Phase 5: smoke test manuale `make dev` → POST `/trigger` → status=running observabile via `/status` polling
- Phase 6: `dotnet test` full suite green, no regressions baseline (zero known-flaky aggiunti)

## PR strategy

Single PR a `main-dev`, base `feature/issue-1861-catalog-sync-run-be`. Commits structured per phase (6 commits semantici). Code review via `/code-review:code-review` skill pre-merge per audit pattern adherence.

## Memory checkpoints

- Post-Phase 1: write_memory("issue-1861-phase1-done", "domain aggregate + 12 unit tests")
- Post-Phase 3: write_memory("issue-1861-handlers-done", "4 CQRS + validators")
- Post-merge: update MEMORY.md "Active Projects" — sposta da WIP a "Executed Plans"

---

🤖 Auto-generated 2026-06-03 via writing-plans workflow.
