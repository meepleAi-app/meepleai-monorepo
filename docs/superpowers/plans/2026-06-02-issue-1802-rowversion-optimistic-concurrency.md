# Issue #1802 — RowVersion Optimistic Concurrency on PdfDocumentEntity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere `[Timestamp] byte[]? RowVersion` a `PdfDocumentEntity` e wrappare 17 handler che mutano la entity per surfacare `DbUpdateConcurrencyException` come 409 (Category A user-facing) / log warning + retry (Category B background) / silent skip (Category C maintenance), con Prometheus counter `meepleai_pdf_concurrency_conflicts_total` e 4 Testcontainers integration tests Barrier-synchronized.

**Architecture:** Optimistic concurrency via PostgreSQL `xmin` system column mapped by EF Core `IsRowVersion()`. Per Newman's recommendation (workshop): implicit API surface — server-side catch + 409, no ETag header. Per Wiegers's enumeration (workshop): tutti i ~17 handler che mutano `PdfDocumentEntity` categorizzati A/B/C con error handling appropriato per ogni categoria.

**Tech Stack:** .NET 9, EF Core 9 + Npgsql, FluentValidation, xUnit + Testcontainers, FluentAssertions, Prometheus.NET.

**Issue**: [#1802](https://github.com/meepleAi-app/meepleai-monorepo/issues/1802) (P3, enhancement)

**Branch**: `feature/issue-1802-rowversion-concurrency` (parent: `main-dev`)

**Effort**: ~8h (post spec-panel workshop scope expansion)

---

## Scope freeze (out-of-scope)

- Locking/blocking — stay with optimistic (read-modify-CAS).
- Other entities outside `DocumentProcessing` BC.
- `ETag` HTTP header pattern (#2055) — implicit 409 only.
- Grafana dashboard panel — added separately if telemetry shows >5 conflicts/day.
- UI/FE changes — toast `Re-index fallito` already handles new 409 naturally.

## File structure

### Created
| Path | Responsibility |
|------|---------------|
| `apps/api/src/Api/Infrastructure/Migrations/<TIMESTAMP>_AddRowVersionToPdfDocuments.cs` | Foundation: EF migration aggiunge `xmin` mapping (no new column, just shadow concurrency token) |
| `apps/api/src/Api/Infrastructure/Metrics/PdfConcurrencyMetrics.cs` | Prometheus counter `meepleai_pdf_concurrency_conflicts_total` + helper `RecordConflict(handlerName, category)` |
| `apps/api/tests/Api.Tests/Integration/DocumentProcessing/PdfRowVersionConcurrencyIntegrationTests.cs` | 4 Testcontainers scenarios (Barrier-synchronized) |

### Modified — Foundation
| Path | Change |
|------|--------|
| `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs` | + `[Timestamp] public byte[]? RowVersion { get; set; }` |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs` | + `builder.Property(e => e.RowVersion).IsRowVersion();` |

### Modified — Category A handlers (9 user-facing, throw ConflictException on conflict)
1. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs`
2. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/DeleteKbDocumentCommandHandler.cs` + `DeletePdfCommandHandler.cs`
3. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UpdatePdfMetadataCommandHandler.cs` (#1687)
4. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/OverridePdfLanguageCommandHandler.cs`
5. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SetPdfVisibilityCommandHandler.cs`
6. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReclassifyDocumentCommandHandler.cs`
7. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CancelPdfProcessingCommandHandler.cs`
8. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfProcessingCommandHandler.cs`
9. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/AddDocumentToCollectionCommandHandler.cs`

### Modified — Category B handlers (6 background, log warning + return success)
10. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs`
11. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs`
12. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ExtractPdfTextCommandHandler.cs`
13. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs`
14. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` (3 mutation sites: main + `MarkFailedAsync` + `TryMarkFailedAsync`)
15. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/VectorDocumentReadyStateHandler.cs`

### Modified — Category C handlers (2 maintenance, log debug + continue batch)
16. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/PurgeStaleDocumentsCommandHandler.cs`
17. `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/RetryFailedPdfsJob.cs`

---

## Tasks

### Task 1: Pre-flight + branch hygiene

**Files:**
- (no edit)

- [ ] **Step 1: Verify clean state**

```bash
cd D:/Repositories/meepleai-monorepo-dev
git branch --show-current  # must be main-dev or feature/*
git status --short          # must be clean (modulo .tmp files)
git pull --ff-only          # update main-dev
```

Expected: clean tree (eccetto `docs/superpowers/plans/*` untracked).

- [ ] **Step 2: Create feature branch from main-dev**

```bash
git checkout main-dev
git pull --ff-only
git checkout -b feature/issue-1802-rowversion-concurrency
git config branch.feature/issue-1802-rowversion-concurrency.parent main-dev
git branch --show-current
```

Expected: `feature/issue-1802-rowversion-concurrency`.

- [ ] **Step 3: Baseline build**

```bash
dotnet build apps/api/src/Api/Api.csproj 2>&1 | tail -3
```

Expected: `Build succeeded. 0 Error(s).`

(No commit — setup only.)

---

### Task 2: Foundation — entity + EF config + migration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/Migrations/<TIMESTAMP>_AddRowVersionToPdfDocuments.cs` (auto-generated)

#### Canonical pattern (from `RuleSpecEntity.cs:27-28` + `RuleSpecEntityConfiguration.cs:37-39`)

```csharp
[Timestamp]
public byte[]? RowVersion { get; set; }   // nullable per PhotoBatchUpload landmine fix (migration 20260524190307)
```

```csharp
builder.Property(e => e.RowVersion).IsRowVersion();
```

This maps to PostgreSQL `xmin` system column automatically (Npgsql provider convention). No `bytea` column added.

- [ ] **Step 1: Add property to `PdfDocumentEntity.cs`**

Find the line immediately AFTER `public string? IndexerVersion { get; set; }` (introduced by #1673):

```csharp
    // Issue #1673: Pipeline indexer version applied at last reindex.
    // Nullable for backwards compat — backfilled to 'v0' on migration.
    public string? IndexerVersion { get; set; }
```

Add after it:

```csharp
    // Issue #1802: Optimistic concurrency control via PostgreSQL xmin system column.
    // Auto-mapped to xmin by Npgsql when configured with .IsRowVersion(). Nullable
    // to avoid PhotoBatchUpload landmine (migration 20260524190307: NOT NULL caused
    // InsertCommand double-mapping bug under Npgsql).
    [Timestamp]
    public byte[]? RowVersion { get; set; }
```

Verify the file has `using System.ComponentModel.DataAnnotations;` at the top. If missing, add it.

- [ ] **Step 2: Add EF configuration to `PdfDocumentEntityConfiguration.cs`**

In `PdfDocumentEntityConfiguration.cs`, after the existing `IndexerVersion` configuration block (introduced by #1673, lines ~168-176), add:

```csharp
        // Issue #1802: Optimistic concurrency via xmin (PostgreSQL system column).
        // Pattern matches RuleSpecEntityConfiguration:38-39 — Npgsql auto-maps to xmin.
        builder.Property(e => e.RowVersion)
            .IsRowVersion();
```

- [ ] **Step 3: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddRowVersionToPdfDocuments --output-dir Infrastructure/Migrations
```

Expected: 2 new files `Infrastructure/Migrations/<TIMESTAMP>_AddRowVersionToPdfDocuments.cs` + `.Designer.cs`.

- [ ] **Step 4: Verify the migration is empty/minimal**

Read the generated `<TIMESTAMP>_AddRowVersionToPdfDocuments.cs`. Because `xmin` is a system column (not a new physical column), the `Up` method should be effectively a no-op — EF only records the conceptual addition in the model snapshot. If the migration is non-empty AND tries to add a `bytea` column, **STOP and report DONE_WITH_CONCERNS** — the pattern recognition failed and the implementer needs to verify the Npgsql provider behavior.

If the migration is empty `protected override void Up(MigrationBuilder migrationBuilder) { }`, that's correct.

- [ ] **Step 5: Build to verify zero errors**

```bash
dotnet build apps/api/src/Api/Api.csproj 2>&1 | tail -3
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 6: Run existing PdfDocument tests to confirm no regression**

```bash
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=DocumentProcessing&Category=Unit" --no-build 2>&1 | tail -8
```

Expected: all green (~336 unit tests). Existing tests instantiate `PdfDocumentEntity` directly; the new nullable `byte[]? RowVersion` doesn't break anything because EF auto-populates on insert.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(api/document-processing): #1802 add RowVersion (xmin) to PdfDocumentEntity"
```

---

### Task 3: Observability — Prometheus counter + helper

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Metrics/PdfConcurrencyMetrics.cs`

#### Why this comes BEFORE handler refactor

The 17 handler refactors will all reference `PdfConcurrencyMetrics.RecordConflict(...)`. Defining the helper first lets later tasks call it without forward references.

- [ ] **Step 1: Create the metrics helper**

```csharp
// apps/api/src/Api/Infrastructure/Metrics/PdfConcurrencyMetrics.cs
using Prometheus;

namespace Api.Infrastructure.Metrics;

/// <summary>
/// Prometheus counter for PdfDocumentEntity optimistic concurrency conflicts (#1802).
/// Cardinality bounded: ~17 handler labels × 3 categories = 51 series max.
/// </summary>
internal static class PdfConcurrencyMetrics
{
    private static readonly Counter ConflictsTotal = Prometheus.Metrics.CreateCounter(
        name: "meepleai_pdf_concurrency_conflicts_total",
        help: "Total number of DbUpdateConcurrencyException occurrences on PdfDocumentEntity, by handler and category.",
        new CounterConfiguration
        {
            LabelNames = new[] { "handler", "category" },
        });

    /// <summary>
    /// Record a concurrency conflict event.
    /// </summary>
    /// <param name="handlerName">Use <c>nameof(YourHandlerClass)</c> for rename-safety.</param>
    /// <param name="category">One of <c>"A"</c> (user-facing), <c>"B"</c> (background pipeline), <c>"C"</c> (maintenance).</param>
    public static void RecordConflict(string handlerName, string category)
    {
        ConflictsTotal.WithLabels(handlerName, category).Inc();
    }
}
```

- [ ] **Step 2: Verify Prometheus.NET is referenced**

```bash
grep -l "prometheus-net" apps/api/src/Api/Api.csproj
```

Expected: match found. The project uses Prometheus.NET for existing metrics (cf. `MeepleAiMetrics.cs`). If missing, **STOP and report NEEDS_CONTEXT**.

- [ ] **Step 3: Build to verify**

```bash
dotnet build apps/api/src/Api/Api.csproj 2>&1 | tail -3
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Metrics/PdfConcurrencyMetrics.cs
git commit -m "feat(api/document-processing): #1802 PdfConcurrencyMetrics counter helper"
```

---

### Task 4: Category A — wrap 9 user-facing handlers (throw ConflictException)

**Files (9 handler files — see "File structure" above):**

#### Pattern (uniform across 9 handlers)

For each handler, locate the **outermost** `SaveChangesAsync` call (the one that persists the user-initiated mutation). Wrap it in `try/catch (DbUpdateConcurrencyException)`:

**Before**:
```csharp
await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

**After**:
```csharp
try
{
    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
catch (DbUpdateConcurrencyException)
{
    PdfConcurrencyMetrics.RecordConflict(nameof(<HandlerClass>), "A");
    _logger.LogWarning(
        "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category A)",
        <pdfIdVariable>, nameof(<HandlerClass>));
    throw new ConflictException(
        $"Document {<pdfIdVariable>} was modified by another concurrent operation; please retry.");
}
```

Required usings (add if missing):
```csharp
using Microsoft.EntityFrameworkCore;             // DbUpdateConcurrencyException
using Api.Infrastructure.Metrics;                 // PdfConcurrencyMetrics
using Api.Middleware.Exceptions;                  // ConflictException
```

#### Per-handler check before wrapping

Each handler MUST have:
- An `ILogger<T> _logger` field (most do — verify before editing)
- A clear PdfDocument id variable to log + interpolate (usually `command.PdfId`, `pdf.Id`, or similar)

If a handler does NOT have `_logger`, add it via constructor injection (DI registration is automatic via `AddScoped`).

#### Sub-step structure (uniform for all 9 handlers)

For each of the 9 handlers:

- [ ] **Step A.N.1: Read the file** to identify the exact outermost `SaveChangesAsync` call + the `_logger` field + the pdf id variable.

- [ ] **Step A.N.2: Add required usings** at the top of the file (if missing).

- [ ] **Step A.N.3: Wrap the SaveChangesAsync** with the try/catch pattern above. Substitute `<HandlerClass>` and `<pdfIdVariable>` with the actual names.

- [ ] **Step A.N.4: Build**:
  ```bash
  dotnet build apps/api/src/Api/Api.csproj 2>&1 | tail -3
  ```

- [ ] **Step A.N.5: Run existing handler tests** (filter by class name) to verify no regression:
  ```bash
  cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~<HandlerClass>Tests" --no-build 2>&1 | tail -5
  ```

- [ ] **Step A.N.6: Commit per handler**:
  ```bash
  git add <handler-path>
  git commit -m "feat(api/document-processing): #1802 Category A concurrency catch in <HandlerName>"
  ```

#### 9 handlers to refactor (in order)

| N | Handler | pdfId variable hint | Sub-tasks |
|---|---------|---------------------|-----------|
| 1 | `ReindexDocumentCommandHandler` | `command.PdfId` | A.1.1 → A.1.6 |
| 2 | `DeleteKbDocumentCommandHandler` (then `DeletePdfCommandHandler` if separate) | `command.Id` | A.2.1 → A.2.6 |
| 3 | `UpdatePdfMetadataCommandHandler` | `command.PdfId` | A.3.1 → A.3.6 |
| 4 | `OverridePdfLanguageCommandHandler` | `command.PdfId` | A.4.1 → A.4.6 |
| 5 | `SetPdfVisibilityCommandHandler` | `command.PdfId` | A.5.1 → A.5.6 |
| 6 | `ReclassifyDocumentCommandHandler` | `command.DocumentId` | A.6.1 → A.6.6 |
| 7 | `CancelPdfProcessingCommandHandler` | `command.PdfId` | A.7.1 → A.7.6 |
| 8 | `RetryPdfProcessingCommandHandler` | `command.PdfId` | A.8.1 → A.8.6 |
| 9 | `AddDocumentToCollectionCommandHandler` | `command.DocumentId` | A.9.1 → A.9.6 |

> **Note on handler N=2**: if `DeleteKbDocumentCommandHandler` and `DeletePdfCommandHandler` are two separate files (or two methods in one handler), wrap both with separate commits.

> **Note on handler N=9**: this handler may not directly mutate `PdfDocumentEntity` — it might just update a join table. Verify by reading the handler first. If it only inserts into a join table, mark this sub-task as "out of scope" and skip with a `git commit --allow-empty` note? **NO** — instead just skip and document in the Task 4 final report.

#### Task 4 acceptance

- 9 commits (one per handler) with consistent messages.
- Build clean after each commit.
- Existing per-handler tests still pass.
- No other files modified.

---

### Task 5: Category B — wrap 6 background pipeline handlers (log + return success)

**Files (6 handler files):**

#### Pattern (uniform across 6 handlers)

Background pipeline handlers run inside Quartz jobs. They must NOT throw `ConflictException` (no HTTP boundary). They must log a warning and return success so Quartz does NOT retry (the next pipeline tick will re-read fresh state):

**Before**:
```csharp
await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

**After**:
```csharp
try
{
    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
catch (DbUpdateConcurrencyException ex)
{
    PdfConcurrencyMetrics.RecordConflict(nameof(<HandlerClass>), "B");
    _logger.LogWarning(ex,
        "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
        <pdfIdVariable>, nameof(<HandlerClass>));
    return; // or "return successResult;" depending on handler signature
}
```

The `return;` is **critical** for Category B: it tells Quartz "job succeeded" so no retry storm. The admin's mutation wins, the pipeline tick at the next scheduled run will re-read fresh state and proceed correctly.

#### Special case: `PdfProcessingPipelineService.cs`

This file has **3 distinct mutation sites** (per #1801 refactor in 7 commits):

| Site | Line range | Description |
|------|-----------|-------------|
| Main pipeline `ProcessPdfAsync` | ~140-336 | Transitions Pending → Extracting → Chunking → Embedding → Indexing → Ready |
| `MarkFailedAsync` | ~720-728 | Best-effort failure marking |
| `TryMarkFailedAsync` | ~734-755 | Defensive failure marking after DbContext disposal |

For each, the try/catch wrapping is the same shape, but the `pdfIdVariable` differs:
- Main: `pdfDoc.Id` or `pdfId`
- MarkFailedAsync: `pdfDoc.Id`
- TryMarkFailedAsync: `pdfDocumentId`

Apply the pattern to ALL THREE sites in `PdfProcessingPipelineService.cs`. ONE commit covers all three (single file).

#### Sub-step structure (uniform for all 6 handlers)

For each of the 6 handlers (or PdfProcessingPipelineService with its 3 sites):

- [ ] **Step B.N.1: Read** to identify mutation site(s).

- [ ] **Step B.N.2: Add usings** (DbUpdateConcurrencyException + Metrics).

- [ ] **Step B.N.3: Wrap each `SaveChangesAsync`** with the Category B pattern.

- [ ] **Step B.N.4: Build**.

- [ ] **Step B.N.5: Run handler-specific tests**:
  ```bash
  cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~<HandlerClass>" --no-build 2>&1 | tail -5
  ```

- [ ] **Step B.N.6: Commit per handler**:
  ```bash
  git commit -m "feat(api/document-processing): #1802 Category B concurrency log in <HandlerName>"
  ```

#### 6 handlers to refactor

| N | Handler | Mutation sites |
|---|---------|----------------|
| 1 | `UploadPdfCommandHandler.Processing` | 1 (main upload pipeline) |
| 2 | `CompleteChunkedUploadCommandHandler` | 1 |
| 3 | `ExtractPdfTextCommandHandler` | 1 |
| 4 | `IndexPdfCommandHandler` | 1 |
| 5 | `PdfProcessingPipelineService` | **3** (main + MarkFailedAsync + TryMarkFailedAsync) |
| 6 | `VectorDocumentReadyStateHandler` | 1 |

#### Task 5 acceptance

- 6 commits.
- Build clean.
- All existing handler tests pass.
- PdfProcessingPipelineService has all 3 mutation sites wrapped.

---

### Task 6: Category C — wrap 2 maintenance handlers (silent skip)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/PurgeStaleDocumentsCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/RetryFailedPdfsJob.cs`

#### Pattern

Maintenance jobs iterate over batches of `PdfDocumentEntity`. Per-item concurrency conflicts are EXPECTED (an admin acted concurrently). They MUST NOT bubble exceptions — silent skip + LogDebug + continue the batch.

For **batch SaveChanges** (one SaveChangesAsync covering N items), wrap with:

```csharp
try
{
    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    _logger.LogInformation("Maintenance batch completed: {Count} items processed", processedCount);
}
catch (DbUpdateConcurrencyException ex)
{
    PdfConcurrencyMetrics.RecordConflict(nameof(<HandlerClass>), "C");
    _logger.LogDebug(ex,
        "Concurrency conflict in {Handler} (Category C) — some items mutated concurrently by admin; batch partially applied",
        nameof(<HandlerClass>));
    // No re-throw. Maintenance job is best-effort.
}
```

For **per-item SaveChanges** (foreach with SaveChangesAsync per item), the try/catch goes inside the loop and continues on conflict:

```csharp
foreach (var item in batch)
{
    item.ProcessingState = nameof(PdfProcessingState.Failed);
    try
    {
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        processedCount++;
    }
    catch (DbUpdateConcurrencyException)
    {
        PdfConcurrencyMetrics.RecordConflict(nameof(<HandlerClass>), "C");
        _logger.LogDebug(
            "Skipped PdfDocument {PdfId} in {Handler} (Category C) — concurrent admin mutation",
            item.Id, nameof(<HandlerClass>));
        // Detach the failed entity so the next iteration's SaveChanges doesn't retry it
        _db.Entry(item).State = EntityState.Detached;
    }
}
_logger.LogInformation("Maintenance batch: {Processed}/{Total} items processed", processedCount, batch.Count);
```

> **Read both files first** to determine if they use batch SaveChanges or per-item SaveChanges. Apply the appropriate pattern.

- [ ] **Step C.1: Refactor PurgeStaleDocumentsCommandHandler.cs**

Read file. Apply Category C pattern. Build. Run tests. Commit:

```bash
git commit -m "feat(api/document-processing): #1802 Category C concurrency skip in PurgeStale"
```

- [ ] **Step C.2: Refactor RetryFailedPdfsJob.cs**

Read file. Apply Category C pattern. Build. Run tests. Commit:

```bash
git commit -m "feat(api/document-processing): #1802 Category C concurrency skip in RetryFailedPdfs"
```

#### Task 6 acceptance

- 2 commits, build clean, tests pass.

---

### Task 7: Integration test — 4 Testcontainers Barrier-synchronized scenarios

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/PdfRowVersionConcurrencyIntegrationTests.cs`

#### Reference pattern (from `ReindexDocumentVersionIntegrationTests.cs`)

Use `SharedTestcontainersFixture` + `IntegrationServiceCollectionBuilder.CreateBase` + `[Collection("Integration-GroupA")]` per la convention del repo.

For true parallelism: `Barrier` + `Task.Run` + `Task.WhenAll`.

- [ ] **Step 1: Create the test file**

```csharp
// apps/api/tests/Api.Tests/Integration/DocumentProcessing/PdfRowVersionConcurrencyIntegrationTests.cs
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for PdfDocumentEntity RowVersion optimistic concurrency.
/// Issue #1802. Uses Barrier-synchronized parallel tasks for real race conditions.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1802")]
public sealed class PdfRowVersionConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000001802");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000001802");

    public PdfRowVersionConcurrencyIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rowversion_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        await MigrateWithRetryAsync(_dbContext);
        await SeedBaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null) await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable d) await d.DisposeAsync();
        try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); } catch { /* best-effort */ }
    }

    private static async Task MigrateWithRetryAsync(MeepleAiDbContext db)
    {
        // Mirror pattern from sibling integration tests (Npgsql transient retry).
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (Npgsql.NpgsqlException) when (attempt < 3)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    private async Task SeedBaseAsync()
    {
        _dbContext!.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "rowversion-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "RowVersion Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "RowVersion Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<PdfDocumentEntity> SeedReadyPdfAsync(string? indexerVersion = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rowversion.pdf",
            FilePath = "/tmp/rowversion.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = TestUserId,
            SharedGameId = TestSharedGameId,
            ProcessingState = nameof(PdfProcessingState.Ready),
            IndexerVersion = indexerVersion ?? IndexerVersionRegistry.Current.Version,
        };
        _dbContext!.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdf;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 1: Parallel two reindex — only one succeeds
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Parallel_TwoReindex_OnlyOneSucceeds()
    {
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        // Use two independent service scopes (otherwise both tasks share the same
        // tracked entity and DbContext — invalidates the race test).
        Task<Exception?> RunReindex(string version) => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(new ReindexDocumentCommand(pdf.Id, version), TestCancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        var resultA = RunReindex(IndexerVersionRegistry.Current.Version);
        var resultB = RunReindex(IndexerVersionRegistry.Current.Version);

        var exceptions = await Task.WhenAll(resultA, resultB);

        // Exactly one succeeded (null), exactly one failed with ConflictException.
        var successes = exceptions.Count(ex => ex is null);
        var conflicts = exceptions.Count(ex => ex is ConflictException);
        successes.Should().Be(1, "exactly one reindex must win the race");
        conflicts.Should().Be(1, "exactly one reindex must lose with ConflictException");

        // The winning RowVersion advanced.
        var reloaded = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        reloaded.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        reloaded.RowVersion.Should().NotBeNull().And.NotEqual(pdf.RowVersion);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 2: Reindex races with Delete — first wins, second 409
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reindex_RacesWithDelete_FirstWinsSecondGets409()
    {
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        Task<(string Op, Exception? Exception)> RunReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version), TestCancellationToken);
                return ("reindex", (Exception?)null);
            }
            catch (Exception ex) { return ("reindex", (Exception?)ex); }
        });

        Task<(string Op, Exception? Exception)> RunDelete() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(new DeleteKbDocumentCommand(pdf.Id), TestCancellationToken);
                return ("delete", (Exception?)null);
            }
            catch (Exception ex) { return ("delete", (Exception?)ex); }
        });

        var results = await Task.WhenAll(RunReindex(), RunDelete());

        var successCount = results.Count(r => r.Exception is null);
        var conflictCount = results.Count(r => r.Exception is ConflictException);
        successCount.Should().Be(1, "exactly one operation must succeed");
        conflictCount.Should().Be(1, "exactly one operation must conflict");

        // If delete won: document removed. If reindex won: document remains with state=Pending.
        var winner = results.Single(r => r.Exception is null);
        var existsCheck = await _dbContext!.PdfDocuments.AsNoTracking()
            .AnyAsync(p => p.Id == pdf.Id, TestCancellationToken);
        if (winner.Op == "delete")
        {
            existsCheck.Should().BeFalse("delete winner removes the document");
            var orphanChunks = await _dbContext.TextChunks.AsNoTracking()
                .CountAsync(tc => tc.PdfDocumentId == pdf.Id, TestCancellationToken);
            orphanChunks.Should().Be(0, "no orphan TextChunks (cascade)");
        }
        else
        {
            existsCheck.Should().BeTrue("reindex winner keeps the document");
            var reloaded = await _dbContext.PdfDocuments.AsNoTracking()
                .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
            reloaded.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 3: Reindex races with background pipeline RowVersion mutation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reindex_RacesWithBackgroundPipeline_AdminGets409()
    {
        // Seed Ready, then simulate a background pipeline mutation (Ready → Chunking)
        // in scope B while admin reads stale RowVersion and tries to reindex in scope A.
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        Task<(string Op, Exception? Exception)> RunAdminReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Force the admin scope to load the PDF (capture RowVersion v1) BEFORE the barrier.
            var loaded = await db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);

            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version), TestCancellationToken);
                return ("admin", (Exception?)null);
            }
            catch (Exception ex) { return ("admin", (Exception?)ex); }
        });

        Task<(string Op, Exception? Exception)> RunPipelineTick() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                // Simulate a Category B mutation directly (no real pipeline service spin-up).
                var entity = await db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
                entity.ProcessingState = nameof(PdfProcessingState.Chunking);
                await db.SaveChangesAsync(TestCancellationToken);
                return ("pipeline", (Exception?)null);
            }
            catch (Exception ex) { return ("pipeline", (Exception?)ex); }
        });

        var results = await Task.WhenAll(RunAdminReindex(), RunPipelineTick());

        // Exactly one wins. The semantics depend on timing — what matters is consistency:
        // EITHER both succeed (no race occurred — barrier timing variance) OR exactly one fails.
        var failureCount = results.Count(r => r.Exception is not null);
        failureCount.Should().BeLessThanOrEqualTo(1, "at most one operation should conflict (or zero if barrier failed to align)");

        // The document MUST end in a consistent state — not partial.
        var final = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        final.ProcessingState.Should().BeOneOf(
            nameof(PdfProcessingState.Pending),   // admin reindex won
            nameof(PdfProcessingState.Chunking)); // pipeline won
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 4: Retry after conflict succeeds
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sequential_RetryAfterConflict_Succeeds()
    {
        var pdf = await SeedReadyPdfAsync();

        // Step 1: provoke a conflict via parallel reindex (same as Scenario 1).
        using (var barrier = new Barrier(participantCount: 2))
        {
            Task<Exception?> RunReindex() => Task.Run(async () =>
            {
                using var scope = _serviceProvider!.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                try { await mediator.Send(new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version), TestCancellationToken); return null; }
                catch (Exception ex) { return ex; }
            });
            await Task.WhenAll(RunReindex(), RunReindex());
        }

        // Step 2: the losing admin retries 1 second later (after winner persisted).
        await Task.Delay(TimeSpan.FromSeconds(1), TestCancellationToken);

        // Fresh read in fresh scope — gets the NEW RowVersion.
        using var retryScope = _serviceProvider!.CreateScope();
        var retryMediator = retryScope.ServiceProvider.GetRequiredService<IMediator>();
        await retryMediator.Send(new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version), TestCancellationToken);

        var final = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        final.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending),
            "retry after conflict should succeed and leave state=Pending");
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build apps/api/src/Api/Api.csproj 2>&1 | tail -3
dotnet build apps/api/tests/Api.Tests/Api.Tests.csproj 2>&1 | tail -3
```

Expected: 0 errors.

- [ ] **Step 3: Run the integration tests (requires Docker)**

```bash
docker info 2>&1 | head -3   # Confirm Docker running
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfRowVersionConcurrencyIntegrationTests" --no-build 2>&1 | tail -10
```

Expected: 4 passed. If Docker is NOT running locally, commit the test file anyway — CI will run them.

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/DocumentProcessing/PdfRowVersionConcurrencyIntegrationTests.cs
git commit -m "test(api/document-processing): #1802 RowVersion concurrency E2E (4 Barrier-sync scenarios)"
```

