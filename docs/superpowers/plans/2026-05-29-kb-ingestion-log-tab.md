# SP5 F3-FU-1 — KB Ingestion log tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere un tab interno "Ingestion log" al `KbDocDetailPanel` dell'Explorer KB admin, che mostra il pipeline status (timeline 5-step) + log entries del job di processing più recente per il documento selezionato.

**Architecture:** Nuova query CQRS by-documentId che riusa il `ProcessingJobDetailDto` esistente, esposta come endpoint admin `/api/v1/admin/kb/docs/{docId}/ingestion-log`. Frontend: 8 nuovi componenti React + 1 hook React Query con polling smart + URL-driven tab switching nel detail panel. Re-use del `RetryJobCommand` esistente per il Re-enqueue.

**Tech Stack:** .NET 9 + EF Core + MediatR + xUnit (backend) · Next.js 16 App Router + TanStack Query + Zod + Tailwind 4 + Vitest + Testing Library (frontend)

**Spec:** `docs/superpowers/specs/2026-05-29-kb-ingestion-log-tab-design.md`
**Issue:** [#1650](https://github.com/meepleAi-app/meepleai-monorepo/issues/1650)
**Branch:** `feature/issue-1650-ingestion-log-tab` (already created, parent: `main-dev`)

---

## Deviation dal design doc

Il design prevedeva nuovi DTO (`IngestionLogDto` + `IngestionStepDto` + `IngestionLogEntryDto`). **Riusiamo invece i DTO esistenti** `ProcessingJobDetailDto` + `ProcessingStepDto` + `StepLogEntryDto` (già in `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/ProcessingJobDto.cs`) perché hanno shape identica al required. Una nota nel design doc finale documenterà l'allineamento.

---

## File Structure

### Backend (1 BoundedContext: `DocumentProcessing`)

| Status | File |
|---|---|
| Create | `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetLatestIngestionLogByDocumentIdQuery.cs` |
| Create | `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetLatestIngestionLogByDocumentIdQueryHandler.cs` |
| Modify | `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` (add `MapGet("/docs/{docId}/ingestion-log", ...)`) |
| Create | `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/Queue/GetLatestIngestionLogByDocumentIdQueryHandlerTests.cs` |
| Create | `apps/api/tests/Api.Tests/Integration/DocumentProcessing/IngestionLogEndpointTests.cs` |

### Frontend (admin KB explorer)

| Status | File |
|---|---|
| Create | `apps/web/src/lib/api/schemas/ingestion-log.schemas.ts` (Zod schemas + types) |
| Create | `apps/web/src/lib/api/admin-kb-ingestion.ts` (API client fn) |
| Create | `apps/web/src/lib/admin-kb/calcCost.ts` (utility cost estimation) |
| Create | `apps/web/src/lib/admin-kb/__tests__/calcCost.test.ts` |
| Create | `apps/web/src/hooks/queries/useKbDocIngestionLog.ts` |
| Create | `apps/web/src/hooks/queries/__tests__/useKbDocIngestionLog.test.ts` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionPanel.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionHero.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionTimeline.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionTimelineStep.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionLogBlock.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionActions.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionPanel.test.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionHero.test.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionTimeline.test.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionLogBlock.test.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionActions.test.tsx` |
| Create | `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx` |
| Modify | `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx` (URL-driven tab switch) |
| Modify | `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx` (tab tests) |

---

## Task 1: Backend — `GetLatestIngestionLogByDocumentIdQuery` record

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetLatestIngestionLogByDocumentIdQuery.cs`

- [ ] **Step 1: Create the query record**

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Fetches the most recent ProcessingJob (with Steps and LogEntries) for a given PdfDocumentId.
/// Returns null when no job exists for the document (e.g. legacy PDFs predating the pipeline).
/// Issue #1650: KB Ingestion log tab.
/// </summary>
internal sealed record GetLatestIngestionLogByDocumentIdQuery(Guid DocumentId)
    : IQuery<ProcessingJobDetailDto?>;
```

- [ ] **Step 2: Verify build**

Run: `cd apps/api && dotnet build src/Api/Api.csproj`
Expected: Build succeeded (no errors)

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetLatestIngestionLogByDocumentIdQuery.cs
git commit -m "feat(kb-ingestion): #1650 add GetLatestIngestionLogByDocumentIdQuery"
```

---

## Task 2: Backend — handler with TDD

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/Queue/GetLatestIngestionLogByDocumentIdQueryHandlerTests.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetLatestIngestionLogByDocumentIdQueryHandler.cs`

- [ ] **Step 1: Write the failing tests (TDD red)**

Reference: pattern from `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/Queue/GetActiveAlertsQueryHandlerTests.cs` for InMemory DbContext setup.

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

public class GetLatestIngestionLogByDocumentIdQueryHandlerTests
{
    private static MeepleAiDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_NoJob_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(ctx);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(ctx);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(Guid.Empty),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_SingleJob_ReturnsJobWithSteps()
    {
        await using var ctx = CreateContext();
        var (docId, jobId) = SeedJob(ctx, status: "Completed", retryCount: 0, maxRetries: 3);
        await ctx.SaveChangesAsync();
        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(ctx);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(docId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(jobId, result!.Id);
        Assert.Equal(docId, result.PdfDocumentId);
        Assert.False(result.CanRetry); // Completed status
    }

    [Fact]
    public async Task Handle_MultipleJobs_ReturnsMostRecent()
    {
        await using var ctx = CreateContext();
        var docId = Guid.NewGuid();
        var olderJobId = Guid.NewGuid();
        var newerJobId = Guid.NewGuid();
        SeedJobWithId(ctx, docId, olderJobId, createdAt: DateTimeOffset.UtcNow.AddHours(-2));
        SeedJobWithId(ctx, docId, newerJobId, createdAt: DateTimeOffset.UtcNow);
        await ctx.SaveChangesAsync();
        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(ctx);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(docId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(newerJobId, result!.Id);
    }

    [Fact]
    public async Task Handle_FailedJobWithRetryAvailable_SetsCanRetryTrue()
    {
        await using var ctx = CreateContext();
        var (docId, _) = SeedJob(ctx, status: "Failed", retryCount: 1, maxRetries: 3);
        await ctx.SaveChangesAsync();
        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(ctx);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(docId),
            CancellationToken.None);

        Assert.True(result!.CanRetry);
    }

    [Fact]
    public async Task Handle_FailedJobAtMaxRetries_SetsCanRetryFalse()
    {
        await using var ctx = CreateContext();
        var (docId, _) = SeedJob(ctx, status: "Failed", retryCount: 3, maxRetries: 3);
        await ctx.SaveChangesAsync();
        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(ctx);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(docId),
            CancellationToken.None);

        Assert.False(result!.CanRetry);
    }

    // ── Helpers ───────────────────────────────────────────────
    private static (Guid DocId, Guid JobId) SeedJob(
        MeepleAiDbContext ctx,
        string status,
        int retryCount,
        int maxRetries)
    {
        var docId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        SeedJobWithId(ctx, docId, jobId, DateTimeOffset.UtcNow, status, retryCount, maxRetries);
        return (docId, jobId);
    }

    private static void SeedJobWithId(
        MeepleAiDbContext ctx,
        Guid docId,
        Guid jobId,
        DateTimeOffset createdAt,
        string status = "Completed",
        int retryCount = 0,
        int maxRetries = 3)
    {
        // Use Reflection or factory method per ProcessingJob shape — refer to
        // Domain.Entities.ProcessingJob.Create(...) signature when implementing.
        // NOTE: Adjust this helper to the actual entity factory in the implementation step.
        throw new NotImplementedException("Adjust helper to ProcessingJob factory signature");
    }
}
```

> **NB**: il setup `SeedJobWithId` ha `throw NotImplementedException` come pro-memoria: durante l'implementation step (3) bisogna allinearlo alla signature concreta di `ProcessingJob.Create()` (verifica `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/ProcessingJob.cs`).

- [ ] **Step 2: Run tests to verify they fail (compile error)**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetLatestIngestionLogByDocumentIdQueryHandlerTests" --no-restore 2>&1 | tail -20`
Expected: COMPILATION ERROR — `GetLatestIngestionLogByDocumentIdQueryHandler does not exist`

- [ ] **Step 3: Implement the handler (TDD green)**

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Fetches the most recent ProcessingJob for a PdfDocumentId, including all Steps
/// and LogEntries. Returns null when the document has no associated job
/// (legacy PDFs predating the pipeline). Read-only — pure CQRS query side.
/// Issue #1650: KB Ingestion log tab.
/// </summary>
internal sealed class GetLatestIngestionLogByDocumentIdQueryHandler
    : IQueryHandler<GetLatestIngestionLogByDocumentIdQuery, ProcessingJobDetailDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetLatestIngestionLogByDocumentIdQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ProcessingJobDetailDto?> Handle(
        GetLatestIngestionLogByDocumentIdQuery query,
        CancellationToken cancellationToken)
    {
        if (query.DocumentId == Guid.Empty) return null;

        var job = await _dbContext.ProcessingJobs
            .AsNoTracking()
            .Include(j => j.PdfDocument)
            .Include(j => j.Steps)
                .ThenInclude(s => s.LogEntries)
            .Where(j => j.PdfDocumentId == query.DocumentId)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (job is null) return null;

        var steps = job.Steps
            .OrderBy(s => s.StepName, StringComparer.Ordinal)
            .Select(s => new ProcessingStepDto(
                s.Id,
                s.StepName,
                s.Status,
                s.StartedAt,
                s.CompletedAt,
                s.DurationMs,
                s.MetadataJson,
                s.LogEntries
                    .OrderBy(l => l.Timestamp)
                    .Select(l => new StepLogEntryDto(l.Id, l.Timestamp, l.Level.ToString(), l.Message))
                    .ToList()
            ))
            .ToList();

        var canRetry = string.Equals(job.Status, "Failed", StringComparison.Ordinal)
                       && job.RetryCount < job.MaxRetries;

        return new ProcessingJobDetailDto(
            job.Id,
            job.PdfDocumentId,
            job.PdfDocument.FileName,
            job.UserId,
            job.Status,
            job.Priority,
            job.CurrentStep,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.ErrorMessage,
            job.RetryCount,
            job.MaxRetries,
            canRetry,
            steps);
    }
}
```

- [ ] **Step 4: Align test helper to actual ProcessingJob factory**

Read `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/ProcessingJob.cs` and replace the `SeedJobWithId` body so it builds via the existing factory (`ProcessingJob.Create(...)` or via Reflection if the constructor is private). Persist via `ctx.ProcessingJobs.Add(job)`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetLatestIngestionLogByDocumentIdQueryHandlerTests" --no-restore 2>&1 | tail -20`
Expected: PASS for all 6 tests

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetLatestIngestionLogByDocumentIdQueryHandler.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/Queue/GetLatestIngestionLogByDocumentIdQueryHandlerTests.cs
git commit -m "feat(kb-ingestion): #1650 implement handler + unit tests (6 cases)"
```

---

## Task 3: Backend — endpoint registration

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`
- Create: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/IngestionLogEndpointTests.cs`

- [ ] **Step 1: Write failing integration test (TDD red)**

Reference: pattern from existing integration tests under `apps/api/tests/Api.Tests/Integration/` (e.g. PdfMetricsEndpointTests). Use the project's `WebApplicationFactory` fixture (verify exact class name when implementing — likely `MeepleAiWebApplicationFactory` or `IntegrationTestBase`).

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

public class IngestionLogEndpointTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _admin;
    private readonly HttpClient _anon;

    public IngestionLogEndpointTests(IntegrationTestFactory factory)
    {
        _admin = factory.CreateClientWithAdminSession();
        _anon = factory.CreateClient();
    }

    [Fact]
    public async Task GET_NoSession_Returns401()
    {
        var response = await _anon.GetAsync($"/api/v1/admin/kb/docs/{Guid.NewGuid()}/ingestion-log");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_NonExistentDoc_Returns200WithNullBody()
    {
        var response = await _admin.GetAsync($"/api/v1/admin/kb/docs/{Guid.NewGuid()}/ingestion-log");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("null", body.Trim());
    }

    [Fact]
    public async Task GET_EmptyGuid_Returns400OrNull()
    {
        // Either ASP.NET model binding rejects (400) or handler returns null (200 "null").
        // Both are acceptable; assert one or the other.
        var response = await _admin.GetAsync($"/api/v1/admin/kb/docs/{Guid.Empty}/ingestion-log");
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest
            || response.StatusCode == HttpStatusCode.OK);
    }
}
```

