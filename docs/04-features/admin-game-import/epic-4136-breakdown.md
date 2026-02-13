# Epic #4136: PDF Wizard per SharedGameCatalog con BGG Integration

> **Epic GitHub**: [#4136](https://github.com/DegrassiAaron/meepleai-monorepo/issues/4136) - ✅ **COMPLETED**
> **Created**: 2026-02-12
> **Completed**: 2026-02-12
> **Total Issues**: 15 wizard issues (All completed)
> **Timeline**: Completed in 1 day

---

## 📊 Epic Status - ✅ COMPLETED

| Phase | Issues | Status |
|-------|--------|--------|
| **Phase 1: Wizard Backend** | 7 issues | ✅ COMPLETED |
| **Phase 2: Wizard Frontend** | 8 issues | ✅ COMPLETED |
| **Phase 3: Bulk Import** | Deferred | ⚠️ NOT NEEDED FOR MVP |

**Epic Status**: ✅ **CLOSED** - All wizard functionality delivered

---

## 🎯 Phase 1: Wizard Backend (P0 - Critical)

### Issue #1: Backend - Upload PDF Command & Handler
**Priority**: P0 - Critical
**Duration**: 1 giorno
**Depends On**: None

**Description**:
Creare comando e handler per upload PDF temporaneo con validazione preliminare.

**Tasks**:
- [ ] `UploadPdfForGameExtractionCommand` + DTO
- [ ] `UploadPdfForGameExtractionCommandHandler`
- [ ] `UploadPdfForGameExtractionCommandValidator`
- [ ] Upload PDF su blob storage temporaneo (S3 o locale)
- [ ] Validazione: formato PDF, max 50MB, non corrotto
- [ ] Unit tests (handler + validator)
- [ ] Integration test (upload + storage)

**Acceptance Criteria**:
- ✅ Upload PDF ≤50MB funzionante
- ✅ Validazione formato e dimensione
- ✅ Storage temporaneo con cleanup automatico (24h)
- ✅ Test coverage ≥90%

---

### Issue #2: Backend - Extract Game Metadata Query
**Priority**: P0 - Critical
**Duration**: 1-2 giorni
**Depends On**: #1

**Description**:
Creare query per estrarre metadati gioco da PDF usando SmolDocling + AI parsing.

**Tasks**:
- [ ] `ExtractGameMetadataFromPdfQuery` + handler
- [ ] `GameMetadataDto` (Title, Year, Players, Age, PlayingTime, Description, ConfidenceScore)
- [ ] Integrazione SmolDocling per OCR
- [ ] AI parsing (OpenRouter) per strutturare dati
- [ ] Confidence scoring (0.0 - 1.0)
- [ ] Fallback: ritorna campi vuoti con confidence 0.0 se extraction fallisce
- [ ] Unit tests + integration tests
- [ ] Mock SmolDocling per test veloci

**Acceptance Criteria**:
- ✅ Estrazione metadati da PDF funzionante
- ✅ Confidence score accurato (≥0.7 per alta qualità)
- ✅ Fallback graceful se extraction fallisce
- ✅ Test coverage ≥90%

---

### Issue #3: Backend - BGG Match & Enrichment Command
**Priority**: P0 - Critical
**Duration**: 1 giorno
**Depends On**: #2

**Description**:
Creare comando per arricchire metadati con dati BGG e rilevare conflitti.

**Tasks**:
- [ ] `EnrichGameMetadataFromBggCommand` + handler
- [ ] `EnrichedGameDto` (merge BGG + extracted data)
- [ ] Conflict detection (es: MinPlayers BGG≠PDF)
- [ ] Integration con `IBggApiClient` esistente
- [ ] Unit tests + integration tests (mock BGG API)

**Acceptance Criteria**:
- ✅ Merge BGG + PDF data funzionante
- ✅ Conflict detection accurato
- ✅ Gestione errori BGG API (timeout, not found)
- ✅ Test coverage ≥90%

---

### Issue #4: Backend - Wizard Endpoints Routing
**Priority**: P0 - Critical
**Duration**: 1 giorno
**Depends On**: #1, #2, #3

**Description**:
Creare endpoint REST per wizard flow.

**Tasks**:
- [ ] `POST /api/v1/admin/games/wizard/upload-pdf` → UploadPdfForGameExtractionCommand
- [ ] `POST /api/v1/admin/games/wizard/extract-metadata` → ExtractGameMetadataFromPdfQuery
- [ ] `POST /api/v1/admin/games/wizard/enrich-from-bgg` → EnrichGameMetadataFromBggCommand
- [ ] `POST /api/v1/admin/games/wizard/confirm-import` → ImportGameFromBggCommand (existing)
- [ ] Authorization: Admin + Editor roles only
- [ ] Integration tests (endpoint → handler)

**Acceptance Criteria**:
- ✅ 4 endpoint funzionanti
- ✅ Authorization corretto (Admin/Editor)
- ✅ Error handling consistente
- ✅ Swagger documentation completa

---

### Issue #5: Backend - Duplicate Detection Enhancement
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: #3

**Description**:
Migliorare detection duplicati con fuzzy matching titolo + BGG ID.

**Tasks**:
- [ ] `CheckDuplicateGameQuery` enhancement (già esiste `CheckBggDuplicateQuery`)
- [ ] Fuzzy matching titolo (Levenshtein distance)
- [ ] Check BggId esistente
- [ ] Warning DTO con suggerimenti merge
- [ ] Unit tests + integration tests

**Acceptance Criteria**:
- ✅ Duplicate detection accurato (fuzzy + BggId)
- ✅ Warning chiaro all'utente
- ✅ Suggerimenti merge actionable
- ✅ Test coverage ≥90%

---

### Issue #6: Backend - Approval Workflow Extension
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: #4

**Description**:
Estendere approval workflow per supportare Editor → Draft, Admin → Published.

**Tasks**:
- [ ] Verifica `SharedGame.SubmitForApproval()` esistente
- [ ] Event `SharedGameSubmittedForApprovalEvent`
- [ ] Email notification admin quando Editor submits
- [ ] Unit tests workflow
- [ ] Integration tests (Editor creates → Admin approves)

**Acceptance Criteria**:
- ✅ Editor può creare gioco in Draft
- ✅ Admin riceve notifica submission
- ✅ Admin può approve → Published
- ✅ Test coverage ≥90%

---

### Issue #7: Backend - Wizard Integration Tests
**Priority**: P0 - Critical
**Duration**: 1 giorno
**Depends On**: All Phase 1 issues

**Description**:
Test end-to-end wizard backend flow.

**Tasks**:
- [ ] E2E test: Upload PDF → Extract → Enrich → Import
- [ ] Test failure scenarios (OCR fail, BGG timeout, duplicate)
- [ ] Test concurrent uploads (race conditions)
- [ ] Performance test (PDF 50MB upload + extraction)
- [ ] Load test: 10 concurrent wizard sessions

**Acceptance Criteria**:
- ✅ E2E test completo funzionante
- ✅ All failure scenarios coperti
- ✅ Performance acceptable (extraction <30s)
- ✅ No memory leaks

---

## 🎨 Phase 2: Wizard Frontend (P0 - Critical)

### Issue #8: Frontend - Wizard Container & State Management
**Priority**: P0 - Critical
**Duration**: 1 giorno
**Depends On**: #4

**Description**:
Creare container wizard con Zustand store per state management.

**Tasks**:
- [ ] `AdminGameImportWizard.tsx` (container)
- [ ] `useGameImportWizardStore` (Zustand)
- [ ] State: uploadedPdf, extractedMetadata, selectedBggId, enrichedData
- [ ] Actions: setUploadedPdf, setExtractedMetadata, setSelectedBggId, resolveConflicts
- [ ] Navigation: step tracking, next/back, validation gates
- [ ] Route: `/admin/games/import/wizard`
- [ ] Component tests (state transitions)

**Acceptance Criteria**:
- ✅ Wizard container funzionante
- ✅ State management completo
- ✅ Navigation step-by-step
- ✅ Test coverage ≥85%

---

### Issue #9: Frontend - Step 1: Upload PDF
**Priority**: P0 - Critical
**Duration**: 1 giorno
**Depends On**: #8

**Description**:
Creare step 1 upload PDF con drag-drop e validazione client-side.

**Tasks**:
- [ ] `Step1UploadPdf.tsx` component
- [ ] Drag & drop area (react-dropzone)
- [ ] Client validation: formato PDF, max 50MB
- [ ] Preview thumbnail PDF prima pagina
- [ ] Progress bar upload
- [ ] API hook: `useUploadPdf` (React Query)
- [ ] Error handling (file troppo grande, formato errato)
- [ ] Component tests

**Acceptance Criteria**:
- ✅ Drag-drop funzionante
- ✅ Validazione client-side
- ✅ Upload con progress bar
- ✅ Error messages chiari
- ✅ Test coverage ≥85%

---

### Issue #10: Frontend - Step 2: Metadata Extraction
**Priority**: P0 - Critical
**Duration**: 1 giorno
**Depends On**: #9

**Description**:
Creare step 2 estrazione metadati con AI confidence e editing manuale.

**Tasks**:
- [ ] `Step2MetadataExtraction.tsx` component
- [ ] Loading state con progress animation (AI extraction in corso)
- [ ] Display extracted metadata: Title, Year, Players, Age, PlayingTime, Description
- [ ] Confidence badge (≥80% verde, 50-79% giallo, <50% rosso)
- [ ] Editable fields per correzione manuale
- [ ] API hook: `useExtractMetadata` (React Query)
- [ ] Component tests

**Acceptance Criteria**:
- ✅ Estrazione con loading UX
- ✅ Display metadati estratti
- ✅ Confidence badge visualizzato
- ✅ Editing manuale funzionante
- ✅ Test coverage ≥85%

---

### Issue #11: Frontend - Step 3: BGG Match
**Priority**: P0 - Critical
**Duration**: 1-2 giorni
**Depends On**: #10

**Description**:
Creare step 3 search BGG con match results e selezione.

**Tasks**:
- [ ] `Step3BggMatch.tsx` component
- [ ] Search input (pre-filled con extracted title)
- [ ] API hook: `useSearchBggGames` (React Query)
- [ ] Results list con match percentage
- [ ] Result card: thumbnail, title, year, BGG ID, match score
- [ ] Manual BGG ID input (se search non trova)
- [ ] Selected highlight state
- [ ] Component tests

**Acceptance Criteria**:
- ✅ Search BGG funzionante
- ✅ Results display con match score
- ✅ Selezione gioco
- ✅ Manual BGG ID input
- ✅ Test coverage ≥85%

---

### Issue #12: Frontend - Step 4: Enrich & Confirm
**Priority**: P0 - Critical
**Duration**: 1-2 giorni
**Depends On**: #11

**Description**:
Creare step 4 merge dati BGG+PDF, conflict resolution e preview finale.

**Tasks**:
- [ ] `Step4EnrichAndConfirm.tsx` component
- [ ] API hook: `useEnrichFromBgg` (React Query)
- [ ] Conflict resolution UI (BGG vs PDF, select winner)
- [ ] Final preview card (MeepleCard)
- [ ] Confirm button → `useImportGame` (existing)
- [ ] Success state con redirect to game detail
- [ ] Component tests

**Acceptance Criteria**:
- ✅ Merge BGG+PDF data
- ✅ Conflict resolution UI
- ✅ Final preview accurato
- ✅ Import funzionante
- ✅ Test coverage ≥85%

---

### Issue #13: Frontend - Wizard Navigation & Progress
**Priority**: P0 - Critical
**Duration**: 0.5 giorni
**Depends On**: #8, #9, #10, #11, #12

**Description**:
Integrare WizardSteps component e navigation controls.

**Tasks**:
- [ ] Integra `WizardSteps` component (already exists)
- [ ] Progress bar (0% → 25% → 50% → 75% → 100%)
- [ ] Next/Back buttons con validation
- [ ] Step validation gates (non può avanzare se step incompleto)
- [ ] Breadcrumb navigation
- [ ] Component tests

**Acceptance Criteria**:
- ✅ Progress indicator funzionante
- ✅ Navigation Next/Back
- ✅ Validation gates rispettate
- ✅ UX smooth

---

### Issue #14: Frontend - Error Handling & Edge Cases
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: All Phase 2 issues

**Description**:
Gestione errori completa e edge cases UX.

**Tasks**:
- [ ] Network error handling (retry logic)
- [ ] BGG API timeout handling
- [ ] OCR extraction failure (fallback manual input)
- [ ] Duplicate warning modal
- [ ] Session timeout handling (auto-save draft)
- [ ] Error boundary per wizard
- [ ] Component tests edge cases

**Acceptance Criteria**:
- ✅ Error messages chiari all'utente
- ✅ Retry automatico dove appropriato
- ✅ Fallback manual input se AI fallisce
- ✅ No crash, sempre recoverable

---

### Issue #15: Frontend - E2E Tests (Playwright)
**Priority**: P0 - Critical
**Duration**: 1 giorno
**Depends On**: All Phase 2 issues

**Description**:
Test E2E completo wizard flow con Playwright.

**Tasks**:
- [ ] E2E: Upload PDF → Extract → BGG Match → Confirm → Success
- [ ] E2E: Duplicate detection warning
- [ ] E2E: Manual BGG ID input
- [ ] E2E: Conflict resolution
- [ ] E2E: Editor submit → Admin approve
- [ ] Visual regression tests (Percy o Playwright snapshots)

**Acceptance Criteria**:
- ✅ E2E test completo funzionante
- ✅ All user flows coperti
- ✅ Visual regression baseline
- ✅ CI integration

---

## 📦 Phase 3: Bulk Import JSON (P1 - High)

### Issue #16: Backend - Bulk Import JSON Command
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: #4

**Description**:
Creare comando per bulk import da file JSON con best-effort strategy.

**Tasks**:
- [ ] `EnqueueBggBatchFromJsonCommand` + handler
- [ ] `BulkImportResult` DTO (total, enqueued, skipped, failed, errors)
- [ ] `BulkImportError` DTO (bggId, gameName, reason)
- [ ] JSON parsing + validation
- [ ] Duplicate check per ogni gioco
- [ ] Best-effort: skip duplicates, continua con i restanti
- [ ] Integration con `EnqueueBggBatchCommand` esistente
- [ ] Unit tests + integration tests

**Acceptance Criteria**:
- ✅ JSON parsing robusto
- ✅ Duplicate skip con log
- ✅ Best-effort strategy funzionante
- ✅ Test coverage ≥90%

---

### Issue #17: Backend - Bulk Import SSE Progress Endpoint
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: #16

**Description**:
Creare endpoint SSE per progress real-time bulk import.

**Tasks**:
- [ ] `GET /api/v1/admin/games/bulk-import/progress` (SSE)
- [ ] Integration con `BggImportQueueBackgroundService` esistente
- [ ] Stats DTO: total, completed, failed, current processing
- [ ] Update ogni 1 secondo
- [ ] Connection management (client disconnect handling)
- [ ] Integration tests

**Acceptance Criteria**:
- ✅ SSE endpoint funzionante
- ✅ Real-time updates ogni 1s
- ✅ Connection cleanup corretto
- ✅ Test coverage ≥90%

---

### Issue #18: Backend - Bulk Import Endpoint
**Priority**: P1 - High
**Duration**: 0.5 giorni
**Depends On**: #16, #17

**Description**:
Creare endpoint POST per bulk import.

**Tasks**:
- [ ] `POST /api/v1/admin/games/bulk-import` → EnqueueBggBatchFromJsonCommand
- [ ] Accept JSON body o IFormFile
- [ ] Authorization: Admin only (non Editor per bulk)
- [ ] Rate limiting: max 1 bulk import ogni 5 minuti
- [ ] Swagger documentation

**Acceptance Criteria**:
- ✅ Endpoint funzionante
- ✅ Authorization Admin only
- ✅ Rate limiting attivo
- ✅ Swagger docs completa

---

### Issue #19: Frontend - Bulk Import Upload UI
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: #18

**Description**:
Creare UI upload JSON per bulk import.

**Tasks**:
- [ ] `BulkImportJsonUploader.tsx` component
- [ ] Route: `/admin/games/import/bulk`
- [ ] Drag-drop JSON file
- [ ] Client validation: formato JSON, max 10MB
- [ ] JSON format example tooltip
- [ ] API hook: `useUploadBulkImportJson`
- [ ] Component tests

**Acceptance Criteria**:
- ✅ Upload JSON funzionante
- ✅ Validation client-side
- ✅ Format example chiaro
- ✅ Test coverage ≥85%

---

### Issue #20: Frontend - Bulk Import Preview & Validation
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: #19

**Description**:
Creare preview JSON con validazione pre-submit.

**Tasks**:
- [ ] `BulkImportPreview.tsx` component
- [ ] Display parsed games count
- [ ] Validation summary: valid, duplicates, errors
- [ ] Errors list con details
- [ ] Confirm import button
- [ ] Component tests

**Acceptance Criteria**:
- ✅ Preview JSON parsed
- ✅ Validation summary chiaro
- ✅ Error details actionable
- ✅ Test coverage ≥85%

---

### Issue #21: Frontend - Bulk Import Progress (SSE)
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: #20

**Description**:
Creare UI progress real-time con SSE subscription.

**Tasks**:
- [ ] `BulkImportProgress.tsx` component
- [ ] Hook: `useBulkImportProgress` (SSE EventSource)
- [ ] Progress bar (X/Y completed)
- [ ] Real-time stats: total, completed, failed, current
- [ ] Latest success list (rolling last 5)
- [ ] Cancel import button
- [ ] Component tests (mock SSE)

**Acceptance Criteria**:
- ✅ SSE connection funzionante
- ✅ Real-time progress display
- ✅ Cancel import operativo
- ✅ Test coverage ≥85%

---

### Issue #22: Frontend - Bulk Import Results Summary
**Priority**: P1 - High
**Duration**: 0.5 giorni
**Depends On**: #21

**Description**:
Creare results summary con report downloadable.

**Tasks**:
- [ ] `BulkImportResults.tsx` component
- [ ] Stats summary: total, imported, failed
- [ ] Errors table con details
- [ ] Download CSV report button
- [ ] New import button (reset wizard)
- [ ] Component tests

**Acceptance Criteria**:
- ✅ Results summary completo
- ✅ Errors table chiara
- ✅ CSV download funzionante
- ✅ Test coverage ≥85%

---

### Issue #23: Frontend - Bulk Import E2E Tests
**Priority**: P1 - High
**Duration**: 1 giorno
**Depends On**: #19, #20, #21, #22

**Description**:
Test E2E completo bulk import flow.

**Tasks**:
- [ ] E2E: Upload JSON → Preview → Import → Progress → Results
- [ ] E2E: Duplicate handling
- [ ] E2E: Errors display
- [ ] E2E: Cancel import mid-progress
- [ ] Visual regression tests

**Acceptance Criteria**:
- ✅ E2E test completo funzionante
- ✅ All user flows coperti
- ✅ Visual regression baseline
- ✅ CI integration

---

## 📋 Documentation Issues

### Issue #24: Documentation - API Reference
**Priority**: P2 - Medium
**Duration**: 0.5 giorni
**Depends On**: All backend issues

**Description**:
Documentare API endpoints wizard + bulk import.

**Tasks**:
- [ ] Swagger annotations complete
- [ ] API reference in `docs/03-api/`
- [ ] Postman collection examples
- [ ] Error codes documentation

---

### Issue #25: Documentation - User Guide
**Priority**: P2 - Medium
**Duration**: 0.5 giorni
**Depends On**: All frontend issues

**Description**:
Documentare user guide admin game import.

**Tasks**:
- [ ] Admin guide: wizard flow con screenshots
- [ ] Admin guide: bulk import con JSON examples
- [ ] Troubleshooting section
- [ ] Best practices

---

## 🎯 Implementation Priority Order

### Week 1 (Critical Path)
1. Issue #1 (Upload PDF)
2. Issue #2 (Extract Metadata)
3. Issue #3 (BGG Enrichment)
4. Issue #4 (Endpoints)
5. Issue #8 (Wizard Container)
6. Issue #9 (Step 1 Upload)
7. Issue #10 (Step 2 Extract)

### Week 2 (Critical Path)
8. Issue #11 (Step 3 BGG Match)
9. Issue #12 (Step 4 Confirm)
10. Issue #13 (Navigation)
11. Issue #14 (Error Handling)
12. Issue #7 (Backend E2E)
13. Issue #15 (Frontend E2E)

### Week 3 (Bulk Import)
14. Issue #16 (Bulk Command)
15. Issue #17 (SSE Progress)
16. Issue #18 (Bulk Endpoint)
17. Issue #19 (Bulk Upload UI)
18. Issue #20 (Bulk Preview)

### Week 4 (Bulk Import + Polish)
19. Issue #21 (Bulk Progress)
20. Issue #22 (Bulk Results)
21. Issue #23 (Bulk E2E)
22. Issue #5 (Duplicate Detection)
23. Issue #6 (Approval Workflow)
24. Issue #24, #25 (Documentation)

---

## ✅ Success Metrics

**Quality**:
- Backend test coverage ≥90%
- Frontend test coverage ≥85%
- E2E tests green
- No critical bugs

**Performance**:
- PDF upload <5s (50MB)
- Metadata extraction <30s
- BGG API match <2s
- Bulk import: 100 games in <5 minutes

**UX**:
- Wizard completion rate >80%
- Error recovery rate >90%
- User satisfaction >4/5

---

## 🔗 Related Documentation

- Epic #4136: [GitHub Issue](https://github.com/DegrassiAaron/meepleai-monorepo/issues/4136)
- DocumentProcessing BC: `apps/api/src/Api/BoundedContexts/DocumentProcessing/README.md`
- SharedGameCatalog BC: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/`
- BGG Integration Guide: `docs/02-development/bgg-import-queue-implementation.md`
- Wizard Component Pattern: `apps/web/src/components/wizard/WizardSteps.tsx`