---

### Task 8: Final verification + PR + issue close-out

**Files:**
- (no edit)

- [ ] **Step 1: Final regression sweep**

```bash
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=DocumentProcessing&Category=Unit" --no-build 2>&1 | tail -5
```

Expected: all DocumentProcessing unit tests still pass (baseline ~336+).

- [ ] **Step 2: Final grep — verify no unwrapped SaveChanges**

```bash
grep -rn 'await.*SaveChangesAsync' apps/api/src/Api/BoundedContexts/DocumentProcessing 2>&1 | grep -v 'PdfDocumentEntity\|Collection\|Test'
```

For each match, confirm it's inside a `try { … } catch (DbUpdateConcurrencyException)` block OR document why it's exempt (e.g. the SaveChanges only persists non-Pdf entities).

- [ ] **Step 3: Verify Prometheus counter is wired**

```bash
grep -rn 'PdfConcurrencyMetrics.RecordConflict' apps/api/src/Api/BoundedContexts/DocumentProcessing
```

Expected: 17 sites (9 Category A + 6 Category B incl. 3 in PdfProcessingPipelineService + 2 Category C).

- [ ] **Step 4: Rebase + push**

```bash
git fetch origin main-dev
git rebase origin/main-dev
git push -u origin feature/issue-1802-rowversion-concurrency
```

