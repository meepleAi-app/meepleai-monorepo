# SP5 Admin KB — F3-FU-4: doc-panel actions + chunks similarity-search

**Issue**: [#1653](https://github.com/meepleAi-app/meepleai-monorepo/issues/1653) (P3, area/backend + area/frontend)
**Date**: 2026-05-29
**Status**: REVIEWED (spec-panel 2026-05-29) — scope confirmed, ready for plan
**Parent**: F3.1 Explorer (PR #1649) · siblings FU-1 #1650 ✅, FU-2 #1651 ✅, FU-3 #1652 ✅, FU-5 #1654, FU-6 #1655
**Mockup**: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb.html` (lines 127-244)

---

## 1. Context & Goal

`KbDocDetailPanel` (right pane of `/admin/knowledge-base` explorer) renders a read-only hero +
chunk list (Overview), Ingestion-log (FU-1) and Used-by (FU-2) tabs. It has **no operational
actions** on the selected document.

**Goal**: let admins act on a KB document from the explorer — re-index, download the original,
delete — plus in-panel chunk discovery via similarity search.

## 2. Current state (evidence)

- `docId` in `KbTree` == **PdfDocumentId** (`AdminKnowledgeBaseEndpoints.cs:85` comment).
- `useKbDocDetail` → `GET /api/v1/kb-docs/{docId}` → `KbDocDetailSchema` (`kb-chunks.schemas.ts:33`).
- `KbDocDetailPanel.tsx` early-returns a "Documento in elaborazione" banner when `status === 'locked'`
  **before** the Overview hero renders (inherited #1649). Used-by tab is allowed through during locked.
- `DeletePdfCommand` already exists (`DocumentProcessing` BC) — **hard delete**: removes PdfDocument
  record + VectorDocument (pgvector) + physical blob + invalidates cache. Wired today only at
  `DELETE /admin/sandbox/pdfs/{id}`. **Does NOT touch TextChunks, and does NOT handle agents that
  reference the doc** (dangling KbCardIds).
- `ReindexDocumentCommand` exists (`POST /admin/pdfs/{pdfId}/reindex`) — deletes chunks, resets to
  Pending, re-enqueues. **No guard against re-indexing a doc already processing.**
- `PdfDocumentEntity` carries `FileSizeBytes`, `LanguageConfidence`, `VersionLabel` — but **not**
  indexer-version, chunk-level avg-confidence, OCR flag, or chunk-token-size.
- `vector-search` (`POST /admin/kb/vector-search`) filters by **GameId, not docId**.

## 3. Scope (confirmed with user 2026-05-29)

Surface = **hero buttons** (mockup-faithful). Phasing decision = **Phase 1 + Phase 2 together**.

### In scope (FU-4)
- **A1 Re-index** — wire existing `POST /admin/pdfs/{docId}/reindex` ✅
- **A2 Download original** — wire existing `GET /pdfs/{docId}/download` ✅
- **A3 Delete** — NEW `DELETE /api/v1/admin/pdfs/{docId}` (reuse `DeletePdfCommand`) + typed-confirm + agent-reference handling (see FR-3 / D-1)
- **B5 Used-by link** — link to existing Used-by tab (FU-2) ✅
- **B3 Export chunks JSON** — NEW export endpoint (full chunk content)
- **C Similarity-search in doc** — extend vector-search with a per-doc filter
- **O-1 mitigation** — restructure the `locked` early-return so Re-index/Delete reach `failed`/`processing` docs

### Spun out of FU-4 (net-new backend features → separate sub-issues of #1653)
- **B1 Re-index w/ version** ([#1673](https://github.com/meepleAi-app/meepleai-monorepo/issues/1673)) — needs versioned indexer (no foundation)
- **B2 View embeddings** ([#1674](https://github.com/meepleAi-app/meepleai-monorepo/issues/1674)) — needs per-doc embeddings API (no foundation; corpus-leak risk per Newman)
- **B4 Quality eval** ([#1675](https://github.com/meepleAi-app/meepleai-monorepo/issues/1675)) — needs per-doc eval pipeline (only global `/admin/rag-quality/report` exists)
- **D Hero metadata enrichment** ([#1676](https://github.com/meepleAi-app/meepleai-monorepo/issues/1676)) — DTO + data extension (Phase 3; some fields absent on entity)

### Out of scope
- KB tool-pages re-skin (FU-3), Preview/PDF-viewer (FU-5), SubNav badges (FU-6), cross-doc bulk actions.

## 4. Backend feasibility map (post-recon)

| # | Action | Endpoint | Status |
|---|--------|----------|--------|
| A1 | Re-index | `POST /api/v1/admin/pdfs/{docId}/reindex` | ✅ exists |
| A2 | Download | `GET /api/v1/pdfs/{docId}/download` | ✅ exists |
| A3 | Delete | NEW `DELETE /api/v1/admin/pdfs/{docId}` reusing `DeletePdfCommand` | ⚠️ endpoint + agent-ref handling |
| B3 | Export chunks JSON | NEW `GET /api/v1/admin/kb/docs/{docId}/chunks/export` (full content) | ⚠️ new endpoint |
| B5 | Used-by link | FU-2 `/admin/kb/docs/{docId}/agents` | ✅ done |
| C | Similarity-search in doc | extend `VectorSemanticSearchQuery` with `DocId`/`PdfId` filter | ⚠️ query + endpoint param |

## 5. Functional requirements (testable — Adzic/Wiegers)

**FR-1 Re-index**
```
Given a doc in state ready|failed
When admin clicks ⟳ Re-index
Then POST /admin/pdfs/{docId}/reindex is called; button enters disabled/pending;
  on 2xx a success toast shows and doc detail + tree queries invalidate (doc → processing);
  on error an error toast shows and the button re-enables.
Given a doc already in state processing|queued
Then the Re-index button is disabled (guard for O-5; backend has no guard).
```

**FR-2 Download**
```
Given a doc with a stored original
When admin clicks ⤓ Download
Then the browser downloads the original PDF via GET /pdfs/{docId}/download.
Given the blob is missing (404) or forbidden (403)
Then an error toast shows; no silent failure.
```

**FR-3 Delete** (destructive)
```
Given a doc "Wingspan-Oceania-EN.pdf" referenced by N agents (N from Used-by/FU-2)
When admin clicks 🗑 Delete
Then a typed-confirm dialog opens showing the impact ("referenced by N agents") and requires
  typing the filename to enable Confirm.
When admin confirms
Then DELETE /admin/pdfs/{docId} hard-deletes the doc (record + vectors + blob + chunks),
  the selection clears, the tree refreshes, and an audit entry is written (actor, docId, N).
```
> **D-1 (decision required at plan three-amigos)**: agent-reference behavior on delete —
> (a) block delete while N>0, (b) warn + cascade-remove the doc from the N agents' KbCards,
> (c) warn-only and leave dangling refs (current `DeletePdfCommand` behavior). The existing
> command does **not** clean agent refs → (c) is the as-is risk; recommend (a) or (b).
> Also confirm: does delete need to remove `TextChunks` (current handler only removes `VectorDocuments`)?

**FR-4 Export chunks JSON**
```
Given a ready doc with M chunks
When admin clicks ⤓ Export chunks JSON
Then the full chunk set (id, position, headingPath, page, full content) downloads as
  {docId}-chunks.json. (Snippet-only FE assembly is rejected — lossy; needs a real endpoint.)
```

**FR-5 Similarity-search in doc**
```
Given a ready doc and a query "predator activation"
When admin types in the in-panel search box
Then a semantic search scoped to THIS doc's chunks runs (vector-search + docId filter),
  results show score + page + snippet, sorted by score desc;
  a score-threshold filter (e.g. ≥0.7) narrows results;
  empty result set shows an empty state; 0-chunk doc disables the search box.
```

## 6. UX & states

- **Authorization**: admin/superadmin only (explorer already admin-gated). New delete/export endpoints
  enforce admin + audit.
- **Locked-state (O-1 resolved → restructure)**: actions render for `ready`, `failed`, and `processing`
  docs. Refactor `KbDocDetailPanel` so the hero + action bar render before/around the locked banner
  (pattern already used for the Used-by tab during locked). Re-index disabled while processing (FR-1).
- **Destructive Delete**: typed-confirm (type filename), impact line with agent count, `bg-destructive
  text-destructive-foreground` (WCAG AA), never silent.
- **Refresh**: pessimistic — await, then invalidate TanStack queries (doc detail, tree, chunks).

## 7. Non-functional

- **Audit**: Delete + Re-index write audit entries (Administration BC). Verify existing commands audit;
  add if missing.
- **a11y**: net-new `<button>` → `type="button"` + `focus-visible:outline-none focus-visible:ring-2
  focus-visible:ring-ring focus-visible:ring-offset-2` (FU-1/FU-3 convention).
- **Security**: download endpoint already enforces owner/admin. New delete/export endpoints: admin gate
  + audit. Similarity-search reuses existing admin vector-search auth.
- **Tokens/ESLint**: amber/emerald/rose hue OK without disable; zinc dark keeps the file-level disable
  already in `KbDocDetailPanel`.

## 8. Test plan

- **Unit (Vitest)**: each action button renders + calls correct hook + disabled-while-pending +
  success/error toast; Re-index disabled when processing; typed-confirm gating + impact line for delete;
  similarity-search result render + score filter + empty state + 0-chunk disabled.
- **Backend (xUnit)**: `DELETE /admin/pdfs/{docId}` (happy, not-found, authorization, audit, agent-ref
  behavior per D-1); vector-search docId-filter extension; chunks export (full content, not-found, auth).
- **E2E smoke (Playwright)**: select doc → re-index → processing; select failed doc → actions reachable;
  select doc → delete (typed-confirm) → removed from tree.

## 9. Phasing (final)

- **FU-4 (this slice)** = Phase 1 + Phase 2: A1, A2, A3 (+endpoint), B5, B3 (+endpoint), C (+query ext),
  O-1 restructure. One feature branch, may split into 2 PRs (BE endpoints + FE wiring) if review size warrants.
- **Spun out as #1653 sub-issues** (do NOT block FU-4): B1 versioned reindex (#1673), B2 per-doc
  embeddings (#1674), B4 per-doc quality eval (#1675), D hero metadata enrichment (#1676).

## 10. Open questions — status

- **O-1** ✅ RESOLVED: restructure locked early-return (user-confirmed).
- **O-2** → **D-1** (plan three-amigos): delete agent-reference behavior + TextChunks cascade.
- **O-3** ℹ️ INFORMATIONAL (D deferred): `FileSizeBytes` present; indexer-version/avg-confidence/OCR/
  chunk-token absent → Phase 3 must add data, not just DTO.
- **O-4** ✅ RESOLVED: export = full content via new endpoint (snippet FE-assembly rejected, lossy).
- **O-5** ✅ RESOLVED (FE guard): disable Re-index while processing/queued (backend has no guard).

## Appendix — spec-panel review provenance (2026-05-29)

Critique mode, focus requirements + architecture + testing. Quality (candidate): Completeness 7,
Testability 3→ (raised via §5 AC), Architecture clarity 5 (scope too broad → phased), Risk 6.

Convergent (Fowler+Newman+Nygard+Cockburn): "full mockup in one slice" not reviewable; B1/B2/B4 are
net-new backend (B2 corpus-leak risk) → spun out. Productive tension: mockup-fidelity vs failure-mode
(hero actions unreachable on failed docs) → resolved by O-1 restructure. Wiegers/Adzic: FRs needed
testable Given/When/Then → §5. Crispin: edge cases (delete referenced doc, reindex-while-processing,
missing blob, 0-chunk search) → §8. Gregory: O-2/O-3 were blocking → investigated; O-2→D-1, O-3 informational.
