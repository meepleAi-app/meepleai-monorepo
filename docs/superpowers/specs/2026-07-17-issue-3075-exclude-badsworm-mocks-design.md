# Issue #3075 — Exclude Badsworm dogfood mock PDFs from seed_state — Design

**Date**: 2026-07-17
**Issue**: [#3075](https://github.com/meepleAi-app/meepleai-monorepo/issues/3075) (tech-debt/backend)
**Branch**: `feature/issue-3075-legacy-shell-pdf-cleanup` (parent `main-dev`)

> **Superseded design note.** This spec replaces an earlier (wrong) design that proposed *deleting* `seed/…` 0-chunk rows as "legacy shells". Adversarial code review on PR #3077 established those rows are **active dogfood fixtures**, not debris — deleting them breaks the demo. This spec is the corrected approach.

---

## Problem

Recurring `seed_state Unhealthy` / `RAG partial-failed` alert on staging. Root cause (corrected):

`SeedBadswormPersonaCommandHandler` (#971) seeds **mock placeholder** `pdf_documents` rows for the Badsworm dogfood demo (Aaron's library) at `seed/badsworm/<game>/rulebook.pdf`, with **no real blob** and **no `text_chunks`**, in deliberate KB-lifecycle states for the dashboard: 6 `Ready`, 7 Wonders `Embedding`, Spirit Island `Pending`.

These collide with the RAG pipeline health infrastructure:
1. `StalePdfRecoveryService` (startup) picks up the `Pending`/`Embedding` mocks as "stale pre-Ready", tries to process them, fails on the missing blob → flips them to `Failed`.
2. `SeedStateHealthCheck` then sees `pdf_failed > 0` → derives `partial_failed` → `/health` Degraded → alert.

Even without the flip, the deliberate `Pending`/`Embedding` mock states mean `seed_state` can never be a clean `ready`.

## Decision — exclude the mocks, do not delete them

The mocks are legitimate, actively-maintained demo fixtures (idempotency gate is `UserLibraryEntries >= 10`, so once deleted they are never recreated). The fix makes the RAG-readiness infrastructure **ignore** them.

### Marker (single source of truth)
`PdfDocumentEntity.DemoMockFilePathPrefix = "seed/"` — real content always lives under `pdfs/{id}/…`; the `seed/` prefix is exclusively the demo-mock scheme. `SeedBadswormPersonaCommandHandler` builds its `FilePath` from this const, and both consumers filter on it, so there is no silent drift.

### Changes
1. **`PdfDocumentEntity`**: add `public const string DemoMockFilePathPrefix = "seed/"` (documented).
2. **`SeedBadswormPersonaCommandHandler`**: build `FilePath` from the const.
3. **`StalePdfRecoveryService.FindStalePdfsAsync`**: `.Where(p => !p.FilePath.StartsWith(prefix))` — never force-process a mock (so it never flips to `Failed`).
4. **`SeedStateHealthCheck`**: exclude the prefix from `pdf_total` / `pdf_ready` / `pdf_failed` counts — mocks are not real corpus content, so `seed_state` reflects only real RAG state. (`chunk_count`/`embedding_count` already unaffected — mocks have none.)

Net effect: mocks stay in their demo states, are never disturbed, and `seed_state` is `ready` when the real corpus is ready.

## Testing
- **`SeedStateHealthCheckDbBoundTests`**: a `seed/` mock in `Failed` state + a real Ready PDF with chunks → `seed_state=ready`, `pdf_failed=0`, `pdf_total=1` (mock excluded).
- **`StalePdfRecoveryServiceTests`**: a `seed/` mock in `Embedding`, 2h old → `FindStalePdfsAsync` returns empty (excluded).
- Existing count-semantics (#2675) + orphan-cleanup (#2907/#2908) tests unaffected (their fixtures use `test/…` / `pdfs/…` paths).

## Out of scope
- Deleting/repairing the mocks (they are intentional).
- The two duplicate #2907/#2908 seeder implementations (`SeedMaintenanceSeeder` + `OrphanPdfCleanupSeeder`) running back-to-back — pre-existing tech debt from a merge collision, unrelated.

## Staging remediation (follow-up, not code)
7 Badsworm mocks were manually deleted from staging during the (mis)diagnosis. After this fix deploys, re-insert them (mirroring the handler) so the demo is restored and — with the exclusion in place — they no longer re-trigger the alert.