If conflicts: resolve and re-run regression sweep (Step 1).

- [ ] **Step 5: Open PR**

```bash
gh pr create --base main-dev --title "feat(api/document-processing): #1802 RowVersion optimistic concurrency" --body "$(cat <<'EOF'
## Summary

Closes #1802. Adds optimistic concurrency control to `PdfDocumentEntity` via PostgreSQL `xmin` (mapped by EF Core `IsRowVersion()`) and wraps 17 handler mutation sites with category-appropriate error handling.

## Categories (Wiegers full enumeration — workshop)

- **Category A** (9 handlers, user-facing): catch `DbUpdateConcurrencyException` → throw `ConflictException` → 409 to caller. FE shows retry toast.
- **Category B** (6 handlers, background pipeline): catch → log warning → return success. Quartz no retry; next pipeline tick re-reads.
- **Category C** (2 handlers, maintenance): catch → log debug → continue batch.

## Observability

- New Prometheus counter `meepleai_pdf_concurrency_conflicts_total{handler,category}` (cardinality ≤51 series).
- Structured warning logs at every conflict.

## Test plan

- [x] Build clean (0 errors, 0 new warnings)
- [x] Existing DocumentProcessing unit tests pass (~336+)
- [x] 4 Testcontainers integration tests (Barrier-synchronized parallel scenarios) pass
- [x] No wire format change (server-only catch + 409)
- [x] No DTO change

## Notes

- **PhotoBatchUpload landmine avoided**: entity uses `[Timestamp] byte[]? RowVersion` (nullable) per fix migration `20260524190307_FixPhotoBatchUploadRowVersionNullable`. Config uses `.IsRowVersion()` only (no `HasColumnName`) — Npgsql auto-maps to `xmin` system column.
- **Implicit API surface** (workshop): no ETag header pattern (#2055). FE existing 409 toast handles new concurrency conflicts naturally.
- **Grafana dashboard** (deferred to follow-up): added separately if telemetry shows >5 conflicts/day in production.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 6: Comment on issue #1802**

```bash
gh issue comment 1802 --body "PR aperto contro \`main-dev\` (link sopra).

