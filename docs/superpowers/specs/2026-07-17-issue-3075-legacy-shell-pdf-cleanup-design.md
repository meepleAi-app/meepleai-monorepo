# Issue #3075 — Extend OrphanPdfCleanupSeeder to remove legacy-scheme empty shells — Design

**Date**: 2026-07-17
**Issue**: [#3075](https://github.com/meepleAi-app/meepleai-monorepo/issues/3075) (tech-debt/backend, follow-up to #2907/#2908)
**Branch**: `feature/issue-3075-legacy-shell-pdf-cleanup` (parent `main-dev`)

---

## Problem

`OrphanPdfCleanupSeeder` (#2907) removes `pdf_documents` whose `shared_game_id` resolves to a missing/soft-deleted SharedGame. It misses a second dead-row class: **legacy-scheme empty shells** — rows with a **valid** parent SharedGame, a pre-migration `FilePath` (`seed/<user>/<game>/rulebook.pdf`), and **zero `text_chunks`**, superseded by a modern record (`pdfs/{id}/...`, `<game>_rulebook.pdf`) that holds the real chunks/embeddings.

They contribute nothing to RAG (0 chunks) but are time bombs: when disturbed (`StalePdfRecoveryService` on startup / a reindex) they fail on the missing blob → `ProcessingState=Failed` → `SeedStateHealthCheck` derives `partial_failed` → `/health` Degraded → recurring `seed_state Unhealthy` email alert. Confirmed on staging 2026-07-17 (7 rows across Spirit Island, 7 Wonders, Ark Nova, Catan, Pandemic, Root, Wingspan), manually hard-deleted to restore `seed_state=ready`.

## Why existing self-healers miss them
- `RetryFailedPdfsJob`: `error_category IS NULL` → skipped.
- `PdfSeeder.TryRepairMissingBlobAsync` (#2666): legacy `FileName` (`rulebook-<game>.pdf`) ≠ manifest `Path.GetFileName(pdfBlobKey)` (`<game>_rulebook.pdf`) → idempotency key misses.
- `OrphanPdfCleanupSeeder` (#2907): parent is valid → not an orphan by its definition.

## Decision — extend `OrphanPdfCleanupSeeder` (not a new seeder)

Same deletion mechanic (`DeleteKbDocumentCommand` → canonical cascade), same best-effort/idempotent loop, same single call-site in `CatalogSeedLayer` (unchanged). The class doc broadens from "orphan-by-missing-parent" to "cleanup of dead `pdf_documents` rows (orphan + legacy empty shell)".

### Target-set definition (safety-critical — zero false positives)
`FilePath LIKE 'seed/%'` **AND** no associated `text_chunks`.
- **Scheme-anchored**: the `seed/` runtime path is frozen — the modern seeder/upload always writes `pdfs/{id}/...`. A `seed/` row is therefore never in-flight → no risk of deleting a record mid-embedding.
- **0-chunk guard**: any row that still serves RAG (has chunks) is untouched, even if legacy-scheme.

### Changes (`OrphanPdfCleanupSeeder.cs`)
1. `FindLegacyShellPdfIdsAsync(db, ct)`:
   ```
   db.PdfDocuments
     .Where(p => p.FilePath.StartsWith("seed/")
                 && !db.TextChunks.Any(tc => tc.PdfDocumentId == p.Id))
     .Select(p => p.Id)
   ```
   (`StartsWith("seed/")` → SQL `LIKE 'seed/%'`; `!Any(text_chunks)` → `NOT EXISTS`.)
2. `CleanupAsync`: `orphanIds.Union(legacyShellIds)` (dedupe — a row can be both), delete each via the existing resilient loop. Log separate counters (orphans vs shells).
3. Broaden the class XML doc.

### Deletion / hook / gating — unchanged
`DeleteKbDocumentCommand` via `IMediator` (cascade: text_chunks/vector_document/pgvector/blob/cache/`PdfDeletedDomainEvent`; idempotent on `NotFoundException`). `CatalogSeedLayer` already calls `CleanupAsync`. Gating stays Staging+ (prod is Core-only).

## Testing

**Unit** (`OrphanPdfCleanupSeederTests` pattern — in-memory `DbContext` + Moq `IMediator`):
- legacy shell (`seed/…` + 0 chunks, valid parent) → `DeleteKbDocumentCommand` sent.
- legacy row **with** chunks → not sent (0-chunk guard).
- modern row (`pdfs/…`) 0 chunks → not sent (path guard).
- classic orphan (#2907, missing parent) → still sent (no regression).
- row that is both orphan **and** legacy shell → sent exactly once (dedupe).
- `mediator` throws `NotFoundException` → skipped, loop continues.

**Integration** (Testcontainers, `BoundedContext=DocumentProcessing`): seed a legacy shell (`seed/` path, 0 chunks, valid parent) + a modern Ready sibling with chunks; run `CleanupAsync`; assert the shell is gone and the modern sibling + its chunks survive.

## Out of scope (YAGNI)
- Legacy rows **with** chunks (still serving RAG).
- Scheme-agnostic definition (0 chunks + Ready sibling) — rejected: false-positive risk on in-flight rows.
- Prod-profile cleanup (Core-only).
