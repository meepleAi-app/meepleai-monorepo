# Issues #2907 + #2908 — Seed self-healing: orphan cleanup + re-enqueue — Design

**Date**: 2026-07-14
**Issues**: [#2907](https://github.com/meepleAi-app/meepleai-monorepo/issues/2907) (orphan pdf cleanup, tech-debt/backend/P3) · [#2908](https://github.com/meepleAi-app/meepleai-monorepo/issues/2908) (re-enqueue stranded Pending, tech-debt/backend/P3)
**Branch**: `feature/issue-2907-2908-seed-orphan-cleanup-reenqueue` (combined PR)
**Parent context**: follow-ups to #2904 (deterministic SharedGame ids, PR #2905 merged 2026-07-13) which stopped *new* orphans; these clean up the *existing* backlog + un-strand Pending PDFs.

---

## Discovery findings that changed the design

1. **#2907 "soft-delete" premise is unfounded.** `pdf_documents` has **no** `IsDeleted`/`DeletedAt`; the `PdfDocument` aggregate has no soft-delete method. Every existing delete path is a **hard cascade delete**. → **Decision (user-confirmed): hard-delete cascade**, not new soft-delete infra.
2. **`pgvector_embeddings` has no FK cascade** (raw-SQL table). A naive EF `Remove` leaves embeddings dangling. The canonical cascade order lives in `DeleteKbDocumentCommandHandler` (pgvector purge via `IVectorStoreAdapter` → EF `Remove` → blob → cache → domain event).
3. **`DeleteKbDocumentCommand` is `record DeleteKbDocumentCommand(Guid Id)`** — clean. → #2907 **reuses the canonical handler via `IMediator`** instead of re-implementing the cascade. Zero duplication; agent-detach + pgvector + cascade + blob + cache + `PdfDeletedDomainEvent` come for free; idempotent (`NotFoundException` on re-run).
4. **`processing_jobs` FK → `pdf_documents` is `OnDelete(Cascade)`.** Deleting an orphan PDF auto-removes its jobs/steps. "Zombie job" risk resolved.
5. **The enqueue block is duplicated** in `PdfSeeder` (new-record path L245-266 + `TryRepairMissingBlobAsync` L366-387). #2908 would be a 3rd copy → **extract `EnqueueProcessingJob` helper** and refactor both.
6. **`CatalogSeedLayer` already resolves `IMediator`** and runs best-effort post-`CatalogSeeder` steps (Badsworm, GameBook) → the natural hook point.

## Decisions (user-confirmed)

| # | Decision |
|---|----------|
| Delete semantics | **Hard-delete cascade** via `IMediator.Send(DeleteKbDocumentCommand(id))` (mirrors the canonical handler). |
| Orphan definition | **missing OR soft-deleted parent** — `pdf.SharedGameId ∉ db.SharedGames` (the `!IsDeleted` global filter naturally catches both), matching the issue's own SQL (`AND NOT sg.is_deleted`). |
| Delivery | **1 combined PR**, gating **Staging+** (inline in `CatalogSeedLayer`, where the 144 known orphans live; Prod runs Core-only). |
| Ordering | #2907 cleanup **before** #2908 re-enqueue (never re-enqueue an about-to-be-deleted orphan). |

## Design

Two new `internal static` seeders under `Infrastructure/Seeders/Catalog/`, invoked from `CatalogSeedLayer.SeedAsync` **after** `CatalogSeeder.SeedAsync` (which contains GameSeeder → PdfSeeder), each wrapped in best-effort `try/catch`:

### #2907 — `OrphanPdfCleanupSeeder.CleanupAsync(db, mediator, logger, ct)`
```
validGameIds = db.SharedGames.Select(g => g.Id)        // global !IsDeleted filter applies
orphanIds    = db.PdfDocuments
                 .Where(p => p.SharedGameId != null && !validGameIds.Contains(p.SharedGameId.Value))
                 .Select(p => p.Id).ToListAsync()
foreach id:  try { await mediator.Send(new DeleteKbDocumentCommand(id), ct); removed++ }
             catch (NotFoundException) { skipped++ }          // already gone → idempotent
             catch (Exception ex) { log; db.ChangeTracker.Clear(); skipped++ }   // resilient
log summary (removed / skipped)
```
- PDFs with `SharedGameId == null` are **not** shared-game-orphans → untouched (out of scope; issue targets `shared_game_id`).
- Idempotent: hard-deleted rows vanish from the anti-join on re-run.

### #2908 — `ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, systemUserId, logger, ct)`
```
validGameIds = db.SharedGames.Select(g => g.Id)
strandedIds  = db.PdfDocuments.Where(p =>
                 p.ProcessingState == nameof(PdfProcessingState.Pending)
                 && p.SharedGameId != null && validGameIds.Contains(p.SharedGameId.Value)
                 && !db.ProcessingJobs.Any(j => j.PdfDocumentId == p.Id
                       && (j.Status == nameof(JobStatus.Queued) || j.Status == nameof(JobStatus.Processing))))
                 .Select(p => p.Id).ToListAsync()
foreach id:  PdfSeeder.EnqueueProcessingJob(db, id, systemUserId); await db.SaveChangesAsync(ct); enqueued++
log summary (enqueued / skipped)
```
- Scoped to valid catalog games (the `validGameIds` filter excludes orphans/soft-deleted) — so it never re-enqueues an orphan even if run standalone.
- "no active job" = no `Queued`/`Processing` job (a stale `Completed`/`Failed`/`Cancelled` job does **not** count as active → re-enqueue).
- Idempotent: after enqueue the PDF has a `Queued` job → excluded on re-run.

### Shared helper — `PdfSeeder.EnqueueProcessingJob(db, pdfId, systemUserId) : ProcessingJobEntity`
Extracts the exact `ProcessingJobEntity{Status=Queued, Priority=0, MaxRetries=3} + 5 ProcessingStepEntity{Status="Pending"}` block (currently copy-pasted twice). Adds to the change tracker and returns the entity; **caller** owns `SaveChangesAsync`. Both existing `PdfSeeder` copies are refactored to call it (behavior-preserving).

### Hook — `CatalogSeedLayer.SeedAsync`
After `await CatalogSeeder.SeedAsync(...)`, before Badsworm:
```
try { var mediator = context.Services.GetRequiredService<IMediator>();
      await OrphanPdfCleanupSeeder.CleanupAsync(context.DbContext, mediator, context.Logger, ct); }
catch (Exception ex) { context.Logger.LogError(ex, "[Catalog] OrphanPdfCleanupSeeder failed — continuing"); }

try { await ReattemptStalePendingPdfsSeeder.ReattemptAsync(context.DbContext, context.SystemUserId, context.Logger, ct); }
catch (Exception ex) { context.Logger.LogError(ex, "[Catalog] ReattemptStalePendingPdfsSeeder failed — continuing"); }
```

## Testing strategy

- **Unit** (`TestDbContextFactory.CreateInMemoryDbContext` + Moq), following `PdfSeederBlobTests`:
  - `OrphanPdfCleanupSeederTests`: orphan (missing parent) → `mediator.Send(DeleteKbDocumentCommand(id))`; soft-deleted parent → also sent; valid parent → **not** sent; `SharedGameId == null` → not sent; `mediator` throws `NotFoundException` → skipped, loop continues. EF global query filter is honored in-memory, so soft-deleted-parent is testable without Testcontainers.
  - `ReattemptStalePendingPdfsSeederTests`: Pending+valid+no-job → 1 Queued job + 5 Pending steps; existing Queued → noop; existing Processing → noop; only Completed/Failed job → enqueue; orphan game → skip; non-Pending (Ready/Failed) → skip; double-invocation → exactly 1 job.
  - Existing `PdfSeederBlobTests` must still pass after the `EnqueueProcessingJob` extraction (regression guard for the refactor).
- **Integration** (Testcontainers `Integration-GroupA`, `[Trait BoundedContext=DocumentProcessing]`): real Npgsql cascade — seed an orphan PDF with text_chunks + vector_document + pgvector embeddings and a valid Pending PDF; run both seeders in order; assert the orphan and all its children are gone (real cascade) and the valid Pending PDF gained a Queued job. Validated on CI (local full integration suite is unreliable).

## DoD mapping

- #2907: retroactive orphan cleanup ✔ (hard-delete cascade via canonical command, idempotent, audit via logger counters). Soft-delete reinterpreted → hard-delete per finding #1 (documented).
- #2908: auto re-enqueue Pending-with-no-active-job scoped to valid catalog games ✔; idempotent; uses the direct-entity enqueue (bypasses MaxQueueSize like PdfSeeder).

## Out of scope / follow-up
- Prod-profile orphan cleanup (would need a `MinimumProfile=Prod` layer) — deferred; known orphans are on staging.
- `StalePdfRecoveryService` overlap (it re-drives Pending in-process): #2908 runs at seed-time before its 30s delay and only creates a Queued job; no code change here, noted for awareness.