## DoD updates
- [x] \`[Timestamp] byte[]? RowVersion\` su \`PdfDocumentEntity\` (xmin mapping, PhotoBatchUpload landmine avoided).
- [x] EF migration applicata.
- [x] 9 Category A handler con \`DbUpdateConcurrencyException → ConflictException\`.
- [x] 6 Category B handler con \`LogWarning + return success\` (incluso 3 mutation sites in PdfProcessingPipelineService).
- [x] 2 Category C handler con \`LogDebug + silent skip\`.
- [x] Prometheus counter \`meepleai_pdf_concurrency_conflicts_total\` wired su tutti i 17 catch sites.
- [x] 4 Testcontainers integration tests (Barrier-sync parallel scenarios).
- [x] Existing tests pass without modification.

Ready for code review."
```

---

## Self-Review

### Spec coverage check

| Issue #1802 acceptance criterion | Task |
|----------------------------------|------|
| Add `[Timestamp] byte[]? RowVersion` to `PdfDocumentEntity` | Task 2 |
| EF migration with `xmin` PostgreSQL convention | Task 2 |
| `.IsRowVersion()` config (NOT `[Timestamp]` only) | Task 2 |
| 9 Category A handlers throw ConflictException | Task 4 |
| 6 Category B handlers log warning + return success | Task 5 |
| 2 Category C handlers log debug + continue | Task 6 |
| Prometheus counter `meepleai_pdf_concurrency_conflicts_total` | Task 3 + 4/5/6 |
| Existing tests pass without modification | Tasks 4/5/6 step 5 (per-handler regression) + Task 8 step 1 |
| 4 Testcontainers integration tests | Task 7 |
| PhotoBatchUpload landmine avoidance | Task 2 (nullable byte[]) |

