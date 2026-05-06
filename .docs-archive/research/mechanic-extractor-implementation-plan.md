# Mechanic Extractor — Implementation Plan

## Overview
Admin page implementing Variant C (human + AI assistant) workflow to extract game mechanics from rulebook PDFs and produce copyright-compliant `RulebookAnalysis` entries.

**Branch**: `feature/mechanic-extractor` (parent: `main-dev`)
**Route**: `/admin/knowledge-base/mechanic-extractor`

## Implementation Phases

### Phase 1: Backend Domain & Commands
1. **MechanicDraft entity** — New entity in SharedGameCatalog with notes/draft fields per section
2. **Repository + EF Config** — `IMechanicDraftRepository`, EF mapping, migration
3. **SaveMechanicDraftCommand** — Create/update draft (auto-save)
4. **GetMechanicDraftQuery** — Load draft for game
5. **AiAssistMechanicDraftCommand** — Send human notes to LLM, receive original text
6. **FinalizeMechanicAnalysisCommand** — Convert draft to RulebookAnalysis.CreateManual()
7. **Admin endpoints** — AdminMechanicExtractorEndpoints.cs with RequireAdminSession

### Phase 2: Frontend
8. **Page route + NavConfig** — /admin/knowledge-base/mechanic-extractor
9. **API client + Zod schemas** — mechanicExtractorClient methods + schemas
10. **SplitPanelLayout + PdfViewerPanel** — Resizable split, client-side PDF viewer
11. **MechanicEditorPanel + tabs** — 6 tabs with notes/draft UI
12. **AiAssist + DraftPreview** — AI assist button, accept/reject flow, auto-save
13. **Finalize + Review** — Save & Activate, review page

### Phase 3: Testing
14. **Backend unit tests**
15. **Frontend component tests**