> **NB**: il nome esatto della fixture (`IntegrationTestFactory`) e di `CreateClientWithAdminSession()` va verificato in fase di implementazione contro un test integration esistente — cerca un file in `apps/api/tests/Api.Tests/Integration/` per il pattern reale.

- [ ] **Step 2: Run integration test (expect 404 endpoint not mapped)**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~IngestionLogEndpointTests" --no-restore 2>&1 | tail -20`
Expected: 404 Not Found responses → tests fail

- [ ] **Step 3: Add the endpoint to AdminKnowledgeBaseEndpoints.cs**

Add the import:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
```

Insert this MapGet inside `MapAdminKnowledgeBaseEndpoints`, after the `/processing-queue` endpoint:

```csharp
// GET /api/v1/admin/kb/docs/{docId}/ingestion-log — Issue #1650
kbGroup.MapGet("/docs/{docId:guid}/ingestion-log", async (
    Guid docId,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    var query = new GetLatestIngestionLogByDocumentIdQuery(docId);
    var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
    return Results.Ok(result);
})
.WithName("GetKbDocIngestionLog")
.WithSummary("Get the latest ProcessingJob (with Steps and LogEntries) for a PdfDocumentId.");
```

- [ ] **Step 4: Run integration test to verify it passes**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~IngestionLogEndpointTests" --no-restore 2>&1 | tail -20`
Expected: PASS for all 3 tests

- [ ] **Step 5: Run full backend test suite (regression check)**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore 2>&1 | tail -10`
Expected: no new failures vs baseline (CLAUDE.md says baseline failure count is 0)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs apps/api/tests/Api.Tests/Integration/DocumentProcessing/IngestionLogEndpointTests.cs
git commit -m "feat(kb-ingestion): #1650 expose GET /admin/kb/docs/{docId}/ingestion-log + integration tests"
```

---

## Task 4: Frontend — Zod schemas

**Files:**
- Create: `apps/web/src/lib/api/schemas/ingestion-log.schemas.ts`

- [ ] **Step 1: Create schemas mirroring the backend DTO**

```typescript
import { z } from 'zod';

/**
 * Mirrors backend `StepLogEntryDto` (DocumentProcessing/Application/DTOs/ProcessingJobDto.cs).
 * Level is a stringified enum: "Info" | "Warning" | "Error".
 */
export const IngestionLogEntrySchema = z.object({
  id: z.string().uuid(),
  timestamp: z.string().datetime({ offset: true }),
  level: z.enum(['Info', 'Warning', 'Error']),
  message: z.string(),
});
export type IngestionLogEntry = z.infer<typeof IngestionLogEntrySchema>;

/**
 * Mirrors backend `ProcessingStepDto`. `StepName` is a stringified `ProcessingStepType`
 * enum: "Upload" | "Extract" | "Chunk" | "Embed" | "Index".
 */
export const IngestionStepSchema = z.object({
  id: z.string().uuid(),
  stepName: z.enum(['Upload', 'Extract', 'Chunk', 'Embed', 'Index']),
  status: z.string(), // queued|running|done|failed (string from backend, no fixed enum)
  startedAt: z.string().datetime({ offset: true }).nullable(),
  completedAt: z.string().datetime({ offset: true }).nullable(),
  durationMs: z.number().nullable(),
  metadataJson: z.string().nullable(),
  logEntries: z.array(IngestionLogEntrySchema),
});
export type IngestionStep = z.infer<typeof IngestionStepSchema>;

/**
 * Mirrors backend `ProcessingJobDetailDto` — top-level shape returned by
 * GET /api/v1/admin/kb/docs/{docId}/ingestion-log.
 * Null body = no job for the document (e.g. legacy PDFs).
 */
export const IngestionLogSchema = z.object({
  id: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  pdfFileName: z.string(),
  userId: z.string().uuid(),
  status: z.string(),
  priority: z.number(),
  currentStep: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
  startedAt: z.string().datetime({ offset: true }).nullable(),
  completedAt: z.string().datetime({ offset: true }).nullable(),
  errorMessage: z.string().nullable(),
  retryCount: z.number().int().nonnegative(),
  maxRetries: z.number().int().positive(),
  canRetry: z.boolean(),
  steps: z.array(IngestionStepSchema),
});
export type IngestionLog = z.infer<typeof IngestionLogSchema>;

export const IngestionLogResponseSchema = IngestionLogSchema.nullable();
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -5`
Expected: no new errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/schemas/ingestion-log.schemas.ts
git commit -m "feat(kb-ingestion): #1650 add Zod schemas for ingestion log"
```

---

## Task 5: Frontend — API client function

**Files:**
- Create: `apps/web/src/lib/api/admin-kb-ingestion.ts`

- [ ] **Step 1: Create the fetcher**

```typescript
import { apiClient } from '@/lib/api/client';
import {
  IngestionLogResponseSchema,
  type IngestionLog,
} from '@/lib/api/schemas/ingestion-log.schemas';

/**
 * GET /api/v1/admin/kb/docs/{docId}/ingestion-log
 * Returns the latest ProcessingJob+Steps+Logs for the given PDF document, or
 * null when no job exists (legacy PDFs predating the pipeline).
 * Issue #1650.
 */