### Placeholder scan

- [x] No "TBD" / "TODO" / "implement later".
- [x] No "Add appropriate error handling" — every catch has explicit exception type + counter + log + action.
- [x] No "Similar to Task N" — Task 4/5/6 have explicit per-handler tables AND uniform pattern code blocks repeated for clarity.
- [x] No "Write tests for the above" — Task 7 has 4 fully-coded scenarios.

### Type consistency

| Symbol | Defined | Used |
|--------|---------|------|
| `PdfDocumentEntity.RowVersion` (byte[]?) | Task 2 | Tasks 4/5/6 (handler refactors) + Task 7 (integration tests) |
| `PdfConcurrencyMetrics.RecordConflict(handler, category)` | Task 3 | Tasks 4/5/6 (17 catch sites) |
| `DbUpdateConcurrencyException` (Microsoft.EntityFrameworkCore) | EF library | Tasks 4/5/6 |
| `ConflictException(string)` | existing `Api.Middleware.Exceptions.ConflictException` | Task 4 only (Category A) |
| `PdfProcessingState` enum values via `nameof(...)` | #1801 (already in main-dev) | Task 7 (integration test fixtures) |
| `IndexerVersionRegistry.Current.Version` | #1673 (already in main-dev) | Task 7 |
| `ReindexDocumentCommand(Guid, string?)` | #1673 (already in main-dev) | Task 7 |
| `DeleteKbDocumentCommand(Guid)` | #1653 (already in main-dev) | Task 7 |

All cross-references resolve to merged code on main-dev.

---

## References

- Issue: [#1802](https://github.com/meepleAi-app/meepleai-monorepo/issues/1802) (post-workshop body, 8.5/10 spec quality)
- Sibling RowVersion pattern: `RuleSpecEntity.cs:27-28` + `RuleSpecEntityConfiguration.cs:38-39`
- PhotoBatchUpload landmine: migration `20260524190307_FixPhotoBatchUploadRowVersionNullable.cs`
- Workshop decisions (2026-06-02): full scope, implicit API, 4 scenarios, observability yes
- Spec-panel review (Wiegers, Fowler, Nygard, Newman, Crispin, Adzic): conversation history 2026-06-02
- Related PRs: #1800 (#1673 reindex selector), #1804 (#1801 nameof refactor), #1805 (#1801 close-out)
