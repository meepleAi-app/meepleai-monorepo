# Implementation Plan: PDF Rulebook Processing Enhancement (Phases 1-4)

**Date**: 2026-03-08
**Epic**: #5442
**PRD**: `docs/plans/2026-03-08-pdf-rulebook-processing-prd.md`
**Base Branch**: `main-dev`

---

## Codebase Analysis Summary

### Existing State (Key Findings)

| Component | Current State | Impact on Plan |
|-----------|--------------|----------------|
| `PdfDocument.DocumentType` | Already exists as `string` (base/expansion/errata/homerule) | Rename to avoid collision with new `DocumentCategory` enum |
| `LlmRulebookAnalyzer` stubs | Partially implemented (call LLM but have Italian fallbacks) | Need fix, not full rewrite |
| Routing threshold | 30,000 chars (`BackgroundAnalysisOptions.LargeRulebookThreshold`) | Replace with complexity score |
| RAG retrieval | `HybridSearchEngine` (BM25 + Vector + RRF + Reranker) | Add structured retrieval as 3rd source |
| Intent classifier | `RoutingLlmPlugin` with rule-based fallback (rules/resources/strategy/setup/learning) | Extend with glossary/faq/victory intents |
| `RulebookAnalysis` entity | Has KeyMechanics, VictoryConditions, Resources, GamePhases, CommonQuestions | Add KeyConcepts, GeneratedQuestions, GameStateSchema JSONB columns |
| Processing pipeline | Always async via `IBackgroundTaskService` | Add priority queue ordering |

### Key File Paths

```
DocumentProcessing BC:
  Entity:     apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs
  Enums:      .../Domain/Enums/PdfProcessingState.cs, ErrorCategory.cs
  DTO:        .../Application/DTOs/PdfDocumentDto.cs
  EF Entity:  apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs
  EF Config:  .../Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs
  Upload:     .../Application/Commands/UploadPdfCommandHandler.cs
  Pipeline:   .../Application/Services/PdfProcessingPipelineService.cs

SharedGameCatalog BC:
  Entity:     .../SharedGameCatalog/Domain/Entities/RulebookAnalysis.cs
  Analyzer:   .../Application/Services/LlmRulebookAnalyzer.cs (3 stubs)
  Interface:  .../Application/Services/IRulebookAnalyzer.cs
  Repository: .../Domain/Repositories/IRulebookAnalysisRepository.cs
  Orchestrator: .../Application/Services/BackgroundAnalysis/BackgroundRulebookAnalysisOrchestrator.cs
  Routing:    .../Application/Commands/AnalyzeRulebookCommandHandler.cs (30K char threshold)
  DI:         .../Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs
  Config:     .../Application/Configuration/BackgroundAnalysisOptions.cs

KnowledgeBase BC (RAG):
  VectorDoc:  .../KnowledgeBase/Domain/Entities/VectorDocument.cs
  HybridSearch: .../Domain/Services/ContextEngineering/HybridSearchEngine.cs
  Retrieval:  .../Application/Services/Reranking/ResilientRetrievalService.cs
  Routing:    .../Domain/Plugins/Implementations/Routing/RoutingLlmPlugin.cs
  AskHandler: .../Application/Handlers/AskQuestionQueryHandler.cs
  Qdrant:     .../Infrastructure/External/QdrantVectorStoreAdapter.cs

Frontend:
  PdfUpload:  apps/web/src/components/admin/shared-games/PdfUploadSection.tsx
  AdminClient: apps/web/src/lib/api/admin-client.ts
  KB Pages:   apps/web/src/app/admin/(dashboard)/knowledge-base/
```

---

## Execution Strategy

### Sprint Allocation

```
Sprint 1 (Phase 1): Document Classification    ~2 weeks
  └── 5 issues (#5443-#5447), all backend+frontend

Sprint 2a (Phase 2): RulebookAnalysis Core      ~2 weeks (parallel with 2b)
  └── 4 issues (#5448-#5452): stubs, complexity routing, quality gates

Sprint 2b (Phase 3): Queue Management           ~2 weeks (parallel with 2a)
  └── 4 issues (#5455-#5458): priority, pause/cancel, rate limit, UI

Sprint 3 (Phase 2 cont): RAG Integration        ~1.5 weeks
  └── 2 issues (#5453-#5454): structured fusion, expansion context, results UI

Sprint 4 (Phase 4): Observability                ~1.5 weeks
  └── 3 issues (#5459-#5461): metrics, alerts, comparison tool
```