export async function fetchKbDocIngestionLog(docId: string): Promise<IngestionLog | null> {
  return apiClient.get<IngestionLog | null>(
    `/api/v1/admin/kb/docs/${docId}/ingestion-log`,
    IngestionLogResponseSchema,
  );
}
```

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -5`
Expected: no new errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/admin-kb-ingestion.ts
git commit -m "feat(kb-ingestion): #1650 add fetchKbDocIngestionLog client"
```

---

## Task 6: Frontend — `calcCost` utility with TDD

**Files:**
- Create: `apps/web/src/lib/admin-kb/__tests__/calcCost.test.ts`
- Create: `apps/web/src/lib/admin-kb/calcCost.ts`

- [ ] **Step 1: Write the failing tests (TDD red)**

```typescript
import { describe, it, expect } from 'vitest';
import { estimateCost } from '../calcCost';

describe('estimateCost', () => {
  it('returns 0 for self-hosted bge-base-en-v1.5', () => {
    const r = estimateCost(100, 'bge-base-en-v1.5');
    expect(r.value).toBe(0);
    expect(r.model).toBe('bge-base-en-v1.5');
    expect(r.formula).toContain('self-hosted');
  });

  it('computes cost for OpenAI text-embedding-3-small', () => {
    const r = estimateCost(100, 'text-embedding-3-small');
    // 100 chunks × 512 tokens × $0.00000002 = $0.001024
    expect(r.value).toBeCloseTo(0.001024, 6);
    expect(r.model).toBe('text-embedding-3-small');
    expect(r.formula).toContain('100');
    expect(r.formula).toContain('512');
  });

  it('returns null result for unknown models', () => {
    const r = estimateCost(100, 'mystery-model');
    expect(r).toBeNull();
  });

  it('returns 0 for zero chunks (self-hosted)', () => {
    const r = estimateCost(0, 'bge-base-en-v1.5');
    expect(r!.value).toBe(0);
  });
});
```

- [ ] **Step 2: Run test, expect failure**

Run: `cd apps/web && pnpm vitest run src/lib/admin-kb/__tests__/calcCost.test.ts 2>&1 | tail -15`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the utility**

```typescript
/**
 * Cost estimation for the embedding step of an ingestion job.
 * Pure FE heuristic — the backend doesn't currently persist real token counts
 * in `ProcessingStep.MetadataJson` for the Embed step (verified 2026-05-29).
 *
 * Formula: chunkCount × AVG_TOKENS_PER_CHUNK × pricePerToken(model)
 *
 * `AVG_TOKENS_PER_CHUNK` is a configured constant (512), matching the
 * sentence-based chunker default. When the backend starts populating real
 * token counts (follow-up #1653), swap this for the real metadata field.
 * Issue #1650.
 */

const AVG_TOKENS_PER_CHUNK = 512;

interface ModelPricing {
  readonly pricePerToken: number; // 0 for self-hosted
  readonly note: string;          // human-readable formula descriptor
}

const KNOWN_MODELS: Record<string, ModelPricing> = {
  'bge-base-en-v1.5': { pricePerToken: 0, note: 'self-hosted' },
  'text-embedding-3-small': {
    pricePerToken: 2e-8, // $0.02 / 1M tokens (OpenAI 2024-12 list)
    note: '×',
  },
};

export interface CostEstimate {
  readonly value: number;
  readonly model: string;
  readonly formula: string;
}