### Branch Strategy

```
main-dev
  └── feature/epic-5442-pdf-rulebook-phase1   (Phase 1: #5443-#5447)
        └── feature/issue-5443-document-category
        └── feature/issue-5444-base-document-id
        └── feature/issue-5445-language-detection
        └── feature/issue-5446-copyright-disclaimer
        └── feature/issue-5447-version-label-ui
  └── feature/epic-5442-pdf-rulebook-phase2   (Phase 2: #5448-#5454)
  └── feature/epic-5442-pdf-rulebook-phase3   (Phase 3: #5455-#5458)
  └── feature/epic-5442-pdf-rulebook-phase4   (Phase 4: #5459-#5461)
```

Each issue → feature branch from phase branch → PR to phase branch → phase branch PR to main-dev.

---

## Phase 1: Document Classification & Metadata

### Issue #5443 — DocumentCategory Enum + Migration

**Execution Order**: 1st (no dependencies)

**Files to Create/Modify**:

| Action | File | Changes |
|--------|------|---------|
| CREATE | `DocumentProcessing/Domain/Enums/DocumentCategory.cs` | Enum: Rulebook=0, Expansion=1, Errata=2, QuickStart=3, Reference=4, PlayerAid=5, Other=6 |
| MODIFY | `DocumentProcessing/Domain/Entities/PdfDocument.cs` | Add `DocumentCategory` property (default Rulebook), add to constructor + Reconstitute |
| MODIFY | `Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs` | Add `DocumentCategory` column (int, default 0) |
| MODIFY | `Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs` | Map column, add index |
| CREATE | Migration | `AddDocumentCategoryToPdfDocument` |
| MODIFY | `DocumentProcessing/Application/DTOs/PdfDocumentDto.cs` | Add `DocumentCategory` field |
| MODIFY | `SharedGameCatalog/Application/Commands/AnalyzeRulebookCommandHandler.cs` | Gate: skip if category not in {Rulebook, Expansion, Errata} |

**Critical Decision**: `PdfDocument` already has `DocumentType` as string. Options:
- **Option A** (recommended): Keep `DocumentType` for backward compat, add `DocumentCategory` as the new enum
- **Option B**: Migrate `DocumentType` to enum (breaking change, risky)

**Tests**:
- Unit: DocumentCategory values, pipeline routing gate
- Integration: Migration runs, existing data defaults to Rulebook

---

### Issue #5444 — BaseDocumentId FK

**Execution Order**: 2nd (after #5443)

**Files to Modify**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `PdfDocument.cs` | Add `BaseDocumentId: Guid?` property |
| MODIFY | `PdfDocumentEntity.cs` | Add `BaseDocumentId` column + navigation |
| MODIFY | `PdfDocumentEntityConfiguration.cs` | Self-referential FK, `OnDelete(SetNull)` |
| CREATE | Migration | `AddBaseDocumentIdToPdfDocument` |
| MODIFY | UploadPdfCommandHandler | Accept `BaseDocumentId` in command, validate same-game |

**Validation Rule**: BaseDocumentId must reference a PdfDocument with same `GameId` or `SharedGameId`.

---

### Issue #5445 — Language Detection

**Execution Order**: 3rd (independent of #5444)

**Files to Create/Modify**:

| Action | File | Changes |
|--------|------|---------|
| CREATE | `DocumentProcessing/Application/Services/ILanguageDetectionService.cs` | Interface |
| CREATE | `DocumentProcessing/Application/Services/LanguageDetectionService.cs` | Implementation using NTextCat or simple heuristic |
| MODIFY | `PdfDocument.cs` | Add `DetectedLanguage: string?` |
| MODIFY | `PdfDocumentEntity.cs` + Config | Add column |
| CREATE | Migration | `AddDetectedLanguageToPdfDocument` |
| MODIFY | `PdfProcessingPipelineService.cs` | Insert detection after extraction, before chunking |

**Language Detection Strategy**:
```
1. After text extraction, take first 2000 chars
2. Use Unicode script detection (fast, < 50ms):
   - CJK Unified Ideographs → reject
   - Arabic/Hebrew blocks → reject
   - Latin/Greek/Cyrillic → accept
3. For Latin script, use trigram frequency analysis for ISO 639-1 code
4. Supported: en, it, de, es, fr, pt, nl, pl, sv, da, no, fi, cs, hu, ro, hr, el
```

**NuGet**: Consider `NTextCat` (license-friendly) or implement simple trigram classifier.

---

### Issue #5446 — Copyright Disclaimer + IsActiveForRag

**Execution Order**: 4th (can parallel with #5445)

**Files to Modify**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `PdfDocument.cs` | Add `IsActiveForRag`, `CopyrightDisclaimerAcceptedAt`, `CopyrightDisclaimerAcceptedBy` |
| MODIFY | Entity + Config + Migration | 3 new columns |
| MODIFY | `QdrantVectorStoreAdapter.cs` | Add `IsActiveForRag` filter to search queries |
| CREATE | `PATCH /documents/{id}/active` endpoint | Toggle handler |
| CREATE | `POST /documents/{id}/accept-disclaimer` endpoint | Disclaimer acceptance |
| MODIFY | `UploadPdfCommandHandler.cs` | Require disclaimer before processing starts |

**RAG Filter Integration Point**: In `QdrantVectorStoreAdapter.SearchAsync()`, add payload filter for `IsActiveForRag=true`. Or filter at `VectorDocumentRepository` level before search.

---

### Issue #5447 — VersionLabel + Document Management UI

**Execution Order**: 5th (after all backend changes from #5443-#5446)

**Files to Create/Modify**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `PdfDocument.cs` | Add `VersionLabel: string?` (max 100) |
| MODIFY | Entity + Config + Migration | New column |
| CREATE | `PATCH /documents/{id}/category` endpoint | Category update with baseDocId + versionLabel |
| CREATE | `GET /games/{gameId}/documents` endpoint | List all documents for a game |
| CREATE | `apps/web/src/components/documents/DocumentCategorySelect.tsx` | Dropdown with icons |
| CREATE | `apps/web/src/components/documents/GameDocumentList.tsx` | List view with toggles |
| CREATE | `apps/web/src/components/documents/CopyrightDisclaimerCheckbox.tsx` | Required checkbox |
| CREATE | `apps/web/src/components/documents/ActiveForRagToggle.tsx` | Switch with optimistic UI |
| CREATE | `apps/web/src/components/documents/BaseDocumentSelector.tsx` | Dropdown for expansion linking |
| CREATE | `apps/web/src/components/documents/DocumentVersionLabel.tsx` | Inline-editable text |
| MODIFY | `PdfUploadSection.tsx` | Add category selector + disclaimer + version label |

**Single Migration**: Combine all Phase 1 column additions into one migration if implementing sequentially. If parallel branches, merge migrations carefully.

---

## Phase 2: Complete RulebookAnalysis Implementation

### Issue #5448 — ExtractKeyConcepts

**Execution Order**: 1st in Phase 2

**Files to Modify**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `RulebookAnalysis.cs` | Add `KeyConcepts: List<KeyConcept>` (JSONB backing field) |
| CREATE | `SharedGameCatalog/Domain/ValueObjects/KeyConcept.cs` | Term, Definition, Category, PageReference, Confidence |
| MODIFY | `LlmRulebookAnalyzer.cs` | Replace stub `ExtractKeyConceptsAsync()` with real LLM prompt |
| MODIFY | EF Entity + Config | JSONB column for KeyConcepts |
| CREATE | Migration | `AddKeyConcepts_GeneratedQuestions_StateSchema` (combine with #5449, #5450) |

**LLM Prompt Design**:
```
System: You are a board game rulebook analyzer. Extract key game-specific terms
and concepts. Return JSON array with exactly these fields per concept:
  - term: the game-specific term
  - definition: clear 1-2 sentence definition
  - category: one of [Mechanic, Component, Rule, Action, Condition]
  - pageReference: section name or "N/A"

User: Analyze this rulebook for game "{gameName}":
{rulebookContent}

Return 8-15 key concepts. Focus on terms a new player would need explained.
```

---

### Issue #5449 — GenerateQuestions

**Execution Order**: Parallel with #5448

**Files to Modify**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `RulebookAnalysis.cs` | Add `GeneratedQuestions: List<GeneratedQuestion>` (JSONB) |
| CREATE | `SharedGameCatalog/Domain/ValueObjects/GeneratedQuestion.cs` | Question, Answer, SourceSection, Confidence, Tags |
| MODIFY | `LlmRulebookAnalyzer.cs` | Replace stub `GenerateQuestionsAsync()`, remove Italian fallbacks |

**LLM Prompt Design**:
```
System: Generate FAQ questions that new players commonly ask about this game.
Each answer must be SYNTHESIZED from the rules (not copied verbatim).
Return JSON array:
  - question: clear question a player would ask
  - answer: synthesized 2-3 sentence answer
  - sourceSection: which rulebook section covers this
  - tags: array of topic tags (setup, scoring, combat, movement, resources, etc.)

User: Generate 6-10 FAQ for "{gameName}":
{rulebookContent}
```

---

### Issue #5450 — ExtractStateSchema

**Execution Order**: Parallel with #5448, #5449

**Files to Modify**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `RulebookAnalysis.cs` | Add `GameStateSchema: JsonDocument?` |
| MODIFY | `LlmRulebookAnalyzer.cs` | Replace stub `ExtractStateSchemaAsync()` |
| MODIFY | EF Config | Map JsonDocument column with `HasColumnType("jsonb")` |

---

### Issue #5451 — Content Complexity Routing

**Execution Order**: After #5448-#5450 (needs extraction metadata)

**Files to Create/Modify**:

| Action | File | Changes |
|--------|------|---------|
| CREATE | `DocumentProcessing/Domain/ValueObjects/ContentComplexityScore.cs` | PageCount, TableCount, ImageRatio, OcrRequired, EstimatedComplexity |
| MODIFY | `PdfDocument.cs` | Add `ContentComplexityScore: decimal?` |
| MODIFY | `PdfProcessingPipelineService.cs` | Compute complexity after extraction |
| MODIFY | `AnalyzeRulebookCommandHandler.cs` | Replace `LargeRulebookThreshold` check with complexity > 0.4 |
| MODIFY | `BackgroundAnalysisOptions.cs` | Add `ComplexityThreshold: decimal` (default 0.4) |

**Complexity Data Source**: Unstructured-service already returns metadata (page count, element types). Parse from extraction response.

---

### Issue #5452 — Critical Section Quality Gate

**Execution Order**: After #5451

**Files to Modify**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `BackgroundRulebookAnalysisOrchestrator.cs` | Phase 2: tag chunks as critical/non-critical. Phase 3: check critical coverage |
| MODIFY | `RulebookAnalysis.cs` | Add `CriticalSectionsCoverage: decimal?`, `MissingSections: List<string>?` |
| CREATE | `SharedGameCatalog/Domain/Enums/AnalysisStatus.cs` | Complete, PartiallyComplete, Failed |
| MODIFY | LlmRulebookChunkAnalyzer | Classify section: victory_conditions, setup, turn_structure → critical |

**Section Classification Heuristic**:
```
Critical keywords in chunk header/content:
  - "victory", "win", "game end", "scoring" → VictoryConditions
  - "setup", "preparation", "starting" → Setup
  - "turn", "round", "phase", "sequence" → TurnStructure
```

---

### Issue #5453 — Structured RAG Fusion

**Execution Order**: After #5448, #5449 (needs concepts + FAQ data)

**Files to Create/Modify**:

| Action | File | Changes |
|--------|------|---------|
| CREATE | `KnowledgeBase/Application/Services/StructuredRetrievalService.cs` | Query RulebookAnalysis fields by intent |
| MODIFY | `RoutingLlmPlugin.cs` | Add intents: `glossary`, `faq`, `victory_conditions` |
| MODIFY | `HybridSearchEngine.cs` | Add 3rd source: structured results with 1.5x weight boost |
| MODIFY | `ResilientRetrievalService.cs` | Orchestrate structured + vector + keyword fusion |
| MODIFY | `AskQuestionQueryHandler.cs` | Pass structured results to LLM context |

**Integration Architecture**:
```
Current:  Query → Intent → HybridSearch (Vector+BM25+RRF) → Rerank → LLM
Enhanced: Query → Intent → Parallel {
            HybridSearch (Vector+BM25+RRF),
            StructuredRetrieval (RulebookAnalysis fields)
          } → 3-way fusion (structured 1.5x weight) → Rerank → LLM
```

**Confidence-gated logic**:
```csharp
if (structuredResult.Confidence > 0.85)
    return structuredResult; // Skip vector search for high-confidence structured matches
if (structuredResult.Confidence < 0.60)
    return vectorResults;   // Fallback to vector-only
// Otherwise: fuse both
```

---

### Issue #5454 — Expansion Context + Analysis Results UI

**Execution Order**: Last in Phase 2 (needs all analysis features)

**Backend**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `BackgroundRulebookAnalysisOrchestrator.cs` Phase 1 | If expansion, load base RulebookAnalysis → inject into overview prompt |
| CREATE | `GET /games/{gameId}/analysis` endpoint | Return full AnalysisDto |
| CREATE | `GET /games/{gameId}/analysis/faq` endpoint | Filtered FAQ list |
| CREATE | `GET /games/{gameId}/analysis/glossary` endpoint | Filtered glossary |

**Frontend**:

| Action | File | Changes |
|--------|------|---------|
| CREATE | `components/games/AnalysisResultsPanel.tsx` | Tabbed view: Mechanics, Phases, FAQ, Glossary, State |
| CREATE | `components/games/AnalysisFaqTab.tsx` | FAQ list with confidence badges |
| CREATE | `components/games/AnalysisGlossaryTab.tsx` | Glossary with category filters |
| MODIFY | Game detail page | Add AnalysisResultsPanel section |

---

## Phase 3: Admin Queue Management

### Issue #5455 — Processing Priority System

**Files to Create/Modify**:

| Action | File | Changes |
|--------|------|---------|
| CREATE | `DocumentProcessing/Domain/Enums/ProcessingPriority.cs` | Low=0, Normal=1, High=2, Urgent=3 |
| MODIFY | `PdfDocument.cs` | Add ProcessingPriority, QueuedAt, ProcessingStartedAt, ProcessingCompletedAt |
| CREATE | `DocumentProcessing/Domain/Entities/ProcessingQueueConfig.cs` | Singleton: IsPaused, MaxConcurrentWorkers, etc. |
| CREATE | `DocumentProcessing/Application/Services/ProcessingQueueService.cs` | Dequeue by priority DESC, QueuedAt ASC |
| MODIFY | `PdfProcessingPipelineService.cs` | Check queue before processing, respect priority order |
| MODIFY | Upload handlers | Set priority: Admin→High, User→Normal |
| CREATE | `PATCH /admin/documents/{id}/priority` endpoint | Bump priority |
| CREATE | `PATCH /admin/processing-queue/config` endpoint | Worker count, etc. |

**Queue Worker Pattern**:
```csharp
// Background service polls every 5 seconds
var nextDoc = await _repo.GetNextPendingAsync(); // ORDER BY Priority DESC, QueuedAt ASC LIMIT 1
if (nextDoc == null || _config.IsPaused) return;
if (currentWorkers >= _config.MaxConcurrentWorkers) return;
await ProcessDocumentAsync(nextDoc);
```

---

### Issue #5456 — Pause/Resume + Cancel + Bulk Reindex

**Files to Create/Modify**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `PdfProcessingState.cs` | Add Cancelled=7 |
| CREATE | `POST /admin/processing-queue/pause` endpoint | Set IsPaused=true |
| CREATE | `POST /admin/processing-queue/resume` endpoint | Set IsPaused=false |
| CREATE | `POST /admin/documents/{id}/cancel` endpoint | Propagate CancellationToken |
| CREATE | `POST /admin/processing-queue/reindex-failed` endpoint | Re-queue all Failed as Low |
| CREATE | `GET /admin/documents/{id}/extracted-text` endpoint | Raw text preview |

---

### Issue #5457 — Rate Limiting + Backpressure + Notifications

**Files to Create/Modify**:

| Action | File | Changes |
|--------|------|---------|
| CREATE | `DocumentProcessing/Application/Services/UploadRateLimiterService.cs` | Redis-based: `pdf_upload:{userId}` sliding window |
| MODIFY | `UploadPdfCommandHandler.cs` | Check rate limit before processing |
| MODIFY | Upload response | Add `estimatedWaitMinutes` when backpressure active |
| CREATE | `DocumentProcessing/Application/Events/PdfProcessingCompletedEvent.cs` | Triggers notification |
| MODIFY | UserNotifications BC | Handle completion event → send notification |

---

### Issue #5458 — Admin Queue Management UI

**Frontend only** — all API endpoints from #5455-#5457 must exist.

| Action | File |
|--------|------|
| CREATE | `components/admin/knowledge-base/QueueControlBar.tsx` |
| CREATE | `components/admin/knowledge-base/QueueStatsCards.tsx` |
| CREATE | `components/admin/knowledge-base/ProcessingQueueTable.tsx` |
| CREATE | `components/admin/knowledge-base/QueueItemActions.tsx` |
| CREATE | `components/admin/knowledge-base/BulkActionsBar.tsx` |
| CREATE | `components/admin/knowledge-base/ExtractedTextPreviewModal.tsx` |
| CREATE | `components/admin/knowledge-base/QueueCapacityIndicator.tsx` |
| MODIFY | `app/admin/(dashboard)/knowledge-base/processing-queue/page.tsx` | Integrate new components |
| MODIFY | `lib/api/admin-client.ts` | Add queue management API methods |

---

## Phase 4: Observability & Quality Monitoring

### Issue #5459 — Per-Phase Timing + Metrics Dashboard

**Backend**:

| Action | File | Changes |
|--------|------|---------|
| MODIFY | `PdfDocument.cs` | Add `ExtractionDurationMs`, `ChunkingDurationMs`, `AnalysisDurationMs`, `EmbeddingDurationMs`, `IndexingDurationMs` |
| MODIFY | `PdfProcessingPipelineService.cs` | Record Stopwatch per phase |
| MODIFY | `RulebookAnalysis.cs` | Add `AnalysisDurationMs: long?` |
| CREATE | `GET /admin/processing-queue/metrics` endpoint | Aggregate metrics query |

**Frontend**:

| Action | File |
|--------|------|
| CREATE | `components/admin/knowledge-base/ProcessingTimeChart.tsx` |
| CREATE | `components/admin/knowledge-base/QualityDistributionChart.tsx` |
| CREATE | `components/admin/knowledge-base/FailureRateTrendChart.tsx` |
| CREATE | `app/admin/(dashboard)/knowledge-base/metrics/page.tsx` |

---

### Issue #5460 — Proactive Alerts

| Action | File | Changes |
|--------|------|---------|
| CREATE | `DocumentProcessing/Application/BackgroundServices/ProcessingQueueMonitorService.cs` | Runs every 2 min |
| CREATE | `DocumentProcessing/Application/Events/DocumentStuckEvent.cs` | > 10 min in processing |
| CREATE | `DocumentProcessing/Application/Events/QueueDepthAlertEvent.cs` | > 20 pending |
| CREATE | `DocumentProcessing/Application/Events/HighFailureRateAlertEvent.cs` | > 15% in 1 hour |
| CREATE | `GET /admin/processing-queue/alerts` endpoint | Active alerts list |
| MODIFY | Admin dashboard | Alert banner component |

---

### Issue #5461 — Analysis Comparison Tool

| Action | File | Changes |
|--------|------|---------|
| CREATE | `SharedGameCatalog/Application/Services/AnalysisComparisonService.cs` | Diff two analyses |
| CREATE | `GET /admin/analysis/{id}/compare/{otherId}` endpoint | Returns diff DTO |
| CREATE | `components/admin/knowledge-base/AnalysisComparisonView.tsx` | Two-column diff |

---

## Execution Per `/implementa`

Each issue follows the `/implementa` workflow:

```
Per ogni issue:
  1. git checkout feature/epic-5442-pdf-rulebook-phase{N}
  2. git checkout -b feature/issue-{ID}-{desc}
  3. Implement (CQRS: Domain → Command+Validator+Handler → Endpoint → Tests)
  4. dotnet test + pnpm test (if frontend changes)
  5. git commit + push
  6. gh pr create --base feature/epic-5442-pdf-rulebook-phase{N}
  7. Code review + fix
  8. gh pr merge --squash --delete-branch
  9. Update issue checkboxes on GitHub
  10. Close issue
```

After all phase issues merged:
```
  gh pr create --base main-dev --head feature/epic-5442-pdf-rulebook-phase{N}
  # Phase-level PR for final review before merging to main-dev
```

---

## Migration Strategy

**Single combined migration per phase** (recommended):

```
Phase 1 Migration: AddDocumentClassificationFields
  - DocumentCategory (int, default 0)
  - BaseDocumentId (Guid?, FK self-referential)
  - DetectedLanguage (string?, max 10)
  - IsActiveForRag (bool, default true)
  - CopyrightDisclaimerAcceptedAt (DateTime?)
  - CopyrightDisclaimerAcceptedBy (Guid?)
  - VersionLabel (string?, max 100)
  + Indexes: DocumentCategory, BaseDocumentId, IsActiveForRag

Phase 2 Migration: AddRulebookAnalysisEnhancements
  - KeyConcepts (jsonb)
  - GeneratedQuestions (jsonb)
  - GameStateSchema (jsonb)
  - ContentComplexityScore (decimal?)
  - CriticalSectionsCoverage (decimal?)
  - MissingSections (jsonb?)
  - AnalysisDurationMs (bigint?)

Phase 3 Migration: AddProcessingQueueManagement
  - ProcessingPriority (int, default 1)
  - QueuedAt (timestamp?)
  - ProcessingStartedAt (timestamp?)
  - ProcessingCompletedAt (timestamp?)
  - CancelledAt (timestamp?)
  - CancelledBy (Guid?)
  + ProcessingQueueConfig table
  + Index: (ProcessingPriority DESC, QueuedAt ASC)

Phase 4 Migration: AddProcessingObservability
  - ExtractionDurationMs (bigint?)
  - ChunkingDurationMs (bigint?)
  - AnalysisDurationMs (bigint?)
  - EmbeddingDurationMs (bigint?)
  - IndexingDurationMs (bigint?)
```

---

## Risk Mitigations

| Risk | Mitigation |
|------|------------|
| Migration conflicts between parallel phases | Combine into single migration per phase; merge Phase 1 first |
| `DocumentType` collision | Keep existing string field, add new `DocumentCategory` enum alongside |
| LLM prompt quality for stubs | Test with 3 real rulebooks (Catan, Carcassonne, Ticket to Ride) before merging |
| Structured RAG fusion degrades quality | Feature flag `EnableStructuredRagFusion` (default false) → enable after A/B validation |
| Queue worker race conditions | Distributed lock per document + optimistic concurrency on dequeue |

---

## Test Strategy

| Phase | Backend Tests | Frontend Tests |
|-------|--------------|----------------|
| Phase 1 | Unit: enum values, pipeline gate, FK validation. Integration: migration, queries | Unit: category selector, disclaimer checkbox. E2E: upload flow with category |
| Phase 2 | Unit: LLM prompt parsing, complexity score, quality gate. Integration: analysis storage, retrieval fusion | Unit: AnalysisResultsPanel tabs. E2E: view analysis on game page |
| Phase 3 | Unit: priority ordering, rate limiter, cancel propagation. Integration: queue dequeue order, pause/resume | Unit: QueueControlBar, QueueTable. E2E: admin queue management |
| Phase 4 | Unit: metric aggregation, alert thresholds. Integration: timing recording, alert emission | Unit: chart components. E2E: metrics dashboard load |

---

**Ready to execute**: Start with `/implementa 5443 --base-branch main-dev` when approved.