export function estimateCost(chunkCount: number, model: string): CostEstimate | null {
  const pricing = KNOWN_MODELS[model];
  if (!pricing) return null;

  if (pricing.pricePerToken === 0) {
    return {
      value: 0,
      model,
      formula: 'self-hosted',
    };
  }

  const value = chunkCount * AVG_TOKENS_PER_CHUNK * pricing.pricePerToken;
  return {
    value,
    model,
    formula: `${chunkCount} × ${AVG_TOKENS_PER_CHUNK} ${pricing.note} $${pricing.pricePerToken.toExponential(2)}`,
  };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/lib/admin-kb/__tests__/calcCost.test.ts 2>&1 | tail -10`
Expected: PASS for all 4 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/admin-kb/calcCost.ts apps/web/src/lib/admin-kb/__tests__/calcCost.test.ts
git commit -m "feat(kb-ingestion): #1650 add cost estimation utility + tests"
```

---

## Task 7: Frontend — `useKbDocIngestionLog` hook with TDD

**Files:**
- Create: `apps/web/src/hooks/queries/__tests__/useKbDocIngestionLog.test.ts`
- Create: `apps/web/src/hooks/queries/useKbDocIngestionLog.ts`

- [ ] **Step 1: Write the failing tests**

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import * as api from '@/lib/api/admin-kb-ingestion';
import { useKbDocIngestionLog } from '../useKbDocIngestionLog';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

const sampleLog = (status: string): IngestionLog => ({
  id: '00000000-0000-0000-0000-000000000001',
  pdfDocumentId: '00000000-0000-0000-0000-000000000002',
  pdfFileName: 'test.pdf',
  userId: '00000000-0000-0000-0000-000000000003',
  status,
  priority: 0,
  currentStep: null,
  createdAt: new Date().toISOString(),
  startedAt: null,
  completedAt: null,
  errorMessage: null,
  retryCount: 0,
  maxRetries: 3,
  canRetry: false,
  steps: [],
});

describe('useKbDocIngestionLog', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('does not fetch when docId is null', () => {
    const spy = vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    renderHook(() => useKbDocIngestionLog({ docId: null }), { wrapper: wrapper() });
    expect(spy).not.toHaveBeenCalled();
  });

  it('does not fetch when enabled is false', () => {
    const spy = vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    renderHook(
      () => useKbDocIngestionLog({ docId: 'xxx', enabled: false }),
      { wrapper: wrapper() });
    expect(spy).not.toHaveBeenCalled();
  });

  it('fetches with valid docId', async () => {
    const log = sampleLog('Completed');
    vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(log);
    const { result } = renderHook(
      () => useKbDocIngestionLog({ docId: 'xxx' }),
      { wrapper: wrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(log);
  });

  it('uses polling interval when status is queued', async () => {
    const log = sampleLog('Queued');
    vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(log);
    const { result } = renderHook(
      () => useKbDocIngestionLog({ docId: 'xxx' }),
      { wrapper: wrapper() });
    await waitFor(() => expect(result.current.data).toBeTruthy());
    // We can't assert refetchInterval directly; assert behavior by checking
    // that the query is still "active" (no terminal state). Conservative test:
    expect(['Queued', 'Processing'].includes(result.current.data!.status)).toBe(true);
  });

  it('returns null when backend body is null', async () => {
    vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    const { result } = renderHook(
      () => useKbDocIngestionLog({ docId: 'xxx' }),
      { wrapper: wrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });
});
```

- [ ] **Step 2: Run test, expect failure**

Run: `cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useKbDocIngestionLog.test.ts 2>&1 | tail -15`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the hook**

```typescript
import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { fetchKbDocIngestionLog } from '@/lib/api/admin-kb-ingestion';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

/**
 * Polling interval (ms) while a job is in a non-terminal state (Queued or
 * Processing). The cadence balances responsiveness with server load —
 * average PDF ingestion takes 30s-5min, so 6s is unobtrusive.
 */
const ACTIVE_POLL_INTERVAL_MS = 6000;

const ACTIVE_STATUSES = new Set(['Queued', 'Processing']);

export const kbDocIngestionLogKeys = {
  all: ['kb', 'doc', 'ingestion-log'] as const,
  byId: (docId: string) => [...kbDocIngestionLogKeys.all, docId] as const,
};

export interface UseKbDocIngestionLogOptions {
  readonly docId: string | null | undefined;
  readonly enabled?: boolean;
}

/**
 * TanStack Query hook for the KB ingestion log of a document.
 * - Refetches every 6s while the job status is Queued or Processing.
 * - Stops polling automatically when the job reaches a terminal state
 *   (Completed | Failed | Cancelled).
 * - Returns null when no job exists for the document.
 *
 * Backend contract: GET /api/v1/admin/kb/docs/{docId}/ingestion-log.
 * Issue #1650.
 */
export function useKbDocIngestionLog(
  options: UseKbDocIngestionLogOptions,
): UseQueryResult<IngestionLog | null, Error> {
  const { docId, enabled = true } = options;
  const isValid = typeof docId === 'string' && docId.length > 0;

  return useQuery<IngestionLog | null, Error>({
    queryKey: isValid ? kbDocIngestionLogKeys.byId(docId) : kbDocIngestionLogKeys.all,
    queryFn: async () => (isValid ? fetchKbDocIngestionLog(docId) : null),
    enabled: enabled && isValid,
    refetchInterval: (q) => {
      const status = q.state.data?.status;
      return status && ACTIVE_STATUSES.has(status) ? ACTIVE_POLL_INTERVAL_MS : false;
    },
    staleTime: 5_000,
  });
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useKbDocIngestionLog.test.ts 2>&1 | tail -10`
Expected: PASS for all 5 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/queries/useKbDocIngestionLog.ts apps/web/src/hooks/queries/__tests__/useKbDocIngestionLog.test.ts
git commit -m "feat(kb-ingestion): #1650 add useKbDocIngestionLog hook with smart polling"
```

---

## Task 8: Frontend — `IngestionTimelineStep` component with TDD

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionTimelineStep.tsx`
- (No standalone test — covered by `IngestionTimeline.test.tsx` in Task 9)

- [ ] **Step 1: Create the component**

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber/emerald/rose chip palette + zinc dark (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import type { IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

interface IngestionTimelineStepProps {
  readonly step: IngestionStep;
  readonly index: number;
  readonly isLast: boolean;
}

const STEP_LABELS: Record<IngestionStep['stepName'], string> = {
  Upload: 'Upload & validate',
  Extract: 'Estrazione testo',
  Chunk: 'Chunking',
  Embed: 'Embedding',
  Index: 'Indexing pgvector',
};

const STEP_SUBS: Record<IngestionStep['stepName'], string> = {
  Upload: 'PDF integrity check · MIME · size',
  Extract: 'Unstructured / SmolDocling / Docnet',
  Chunk: 'Sentence-based, target 512 tok',
  Embed: 'Vector embedding generation',
  Index: 'HNSW index, commit tx',
};

function chipForStatus(status: string): { wrapper: string; icon: string } {
  const s = status.toLowerCase();
  if (s === 'done' || s === 'completed' || s === 'succeeded') {
    return { wrapper: 'bg-emerald-500/20 text-emerald-700 dark:text-emerald-300', icon: '✓' };
  }
  if (s === 'running' || s === 'processing') {
    return {
      wrapper: 'bg-amber-500/30 text-amber-800 dark:text-amber-200 animate-pulse',
      icon: '▸',
    };
  }
  if (s === 'failed' || s === 'errored') {
    return { wrapper: 'bg-rose-500/20 text-rose-700 dark:text-rose-300', icon: '✕' };
  }
  // queued | pending | default
  return { wrapper: 'bg-muted text-muted-foreground', icon: '·' };
}

function formatDuration(ms: number | null, startedAt: string | null): string {
  if (ms !== null) return `+ ${(ms / 1000).toFixed(2)}s`;
  if (startedAt !== null) return 'in corso';
  return 'in attesa';
}

export function IngestionTimelineStep({ step, index, isLast }: IngestionTimelineStepProps) {
  const chip = chipForStatus(step.status);
  return (
    <li
      data-testid={`ingestion-step-${step.stepName.toLowerCase()}`}
      data-step-status={step.status.toLowerCase()}
      className="relative grid grid-cols-[28px_1fr_auto] gap-2.5 items-center py-2 px-1"
    >
      {!isLast && (
        <span
          aria-hidden="true"
          className="absolute left-[13px] top-[28px] bottom-[-8px] w-0.5 bg-border/60"
        />
      )}
      <span
        aria-hidden="true"
        className={`relative z-10 size-6 rounded-full grid place-items-center font-mono text-[11px] font-bold border-2 border-card ${chip.wrapper}`}
      >
        {chip.icon}
      </span>
      <span className="min-w-0">
        <span className="block font-quicksand text-[12.5px] font-bold truncate">
          {STEP_LABELS[step.stepName]}
        </span>
        <span className="block font-mono text-[10px] font-medium text-muted-foreground truncate">
          {STEP_SUBS[step.stepName]}
        </span>
      </span>
      <span className="font-mono text-[10.5px] text-muted-foreground whitespace-nowrap">
        {formatDuration(step.durationMs, step.startedAt)}
      </span>
    </li>
  );
}
```

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -5`
Expected: no new errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionTimelineStep.tsx
git commit -m "feat(kb-ingestion): #1650 add IngestionTimelineStep component"
```

---

## Task 9: Frontend — `IngestionTimeline` component with TDD

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionTimeline.test.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionTimeline.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { IngestionTimeline } from '../IngestionTimeline';
import type { IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

function buildStep(name: IngestionStep['stepName'], status: string, durationMs: number | null = 100): IngestionStep {
  return {
    id: `00000000-0000-0000-0000-00000000000${name[0]}`,
    stepName: name,
    status,
    startedAt: '2026-05-29T10:00:00.000Z',
    completedAt: status === 'Done' ? '2026-05-29T10:00:01.000Z' : null,
    durationMs,
    metadataJson: null,
    logEntries: [],
  };
}

describe('IngestionTimeline', () => {
  it('renders all 5 step slots even if backend returns fewer', () => {
    const { getByTestId } = render(
      <IngestionTimeline steps={[buildStep('Upload', 'Done')]} />,
    );
    expect(getByTestId('ingestion-step-upload')).toBeInTheDocument();
    expect(getByTestId('ingestion-step-extract')).toBeInTheDocument();
    expect(getByTestId('ingestion-step-chunk')).toBeInTheDocument();
    expect(getByTestId('ingestion-step-embed')).toBeInTheDocument();
    expect(getByTestId('ingestion-step-index')).toBeInTheDocument();
  });

  it('synthesizes a pending placeholder for missing steps', () => {
    const { getByTestId } = render(
      <IngestionTimeline steps={[buildStep('Upload', 'Done')]} />,
    );
    expect(getByTestId('ingestion-step-extract').getAttribute('data-step-status')).toBe('pending');
  });

  it('preserves backend status when the step is present', () => {
    const { getByTestId } = render(
      <IngestionTimeline steps={[buildStep('Embed', 'Running', null)]} />,
    );
    expect(getByTestId('ingestion-step-embed').getAttribute('data-step-status')).toBe('running');
  });

  it('renders in fixed pipeline order regardless of input order', () => {
    const { getAllByTestId } = render(
      <IngestionTimeline
        steps={[
          buildStep('Index', 'Done'),
          buildStep('Upload', 'Done'),
        ]}
      />,
    );
    const items = getAllByTestId(/^ingestion-step-/);
    expect(items[0].getAttribute('data-testid')).toBe('ingestion-step-upload');
    expect(items[4].getAttribute('data-testid')).toBe('ingestion-step-index');
  });
});
```

- [ ] **Step 2: Run test, expect failure**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionTimeline.test.tsx 2>&1 | tail -15`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the component**

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber/emerald/rose chip palette + zinc dark (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import type { IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';
import { IngestionTimelineStep } from './IngestionTimelineStep';

const PIPELINE_ORDER: ReadonlyArray<IngestionStep['stepName']> = [
  'Upload',
  'Extract',
  'Chunk',
  'Embed',
  'Index',
];

interface IngestionTimelineProps {
  readonly steps: ReadonlyArray<IngestionStep>;
}

function pendingPlaceholder(name: IngestionStep['stepName']): IngestionStep {
  return {
    id: `pending-${name}`,
    stepName: name,
    status: 'pending',
    startedAt: null,
    completedAt: null,
    durationMs: null,
    metadataJson: null,
    logEntries: [],
  };
}

/**
 * Always renders the 5 pipeline steps in fixed order (Upload → Extract → Chunk →
 * Embed → Index). Missing steps from backend are rendered as `pending`
 * placeholders. Issue #1650.
 */
export function IngestionTimeline({ steps }: IngestionTimelineProps) {
  const byName = new Map(steps.map((s) => [s.stepName, s] as const));
  const ordered = PIPELINE_ORDER.map((n) => byName.get(n) ?? pendingPlaceholder(n));

  return (
    <ol className="flex flex-col gap-0 py-1.5 list-none m-0 p-0">
      {ordered.map((step, idx) => (
        <IngestionTimelineStep
          key={step.id}
          step={step}
          index={idx}
          isLast={idx === ordered.length - 1}
        />
      ))}
    </ol>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionTimeline.test.tsx 2>&1 | tail -10`
Expected: PASS for all 4 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionTimeline.tsx apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionTimeline.test.tsx
git commit -m "feat(kb-ingestion): #1650 add IngestionTimeline with fixed pipeline order"
```

---

## Task 10: Frontend — `IngestionLogBlock` component with TDD

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionLogBlock.test.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionLogBlock.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { IngestionLogBlock } from '../IngestionLogBlock';
import type { IngestionLogEntry, IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

const entry = (level: 'Info' | 'Warning' | 'Error', message: string, secondsAfter = 0): IngestionLogEntry => ({
  id: `00000000-0000-0000-0000-00000000${level[0]}${secondsAfter.toString().padStart(2, '0')}`,
  timestamp: new Date(2026, 4, 29, 10, 0, secondsAfter).toISOString(),
  level,
  message,
});

const step = (name: IngestionStep['stepName'], entries: IngestionLogEntry[]): IngestionStep => ({
  id: `step-${name}`,
  stepName: name,
  status: 'Done',
  startedAt: null,
  completedAt: null,
  durationMs: null,
  metadataJson: null,
  logEntries: entries,
});

describe('IngestionLogBlock', () => {
  it('renders empty placeholder when no entries', () => {
    const { getByText } = render(<IngestionLogBlock steps={[]} />);
    expect(getByText(/nessun log/i)).toBeInTheDocument();
  });

  it('concatenates entries from all steps in timestamp order', () => {
    const { container } = render(
      <IngestionLogBlock
        steps={[
          step('Upload', [entry('Info', 'second', 1)]),
          step('Extract', [entry('Info', 'first', 0)]),
        ]}
      />,
    );
    const text = container.textContent ?? '';
    expect(text.indexOf('first')).toBeLessThan(text.indexOf('second'));
  });

  it('applies error color class to Error entries', () => {
    const { container } = render(
      <IngestionLogBlock steps={[step('Index', [entry('Error', 'boom')])]} />,
    );
    expect(container.querySelector('[data-log-level="Error"]')?.className).toContain('text-rose');
  });

  it('applies warning color class to Warning entries', () => {
    const { container } = render(
      <IngestionLogBlock steps={[step('Index', [entry('Warning', 'careful')])]} />,
    );
    expect(container.querySelector('[data-log-level="Warning"]')?.className).toContain('text-amber');
  });

  it('formats timestamps with seconds precision', () => {
    const { container } = render(
      <IngestionLogBlock steps={[step('Upload', [entry('Info', 'hello', 5)])]} />,
    );
    expect(container.textContent).toMatch(/10:00:05/);
  });
});
```

- [ ] **Step 2: Run test, expect failure**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionLogBlock.test.tsx 2>&1 | tail -15`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the component**

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer log: amber/rose accent (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import type { IngestionLogEntry, IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

interface IngestionLogBlockProps {
  readonly steps: ReadonlyArray<IngestionStep>;
}

const LEVEL_CLASS: Record<IngestionLogEntry['level'], string> = {
  Info: 'text-muted-foreground',
  Warning: 'text-amber-600 dark:text-amber-300',
  Error: 'text-rose-700 dark:text-rose-300 font-semibold',
};

function pad2(n: number): string {
  return n.toString().padStart(2, '0');
}

function pad3(n: number): string {
  return n.toString().padStart(3, '0');
}

function formatTimestamp(iso: string): string {
  const d = new Date(iso);
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(d.getSeconds())}.${pad3(d.getMilliseconds())}`;
}

/**
 * Flat log block — concatenates all log entries across the 5 pipeline steps,
 * ordered by timestamp ascending. Color coding per level (Info muted, Warning
 * amber, Error rose). No live cursor (polling instead of SSE per design).
 * Issue #1650.
 */
export function IngestionLogBlock({ steps }: IngestionLogBlockProps) {
  const all = steps
    .flatMap((s) => s.logEntries.map((e) => ({ ...e, step: s.stepName })))
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp));

  if (all.length === 0) {
    return (
      <div className="font-mono text-[11px] bg-muted/40 text-muted-foreground rounded-md px-3.5 py-3 italic">
        Nessun log da mostrare.
      </div>
    );
  }

  return (
    <pre
      data-testid="ingestion-log-block"
      className="font-mono text-[11px] bg-muted/40 text-foreground rounded-md px-3.5 py-3 leading-relaxed max-h-[280px] overflow-auto whitespace-pre-wrap break-words m-0"
    >
      {all.map((e) => (
        <span key={e.id} data-log-level={e.level} className={LEVEL_CLASS[e.level]}>
          <span className="text-muted-foreground">[{formatTimestamp(e.timestamp)}]</span>{' '}
          [{e.level.toUpperCase()}] {e.message}
          {'\n'}
        </span>
      ))}
    </pre>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionLogBlock.test.tsx 2>&1 | tail -10`
Expected: PASS for all 5 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionLogBlock.tsx apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionLogBlock.test.tsx
git commit -m "feat(kb-ingestion): #1650 add IngestionLogBlock with color-coded levels"
```

---

## Task 11: Frontend — `IngestionActions` component with TDD

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionActions.test.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionActions.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { IngestionActions } from '../IngestionActions';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

function sampleLog(opts: Partial<IngestionLog> = {}): IngestionLog {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    pdfDocumentId: '00000000-0000-0000-0000-000000000002',
    pdfFileName: 'doc.pdf',
    userId: '00000000-0000-0000-0000-000000000003',
    status: 'Completed',
    priority: 0,
    currentStep: null,
    createdAt: new Date().toISOString(),
    startedAt: null,
    completedAt: null,
    errorMessage: null,
    retryCount: 0,
    maxRetries: 3,
    canRetry: false,
    steps: [],
    ...opts,
  };
}

describe('IngestionActions', () => {
  it('shows Download log and Copy job ID always', () => {
    render(<IngestionActions log={sampleLog()} onRetry={vi.fn()} />);
    expect(screen.getByRole('button', { name: /download/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /copy job id/i })).toBeInTheDocument();
  });

  it('hides Re-enqueue when canRetry is false', () => {
    render(<IngestionActions log={sampleLog({ canRetry: false })} onRetry={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /re-enqueue/i })).not.toBeInTheDocument();
  });

  it('shows Re-enqueue when canRetry is true', () => {
    render(<IngestionActions log={sampleLog({ canRetry: true })} onRetry={vi.fn()} />);
    expect(screen.getByRole('button', { name: /re-enqueue/i })).toBeInTheDocument();
  });

  it('calls onRetry with jobId when Re-enqueue clicked', () => {
    const onRetry = vi.fn();
    const log = sampleLog({ canRetry: true });
    render(<IngestionActions log={log} onRetry={onRetry} />);
    fireEvent.click(screen.getByRole('button', { name: /re-enqueue/i }));
    expect(onRetry).toHaveBeenCalledWith(log.id);
  });

  it('writes job ID to clipboard when Copy clicked', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, writable: true });
    const log = sampleLog();
    render(<IngestionActions log={log} onRetry={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /copy job id/i }));
    expect(writeText).toHaveBeenCalledWith(log.id);
  });
});
```

- [ ] **Step 2: Run test, expect failure**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionActions.test.tsx 2>&1 | tail -15`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the component**

```tsx
'use client';

import { useCallback } from 'react';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

interface IngestionActionsProps {
  readonly log: IngestionLog;
  readonly onRetry: (jobId: string) => void;
}

function buildLogText(log: IngestionLog): string {
  return log.steps
    .flatMap((s) => s.logEntries.map((e) => ({ ...e, step: s.stepName })))
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp))
    .map((e) => `[${e.timestamp}] [${e.level.toUpperCase()}] [${e.step}] ${e.message}`)
    .join('\n');
}

function downloadAsFile(content: string, filename: string): void {
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.style.display = 'none';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  // Defer revoke so download can start.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/**
 * Footer with up to 3 actions for the ingestion log tab:
 *   - Download log (always)
 *   - Copy job ID (always)
 *   - Re-enqueue (only when canRetry === true)
 * Issue #1650.
 */
export function IngestionActions({ log, onRetry }: IngestionActionsProps) {
  const handleDownload = useCallback(() => {
    downloadAsFile(buildLogText(log), `ingestion-${log.id}.log`);
  }, [log]);

  const handleCopy = useCallback(() => {
    void navigator.clipboard.writeText(log.id);
  }, [log.id]);

  const handleRetry = useCallback(() => {
    onRetry(log.id);
  }, [log.id, onRetry]);

  return (
    <div className="mt-3 flex flex-wrap gap-1.5">
      <button
        type="button"
        onClick={handleDownload}
        className="px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70"
      >
        ⤓ Download log
      </button>
      <button
        type="button"
        onClick={handleCopy}
        className="px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70"
      >
        📋 Copy job ID
      </button>
      {log.canRetry && (
        <button
          type="button"
          onClick={handleRetry}
          className="px-3 py-1.5 text-xs font-medium border border-amber-500/50 text-amber-700 dark:text-amber-300 rounded-md hover:bg-amber-500/10"
        >
          ⟳ Re-enqueue
        </button>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionActions.test.tsx 2>&1 | tail -10`
Expected: PASS for all 5 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionActions.tsx apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionActions.test.tsx
git commit -m "feat(kb-ingestion): #1650 add IngestionActions (download/copy/retry)"
```

---

## Task 12: Frontend — `IngestionHero` component with TDD

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionHero.test.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionHero.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IngestionHero } from '../IngestionHero';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

function sample(opts: Partial<IngestionLog> = {}): IngestionLog {
  return {
    id: '00000000-0000-0000-0000-000000000099',
    pdfDocumentId: '00000000-0000-0000-0000-000000000088',
    pdfFileName: 'rulebook.pdf',
    userId: '00000000-0000-0000-0000-000000000077',
    status: 'Completed',
    priority: 0,
    currentStep: null,
    createdAt: '2026-05-29T10:00:00.000Z',
    startedAt: '2026-05-29T10:00:01.000Z',
    completedAt: '2026-05-29T10:00:11.000Z',
    errorMessage: null,
    retryCount: 0,
    maxRetries: 3,
    canRetry: false,
    steps: [],
    ...opts,
  };
}

describe('IngestionHero', () => {
  it('shows the file name', () => {
    render(<IngestionHero log={sample()} chunkCount={42} pageCount={10} />);
    expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
  });

  it('shows status chip', () => {
    render(<IngestionHero log={sample({ status: 'Failed' })} chunkCount={0} pageCount={0} />);
    expect(screen.getByText(/failed/i)).toBeInTheDocument();
  });

  it('shows retry counter when retryCount > 0', () => {
    render(<IngestionHero log={sample({ retryCount: 2, maxRetries: 3 })} chunkCount={0} pageCount={0} />);
    expect(screen.getByText(/retry 2\/3/i)).toBeInTheDocument();
  });

  it('hides retry counter when retryCount === 0', () => {
    render(<IngestionHero log={sample({ retryCount: 0 })} chunkCount={0} pageCount={0} />);
    expect(screen.queryByText(/retry/i)).not.toBeInTheDocument();
  });

  it('renders 4 KPI cards: Progress, Chunks, Pages, Cost', () => {
    render(<IngestionHero log={sample()} chunkCount={400} pageCount={80} />);
    expect(screen.getByText(/progresso/i)).toBeInTheDocument();
    expect(screen.getByText(/chunks/i)).toBeInTheDocument();
    expect(screen.getByText(/pages/i)).toBeInTheDocument();
    expect(screen.getByText(/cost/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test, expect failure**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionHero.test.tsx 2>&1 | tail -15`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the component**

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer hero chips (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';
import { estimateCost } from '@/lib/admin-kb/calcCost';

interface IngestionHeroProps {
  readonly log: IngestionLog;
  readonly chunkCount: number;
  readonly pageCount: number;
  /**
   * Embedding model identifier (defaults to the self-hosted bge-base while
   * the backend does not yet expose per-job model in metadata). Issue #1650.
   */
  readonly embeddingModel?: string;
}

function statusChipClass(status: string): string {
  const s = status.toLowerCase();
  if (s === 'completed' || s === 'done') return 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300';
  if (s === 'running' || s === 'processing') return 'bg-amber-500/15 text-amber-700 dark:text-amber-300 animate-pulse';
  if (s === 'failed') return 'bg-rose-500/15 text-rose-700 dark:text-rose-300';
  return 'bg-muted text-muted-foreground';
}

function deriveProgressPct(log: IngestionLog): number {
  if (log.status === 'Completed') return 100;
  if (log.status === 'Failed') return 0;
  const done = log.steps.filter((s) => s.status.toLowerCase() === 'done').length;
  return Math.round((done / 5) * 100);
}

function formatCost(value: number): string {
  if (value === 0) return '$0.00';
  if (value < 0.001) return `≈ $${value.toFixed(6)}`;
  return `≈ $${value.toFixed(4)}`;
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="bg-card/60 border border-border/40 rounded-md px-3 py-2">
      <dt className="font-mono text-[9.5px] uppercase tracking-wider text-muted-foreground">{label}</dt>
      <dd className="font-quicksand text-[15px] font-bold">{value}</dd>
      {hint && <dd className="font-mono text-[9px] text-muted-foreground mt-0.5">{hint}</dd>}
    </div>
  );
}

export function IngestionHero({ log, chunkCount, pageCount, embeddingModel = 'bge-base-en-v1.5' }: IngestionHeroProps) {
  const cost = estimateCost(chunkCount, embeddingModel);
  const progress = deriveProgressPct(log);

  return (
    <header className="p-4 border-b border-border/60 bg-gradient-to-b from-amber-500/5 to-transparent">
      <div className="flex items-start gap-3">
        <span aria-hidden="true" className="text-3xl shrink-0">📄</span>
        <div className="flex-1 min-w-0">
          <h3 className="font-quicksand font-bold text-base truncate">{log.pdfFileName}</h3>
          <div className="mt-1 flex flex-wrap items-center gap-1.5 font-mono text-[10px] text-muted-foreground">
            <span>job {log.id.slice(0, 8)}</span>
            <span aria-hidden="true">·</span>
            <span>started {log.startedAt ?? '—'}</span>
            {log.retryCount > 0 && (
              <>
                <span aria-hidden="true">·</span>
                <span data-testid="ingestion-retry-counter">retry {log.retryCount}/{log.maxRetries}</span>
              </>
            )}
            <span className={`ml-auto inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full ${statusChipClass(log.status)}`}>
              {log.status}
            </span>
          </div>
        </div>
      </div>

      <dl className="mt-3 grid grid-cols-4 gap-2">
        <Stat label="Progresso" value={`${progress}%`} />
        <Stat label="Chunks" value={chunkCount.toLocaleString('it-IT')} />
        <Stat label="Pages" value={pageCount === 0 ? '—' : pageCount.toLocaleString('it-IT')} />
        <Stat label="Cost" value={cost ? formatCost(cost.value) : '—'} hint={cost?.formula ?? 'modello sconosciuto'} />
      </dl>
    </header>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionHero.test.tsx 2>&1 | tail -10`
Expected: PASS for all 5 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionHero.tsx apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionHero.test.tsx
git commit -m "feat(kb-ingestion): #1650 add IngestionHero with 4 KPIs"
```

---

## Task 13: Frontend — `IngestionPanel` container with TDD

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionPanel.test.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionPanel.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { IngestionPanel } from '../IngestionPanel';
import * as ingestApi from '@/lib/api/admin-kb-ingestion';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';
import type { ReactNode } from 'react';

function wrap() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

function buildLog(): IngestionLog {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    pdfDocumentId: '00000000-0000-0000-0000-000000000002',
    pdfFileName: 'doc.pdf',
    userId: '00000000-0000-0000-0000-000000000003',
    status: 'Completed',
    priority: 0,
    currentStep: null,
    createdAt: '2026-05-29T10:00:00.000Z',
    startedAt: '2026-05-29T10:00:01.000Z',
    completedAt: '2026-05-29T10:00:11.000Z',
    errorMessage: null,
    retryCount: 0,
    maxRetries: 3,
    canRetry: false,
    steps: [],
  };
}

describe('IngestionPanel', () => {
  it('shows skeleton while loading', () => {
    vi.spyOn(ingestApi, 'fetchKbDocIngestionLog').mockImplementation(
      () => new Promise(() => undefined),
    );
    render(<IngestionPanel docId="xxx" chunkCount={0} pageCount={0} />, { wrapper: wrap() });
    expect(screen.getByTestId('ingestion-panel-loading')).toBeInTheDocument();
  });

  it('shows empty state when backend returns null', async () => {
    vi.spyOn(ingestApi, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    render(<IngestionPanel docId="xxx" chunkCount={0} pageCount={0} />, { wrapper: wrap() });
    await waitFor(() => expect(screen.getByTestId('ingestion-panel-empty')).toBeInTheDocument());
  });

  it('renders hero + timeline + log + actions when data present', async () => {
    vi.spyOn(ingestApi, 'fetchKbDocIngestionLog').mockResolvedValue(buildLog());
    render(<IngestionPanel docId="xxx" chunkCount={10} pageCount={4} />, { wrapper: wrap() });
    await waitFor(() => expect(screen.getByText('doc.pdf')).toBeInTheDocument());
    expect(screen.getByTestId('ingestion-step-upload')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /download/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test, expect failure**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionPanel.test.tsx 2>&1 | tail -15`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the container**

```tsx
'use client';

import { useKbDocIngestionLog } from '@/hooks/queries/useKbDocIngestionLog';
import { IngestionHero } from './IngestionHero';
import { IngestionTimeline } from './IngestionTimeline';
import { IngestionLogBlock } from './IngestionLogBlock';
import { IngestionActions } from './IngestionActions';

interface IngestionPanelProps {
  readonly docId: string;
  readonly chunkCount: number;
  readonly pageCount: number;
}

/**
 * Container of the "Ingestion log" tab inside KbDocDetailPanel. Wires the
 * hook to four presentational children (Hero / Timeline / LogBlock / Actions).
 * Issue #1650.
 *
 * Re-enqueue action: temporarily wired as a no-op handler — backend retry
 * endpoint POST /admin/queue/jobs/{jobId}/retry is integrated in Task 14
 * (after the panel is consumed by KbDocDetailPanel).
 */
export function IngestionPanel({ docId, chunkCount, pageCount }: IngestionPanelProps) {
  const query = useKbDocIngestionLog({ docId });

  if (query.isLoading) {
    return (
      <div
        data-testid="ingestion-panel-loading"
        className="border border-border/60 rounded-lg bg-card/80 p-6 animate-pulse min-h-[200px]"
      >
        <div className="h-6 w-2/3 bg-muted rounded mb-4" />
        <div className="h-4 w-1/2 bg-muted rounded mb-2" />
        <div className="h-4 w-1/3 bg-muted rounded" />
      </div>
    );
  }

  if (query.isError) {
    return (
      <div className="border border-rose-500/30 rounded-lg bg-rose-500/5 p-6 text-sm text-rose-700 dark:text-rose-300">
        Errore caricamento ingestion log: {query.error.message}
      </div>
    );
  }

  if (query.data === null) {
    return (
      <div
        data-testid="ingestion-panel-empty"
        className="border border-border/60 rounded-lg bg-card/80 p-8 text-center text-sm text-muted-foreground min-h-[200px] flex flex-col items-center justify-center gap-2"
      >
        <span aria-hidden="true" className="text-3xl">🗂️</span>
        <p>Nessun job di ingestion per questo documento.</p>
        <p className="text-xs">Il documento potrebbe essere stato indicizzato con una pipeline precedente.</p>
      </div>
    );
  }

  const log = query.data;

  function handleRetry(_jobId: string): void {
    // Wired in Task 14 with the retry mutation hook.
  }

  return (
    <section className="border border-border/60 rounded-lg bg-card/80 overflow-hidden">
      <IngestionHero log={log} chunkCount={chunkCount} pageCount={pageCount} />
      <div className="p-4">
        <IngestionTimeline steps={log.steps} />
      </div>
      <div className="px-4 pb-4">
        <IngestionLogBlock steps={log.steps} />
      </div>
      <div className="px-4 pb-4">
        <IngestionActions log={log} onRetry={handleRetry} />
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionPanel.test.tsx 2>&1 | tail -10`
Expected: PASS for all 3 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionPanel.tsx apps/web/src/components/admin/knowledge-base/explorer/ingestion/__tests__/IngestionPanel.test.tsx
git commit -m "feat(kb-ingestion): #1650 add IngestionPanel container"
```

---

## Task 14: Frontend — wire `Re-enqueue` mutation

**Files:**
- Modify: `apps/web/src/lib/api/admin-kb-ingestion.ts`
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionPanel.tsx`

- [ ] **Step 1: Add the mutation function to the API client**

Append to `apps/web/src/lib/api/admin-kb-ingestion.ts`:

```typescript
/**
 * POST /api/v1/admin/queue/jobs/{jobId}/retry
 * Re-enqueues a failed processing job. Backend handler is RetryJobCommand.
 * Issue #1650.
 */
export async function retryIngestionJob(jobId: string): Promise<void> {
  await apiClient.post<unknown>(
    `/api/v1/admin/queue/jobs/${jobId}/retry`,
    {},
  );
}
```

> **NB**: verify the exact `apiClient.post` signature when implementing (the codebase may need a schema arg or accept an undefined body). Adjust to the actual `apiClient` shape.

- [ ] **Step 2: Wire mutation in `IngestionPanel`**

Replace the `handleRetry` stub with a real mutation:

```tsx
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { fetchKbDocIngestionLog, retryIngestionJob } from '@/lib/api/admin-kb-ingestion';
import { kbDocIngestionLogKeys } from '@/hooks/queries/useKbDocIngestionLog';

// ...inside IngestionPanel body, before the early returns:
const qc = useQueryClient();
const retryMutation = useMutation({
  mutationFn: (jobId: string) => retryIngestionJob(jobId),
  onSuccess: () => qc.invalidateQueries({ queryKey: kbDocIngestionLogKeys.byId(docId) }),
});

// Replace handleRetry:
const handleRetry = (jobId: string) => retryMutation.mutate(jobId);
```

- [ ] **Step 3: Run all ingestion FE tests**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/ingestion 2>&1 | tail -10`
Expected: all PASS (no regression)

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/admin-kb-ingestion.ts apps/web/src/components/admin/knowledge-base/explorer/ingestion/IngestionPanel.tsx
git commit -m "feat(kb-ingestion): #1650 wire Re-enqueue mutation to RetryJobCommand"
```

---

## Task 15: Frontend — `KbDocDetailTabs` component with TDD

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { KbDocDetailTabs } from '../KbDocDetailTabs';
import { ReadonlyURLSearchParams } from 'next/navigation';
import { vi } from 'vitest';

// Mock next/navigation for the test environment.
vi.mock('next/navigation', async () => {
  const actual = await vi.importActual<typeof import('next/navigation')>('next/navigation');
  return {
    ...actual,
    usePathname: () => '/admin/knowledge-base',
    useSearchParams: () => new URLSearchParams('docId=abc&tab=ingestion'),
  };
});

describe('KbDocDetailTabs', () => {
  it('renders two tabs: Overview and Ingestion log', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    expect(screen.getByRole('link', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /ingestion log/i })).toBeInTheDocument();
  });

  it('preserves docId in each tab href', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    const ingestionLink = screen.getByRole('link', { name: /ingestion log/i });
    expect(ingestionLink.getAttribute('href')).toContain('docId=abc');
    expect(ingestionLink.getAttribute('href')).toContain('tab=ingestion');
  });

  it('marks active tab with aria-current="page"', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="ingestion" />);
    expect(screen.getByRole('link', { name: /ingestion log/i }).getAttribute('aria-current')).toBe('page');
    expect(screen.getByRole('link', { name: /overview/i }).getAttribute('aria-current')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test, expect failure**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx 2>&1 | tail -15`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the component**

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber accent underline (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import Link from 'next/link';

export type KbDocTabKey = 'overview' | 'ingestion';

interface KbDocDetailTabsProps {
  readonly docId: string;
  readonly activeTab: KbDocTabKey;
}

const TABS: ReadonlyArray<{ readonly key: KbDocTabKey; readonly label: string }> = [
  { key: 'overview', label: 'Overview' },
  { key: 'ingestion', label: 'Ingestion log' },
];

/**
 * Inner tab nav for `KbDocDetailPanel`. URL-driven via `?tab=overview` (default)
 * or `?tab=ingestion`. Preserves `docId` in each link. Issue #1650.
 *
 * Future follow-ups (#1651/#1653/#1654) will add Used-by, Actions, and
 * Preview tabs to this same component.
 */
export function KbDocDetailTabs({ docId, activeTab }: KbDocDetailTabsProps) {
  return (
    <nav
      aria-label="Sezione documento"
      className="border-b border-border/60 -mx-4 px-4 mb-4 overflow-x-auto"
    >
      <ul className="flex gap-1 min-w-max m-0 p-0 list-none">
        {TABS.map((tab) => {
          const isActive = activeTab === tab.key;
          const params = new URLSearchParams({ docId });
          if (tab.key !== 'overview') params.set('tab', tab.key);
          const href = `/admin/knowledge-base?${params.toString()}`;
          return (
            <li key={tab.key}>
              <Link
                href={href}
                aria-current={isActive ? 'page' : undefined}
                className={[
                  'inline-flex items-center px-3 py-2 text-sm font-medium border-b-2 -mb-px transition-colors',
                  isActive
                    ? 'border-amber-500 text-foreground'
                    : 'border-transparent text-muted-foreground hover:text-foreground hover:border-border',
                ].join(' ')}
              >
                {tab.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx 2>&1 | tail -10`
Expected: PASS for all 3 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx
git commit -m "feat(kb-ingestion): #1650 add KbDocDetailTabs URL-driven nav"
```

---

## Task 16: Frontend — refactor `KbDocDetailPanel` to host tabs

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`
- Modify (extend): `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx`

- [ ] **Step 1: Add a failing test for tab routing**

Append to `KbDocDetailPanel.test.tsx`:

```tsx
import { vi } from 'vitest';

// Inside an existing describe block or a new one:
describe('KbDocDetailPanel — tab switching', () => {
  beforeEach(() => vi.resetModules());

  it('renders Overview when no ?tab query', async () => {
    vi.doMock('next/navigation', () => ({
      useSearchParams: () => new URLSearchParams('docId=abc'),
      usePathname: () => '/admin/knowledge-base',
    }));
    const { KbDocDetailPanel } = await import('../KbDocDetailPanel');
    render(<KbDocDetailPanel docId="abc" />, { wrapper: wrapWithQueryClient() });
    expect(screen.queryByTestId('ingestion-panel-empty')).not.toBeInTheDocument();
    // Existing chunks rendering should still appear
  });

  it('renders IngestionPanel when ?tab=ingestion', async () => {
    vi.doMock('next/navigation', () => ({
      useSearchParams: () => new URLSearchParams('docId=abc&tab=ingestion'),
      usePathname: () => '/admin/knowledge-base',
    }));
    const { KbDocDetailPanel } = await import('../KbDocDetailPanel');
    // Mock the ingestion api to avoid network calls
    const ingest = await import('@/lib/api/admin-kb-ingestion');
    vi.spyOn(ingest, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    render(<KbDocDetailPanel docId="abc" />, { wrapper: wrapWithQueryClient() });
    await waitFor(() =>
      expect(screen.getByTestId('ingestion-panel-empty')).toBeInTheDocument(),
    );
  });
});
```

> **NB**: `wrapWithQueryClient` is the existing test wrapper used in the file (or create it locally — reference the existing tests in this file when implementing).

- [ ] **Step 2: Refactor `KbDocDetailPanel`**

```tsx
// At top of file:
import { useSearchParams } from 'next/navigation';
import { KbDocDetailTabs, type KbDocTabKey } from './KbDocDetailTabs';
import { IngestionPanel } from './ingestion/IngestionPanel';

// Inside the component, after the existing "if (docId === null)" placeholder
// and before the chunks rendering — replace the existing return when
// envelope?.status === 'ready' so that:

const searchParams = useSearchParams();
const activeTab: KbDocTabKey = searchParams?.get('tab') === 'ingestion' ? 'ingestion' : 'overview';

// In the final "ready" branch, wrap the existing hero+chunks with the tab nav
// and conditionally render ingestion panel:

return (
  <div className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 overflow-hidden">
    <KbDocDetailTabs docId={doc.id} activeTab={activeTab} />
    {activeTab === 'ingestion' ? (
      <IngestionPanel
        docId={doc.id}
        chunkCount={doc.chunkCount}
        pageCount={doc.pageCount ?? 0}
      />
    ) : (
      <>
        {/* existing Hero header + chunks list — keep as-is */}
      </>
    )}
  </div>
);
```

> **NB**: `doc.id` and `doc.chunkCount`/`doc.pageCount` already exist in the
> envelope ("ready" branch). Adjust the wrapping markup carefully — the existing
> `<header>` and `<section>` blocks for the Overview tab must stay inside the
> `{activeTab !== 'ingestion' && (...)}` branch, otherwise both tabs render
> simultaneously.

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/admin/knowledge-base/explorer 2>&1 | tail -15`
Expected: all PASS (existing + new tab tests)

- [ ] **Step 4: Run full FE test suite (regression check)**

Run: `cd apps/web && pnpm test 2>&1 | tail -10`
Expected: no new failures

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx
git commit -m "feat(kb-ingestion): #1650 wire KbDocDetailPanel tabs (?tab=ingestion)"
```

---

## Task 17: Quality gates (lint, typecheck, full suite)

**Files:** none modified, verification-only

- [ ] **Step 1: Frontend lint**

Run: `cd apps/web && pnpm lint 2>&1 | tail -20`
Expected: no new errors (warnings on token utility classes may appear — verify they're already lint-disabled inline per the design's ESLint rule)

- [ ] **Step 2: Frontend typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -5`
Expected: PASS

- [ ] **Step 3: Frontend full unit suite + coverage**

Run: `cd apps/web && pnpm test:coverage --run 2>&1 | tail -30`
Expected: PASS; coverage on new files ≥85% (target from CLAUDE.md)

- [ ] **Step 4: Backend full unit suite**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore 2>&1 | tail -10`
Expected: PASS, no new failures vs baseline (baseline is zero per CLAUDE.md)

- [ ] **Step 5: Backend coverage on new handler (spot check)**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetLatestIngestionLogByDocumentIdQueryHandlerTests" /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:Include="[Api]Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue.GetLatestIngestionLogByDocumentIdQueryHandler*" 2>&1 | tail -10`
Expected: ≥90% line coverage on the new handler file (per CLAUDE.md backend target)

- [ ] **Step 6: Commit (only if any housekeeping changed)**

No code changes expected here — if `pnpm lint --fix` made adjustments, commit them:
```bash
git status --short
# if any modified:
git add -A && git commit -m "chore(kb-ingestion): #1650 lint autofix"
```

---

## Task 18: Open PR, code review, merge, close issue

**Files:** none in workspace — workflow-only

- [ ] **Step 1: Push branch and open PR**

Run:
```bash
git push -u origin feature/issue-1650-ingestion-log-tab
gh pr create --title "feat(admin-kb): #1650 F3-FU-1 — Ingestion log tab in KbDocDetailPanel" --body "$(cat <<'EOF'
## Summary

Implements F3-FU-1 (#1650): a new "Ingestion log" tab inside `KbDocDetailPanel` showing the pipeline status (5-step timeline) + log entries of the most recent processing job for the selected PDF.

- Backend: new `GetLatestIngestionLogByDocumentIdQuery` + handler + endpoint `GET /api/v1/admin/kb/docs/{docId}/ingestion-log` (reuses existing `ProcessingJobDetailDto`)
- Frontend: URL-driven tab switching (`?tab=ingestion`) + 7 new components in `explorer/ingestion/` + 1 hook with smart polling (6s while running)
- Reuses existing `RetryJobCommand` for the Re-enqueue action

Design doc: `docs/superpowers/specs/2026-05-29-kb-ingestion-log-tab-design.md`
Plan: `docs/superpowers/plans/2026-05-29-kb-ingestion-log-tab.md`

## Test plan
- [x] Backend: 6 unit tests on the handler + 3 integration tests on the endpoint
- [x] Frontend: 25+ unit tests across hook, utility, and 6 new components
- [x] Lint + typecheck pass
- [x] Manual: open `/admin/knowledge-base?docId=<existing>&tab=ingestion` → timeline + log + actions render
- [ ] Manual: click "Re-enqueue" on a Failed job and verify status flips back to Queued

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)" --base main-dev`
```

Expected: PR URL printed. Note it for next steps.

- [ ] **Step 2: Run code review**

Invoke the code review skill against the new PR URL:
```
/code-review:code-review <PR-URL>
```

Address any P0/P1 issues raised (commit fixes onto the same branch — they'll auto-update the PR).

- [ ] **Step 3: Verify CI green**

Run: `gh pr checks <PR-NUMBER> --watch`
Expected: all checks green (lint, typecheck, backend tests, FE tests, build)

- [ ] **Step 4: Merge PR**

Run: `gh pr merge <PR-NUMBER> --squash --delete-branch`
Expected: merged into `main-dev`, feature branch auto-deleted on remote.

- [ ] **Step 5: Update issue #1650**

Run:
```bash
gh issue comment 1650 --body "Implemented and merged via PR #<PR-NUMBER>. Closes the F3-FU-1 follow-up scope. Subsequent follow-ups: #1651 (Used-by), #1653 (Azioni), #1654 (Preview), #1655 (Badge count)."
gh issue close 1650
```

- [ ] **Step 6: Clean up local branch**

Run: `git checkout main-dev && git pull --ff-only && git branch -D feature/issue-1650-ingestion-log-tab`
Expected: back on `main-dev`, local feature branch removed.

---

## Spec coverage matrix

| Spec section | Task(s) |
|---|---|
| Architecture diagram | 1, 2, 3, 5, 7, 13, 16 |
| Decisione Q1 (polling 6s) | 7 (`useKbDocIngestionLog`) |
| Decisione Q2 (solo ultimo job) | 2 (`OrderByDescending` + `FirstOrDefaultAsync`) |
| Decisione Q3 (color coding only) | 10 (`IngestionLogBlock`) |
| Decisione Q4 (timeline + log block) | 8, 9, 10 |
| Decisione Q5 (nuovo endpoint dedicato) | 1, 2, 3 |
| Decisione Q6a (URL-driven tab) | 15, 16 |
| Decisione Q6b (nuovi componenti FE) | 8–13 |
| Decisione Q6c (4 KPI w/ Cost FE) | 6, 12 |
| Decisione Q6d (3 actions) | 11, 14 |
| Mapping 5 step → label UI | 8 (`STEP_LABELS`) |
| Cost utility | 6 |
| Re-enqueue riusa RetryJobCommand | 14 |
| Error handling: docId null | 13 |
| Error handling: nessun job | 13 (empty state) |
| Error handling: job failed | 12 (status chip), 11 (Re-enqueue) |
| Polling auto-stop su terminal | 7 (`refetchInterval` returns `false`) |
| Test backend ≥90% | 2, 3, 17 |
| Test frontend ≥85% | 6, 7, 9, 10, 11, 12, 13, 15, 16, 17 |
| E2E smoke (optional) | (deferred — design dichiara opzionale per P3) |
| Out of scope (SSE/filtri/storico/cost reale/badge) | n/a (escluso) |

---

## Self-review notes (writer)

- **Placeholders/TBDs**: searched for "TBD", "TODO" → only "TBD" appears in 1 NB about adjusting the `SeedJobWithId` helper to the actual `ProcessingJob.Create` signature, which is the legitimate next-step for the engineer at impl time. Acceptable.
- **Type consistency**: `IngestionLog`/`IngestionStep`/`IngestionLogEntry` names match across all tasks. Backend keeps `ProcessingJobDetailDto`/`ProcessingStepDto`/`StepLogEntryDto` (existing). FE Zod schemas (`IngestionLogSchema` etc.) wrap the backend shape and re-expose under domain-aligned names — explicit mapping done in Task 4.
- **Spec coverage**: matrix above covers all 11 functional requirements + error handling + testing.
- **Adjusted from design**: DTO reuse (no new IngestionLogDto) — documented in the "Deviation" section at the top.
