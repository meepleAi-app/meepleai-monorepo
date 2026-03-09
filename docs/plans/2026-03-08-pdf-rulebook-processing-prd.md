# PRD: PDF Rulebook Processing Enhancement

**Date**: 2026-03-08
**Status**: Draft
**Author**: Spec Panel (Wiegers, Adzic, Fowler, Nygard, Crispin)
**Stakeholders**: Product, Engineering, AI Team

---

## Executive Summary

This PRD defines enhancements to MeepleAI's PDF rulebook processing pipeline — from upload through structured analysis to RAG-powered Q&A. The goal is to transform raw rulebook PDFs into **rich, structured game knowledge** (mechanics, rules, FAQs, glossary, game state schema) that powers the AI assistant.

Two upload flows exist: **Admin** (SharedGame catalog, public) and **User** (private, owned games only). Both feed into the same processing pipeline but with different visibility, priority, and approval rules.

### Strategic Decisions (Locked)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Copyright policy | User uploads private-only, disclaimer at upload | Legal safety — user asserts ownership |
| PDF content rework | Extracted knowledge is reworked/synthesized, not verbatim copy | Fair use — transformative output |
| Rulebook versioning | User selects active version + toggles expansions/errata | Games evolve; players need control |
| Non-rulebook PDFs | Consultation-only, no RulebookAnalysis pipeline | Reference cards and player aids have different structure |
| Supported languages | Western only (EN, IT, DE, ES, FR, PT, NL, PL, SV, ...) | Tesseract OCR coverage + LLM quality |
| Processing queue | Admin-managed, priority-based, persistent (PostgreSQL) | Visibility and control over compute-intensive pipeline |
| Fine-tuned model | Phase 3 — after generic LLM implementation proves value | Build dataset from Phase 1-2 outputs; avoid premature investment |

---

## Phase 1: Document Classification & Metadata

**Epic**: PDF Document Classification System
**Priority**: P0 (highest — prerequisite for all other phases)
**Estimated Issues**: ~12
**Dependencies**: None (extends existing PdfDocument entity)

### Problem Statement

The current pipeline treats all uploaded PDFs identically. A full rulebook, a 2-page quick-start guide, a reference card, and an expansion rulebook all enter the same 4-phase analysis pipeline. This wastes compute on non-rulebooks and produces incorrect analysis for expansion content that lacks base game context.

### Requirements

#### Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| DC-001 | PDFs are classified by category at upload time | Must | Categories: `Rulebook`, `Expansion`, `Errata`, `QuickStart`, `Reference`, `PlayerAid`, `Other` |
| DC-002 | Only `Rulebook`, `Expansion`, `Errata` enter the RulebookAnalysis pipeline | Must | `QuickStart`, `Reference`, `PlayerAid`, `Other` are indexed for vector search only (no structured analysis) |
| DC-003 | `Expansion` PDFs are linked to a base `Rulebook` PDF | Should | FK `BaseDocumentId` on PdfDocument; expansion analysis includes base game context |
| DC-004 | `Errata` PDFs are linked to the rulebook they correct | Must | FK `BaseDocumentId`; errata content merged into base analysis on reindex |
| DC-005 | User selects document category during upload | Must | Dropdown selector in upload UI with clear descriptions |
| DC-006 | User can toggle which documents are active for RAG | Must | Checkbox per document in "My Game PDFs" view; only active docs searched |
| DC-007 | Admin can reclassify documents post-upload | Should | Admin KB page: bulk reclassify action |
| DC-008 | Copyright disclaimer shown before user upload | Must | Checkbox: "I confirm I own this game and this PDF is for personal use only" |
| DC-009 | Language validation at upload | Must | Detect language from first 2 pages; reject if CJK/Arabic/Hebrew with clear message |
| DC-010 | Language auto-detection stored on PdfDocument | Should | `DetectedLanguage` field (ISO 639-1); used for OCR config and prompt language |
| DC-011 | User can upload multiple PDFs per game (base + expansions + errata) | Must | List view showing all PDFs for a game with category, status, active toggle |
| DC-012 | Version label for each document | Should | User-editable string (e.g., "2nd Edition", "v1.3 Errata", "Seafarers Expansion") |

#### Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| DC-NFR-001 | Language detection latency | < 500ms (first 2 pages only) |
| DC-NFR-002 | Category selection UX | < 3 clicks from upload start to submission |
| DC-NFR-003 | Active toggle response time | < 200ms (optimistic UI + async reindex) |

### Data Model Changes

```
PdfDocument (ALTER existing entity)
├── + DocumentCategory: enum (Rulebook=0, Expansion=1, Errata=2, QuickStart=3, Reference=4, PlayerAid=5, Other=6)
├── + BaseDocumentId: Guid? (FK → PdfDocument, self-referential, for Expansion/Errata linkage)
├── + DetectedLanguage: string? (ISO 639-1, e.g., "en", "it", "de")
├── + VersionLabel: string? (max 100, user-editable)
├── + IsActiveForRag: bool (default true, user-toggleable)
├── + CopyrightDisclaimerAcceptedAt: DateTime? (timestamp of user acceptance)
└── + CopyrightDisclaimerAcceptedBy: Guid? (FK → User)

Indexes:
├── IX_PdfDocument_BaseDocumentId (for expansion/errata lookups)
├── IX_PdfDocument_DocumentCategory (for filtered queries)
└── IX_PdfDocument_IsActiveForRag (for RAG retrieval filtering)
```

### API Endpoints

```
PATCH  /api/v1/documents/{documentId}/category
  - Body: { category: "Expansion", baseDocumentId?: "guid", versionLabel?: "string" }
  - Auth: Document owner or Admin
  - Validation: baseDocumentId must belong to same game, category must be valid enum
  - Returns: 200 OK with updated DocumentDto

PATCH  /api/v1/documents/{documentId}/active
  - Body: { isActive: true/false }
  - Auth: Document owner
  - Side effect: If deactivated, vectors remain but excluded from search filter
  - Returns: 200 OK

GET    /api/v1/games/{gameId}/documents
  - Returns: List<DocumentDto> with category, status, isActive, versionLabel, detectedLanguage
  - Auth: Game owner (private) or Admin (shared)
  - Includes: baseDocument reference for expansions/errata

POST   /api/v1/documents/{documentId}/accept-disclaimer
  - Body: { accepted: true }
  - Auth: Uploading user
  - Validation: Must be accepted before processing starts
  - Returns: 200 OK
```

### Frontend Integration

```
Modified Components:
├── PdfUploadSection.tsx: Add category dropdown + version label input + disclaimer checkbox
├── PdfUploadModal.tsx: Add base document selector (visible when category = Expansion|Errata)
└── NEW: GameDocumentList.tsx: List all PDFs for a game with active toggles and category badges

New Components:
├── DocumentCategorySelect.tsx (dropdown with icons and descriptions per category)
├── CopyrightDisclaimerCheckbox.tsx (with legal text, required before upload starts)
├── DocumentVersionLabel.tsx (inline-editable text field)
├── ActiveForRagToggle.tsx (switch with optimistic UI + toast on error)
└── BaseDocumentSelector.tsx (dropdown of existing Rulebook PDFs for the same game)
```

### Scenarios (Gherkin)

```gherkin
Scenario: User uploads a base rulebook
  Given I own "Catan" in my library
  When I click "Upload PDF" on the Catan game page
  Then I see a copyright disclaimer checkbox
  And I must check it before the upload button is enabled
  When I select category "Rulebook" and upload "catan-rules-v2.pdf"
  Then the document enters the processing queue with DocumentCategory=Rulebook
  And DetectedLanguage is auto-detected as "en"
  And IsActiveForRag defaults to true

Scenario: User uploads an expansion rulebook linked to base
  Given I have "catan-rules-v2.pdf" (Rulebook, Ready) for Catan
  When I upload "catan-seafarers.pdf" with category "Expansion"
  Then I see a "Base Rulebook" selector showing "catan-rules-v2.pdf"
  When I select it and confirm
  Then the expansion is linked via BaseDocumentId
  And the RulebookAnalysis for the expansion includes base game context

Scenario: User toggles a document inactive for RAG
  Given I have 3 PDFs for Catan: base (active), seafarers (active), errata-v1 (active)
  When I toggle "seafarers" to inactive
  Then the AI assistant no longer searches seafarers content for Catan queries
  But the PDF and its analysis remain stored (not deleted)
  And I can reactivate it at any time

Scenario: Upload rejected for unsupported language
  Given I try to upload "catan-rules-jp.pdf" (Japanese rulebook)
  When language detection identifies Japanese
  Then the upload is rejected with message:
    "Currently only Western-alphabet languages are supported (English, Italian, German, Spanish, French, etc.)"
  And no PdfDocument entity is created

Scenario: Non-rulebook PDF skips analysis
  Given I upload "catan-reference-card.pdf" with category "Reference"
  Then the document goes through Extraction → Chunking → Embedding → Indexing
  But RulebookAnalysis 4-phase pipeline is NOT triggered
  And the document is available for RAG vector search (consultation only)
  And no structured mechanics/FAQ/glossary is extracted
```

---

## Phase 2: Complete RulebookAnalysis Implementation

**Epic**: Structured Rulebook Knowledge Extraction
**Priority**: P0 (core value proposition)
**Estimated Issues**: ~15
**Dependencies**: Phase 1 (DocumentCategory determines pipeline routing)

### Problem Statement

The RulebookAnalysis pipeline has 3 unimplemented methods (`ExtractStateSchema`, `GenerateQuestions`, `ExtractKeyConcepts`) that are currently stubs returning fallback data. These represent the **highest-value features** for the AI assistant:
- Without `GenerateQuestions`, the RAG system is purely reactive — users must know what to ask
- Without `ExtractKeyConcepts`, term disambiguation fails for complex games
- Without `ExtractStateSchema`, the system cannot help track game state during play

Additionally, the structured `RulebookAnalysis` data is not integrated into RAG retrieval — the system uses only vector chunks, ignoring the rich structured knowledge.

### Requirements

#### Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| RA-001 | `ExtractKeyConcepts` extracts glossary from rulebook | Must | Min 5 concepts per rulebook; each has: term, definition, category (Mechanic/Component/Rule/Action/Condition), page reference if available |
| RA-002 | `GenerateQuestions` produces FAQ from rulebook | Must | 5-10 questions per rulebook; each has: question, answer (synthesized, not verbatim), confidence score, source section |
| RA-003 | `ExtractStateSchema` produces game state tracking structure | Should | JSON schema defining: tracked resources per player, shared state, victory progress, turn/round tracking |
| RA-004 | RulebookAnalysis structured data is used in RAG retrieval | Must | Query "How do you win Catan?" hits both vector chunks AND RulebookAnalysis.VictoryConditions; fusion ranking |
| RA-005 | Quality gate: critical sections require 100% analysis success | Must | Victory conditions, setup, turn structure chunks must succeed; 75% threshold for non-critical |
| RA-006 | Expansion analysis includes base game context | Must | When analyzing an Expansion PDF, Phase 1 (Overview) receives base game RulebookAnalysis as context |
| RA-007 | Errata analysis produces diff against base | Should | Output: list of changed rules with before/after, affected mechanics, affected FAQ answers |
| RA-008 | Analysis results viewable by document owner | Must | "Analysis Results" panel on game page showing: mechanics, phases, victory conditions, FAQ, glossary |
| RA-009 | Admin can trigger re-analysis with different parameters | Should | Admin action: re-analyze with custom chunk size, different LLM model, or expanded prompt |
| RA-010 | Confidence score per extracted element | Must | Each mechanic, FAQ, concept has individual confidence 0-1; low-confidence items flagged in UI |
| RA-011 | Routing by content complexity, not file size | Must | Replace 50KB threshold with complexity score: page count, table count, image ratio, OCR requirement |

#### Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| RA-NFR-001 | Analysis time: simple rulebook (< 15 pages, text-only) | < 60 seconds |
| RA-NFR-002 | Analysis time: complex rulebook (15-40 pages, tables+images) | < 5 minutes |
| RA-NFR-003 | FAQ quality: human-rated relevance | > 80% of questions rated "useful" by 3 reviewers |
| RA-NFR-004 | Glossary completeness | > 90% of unique game terms captured |
| RA-NFR-005 | RAG answer quality improvement | > 15% improvement in answer relevance with structured+vector vs vector-only |

### Data Model Changes

```
RulebookAnalysis (ALTER existing entity)
├── + KeyConcepts: List<KeyConcept> (JSONB)
│     ├── Term: string
│     ├── Definition: string
│     ├── Category: enum (Mechanic, Component, Rule, Action, Condition)
│     ├── PageReference: string?
│     └── Confidence: decimal (0-1)
├── + GeneratedQuestions: List<GeneratedQuestion> (JSONB)
│     ├── Question: string
│     ├── Answer: string (synthesized, not verbatim)
│     ├── SourceSection: string
│     ├── Confidence: decimal (0-1)
│     └── Tags: List<string> (e.g., "setup", "scoring", "combat")
├── + GameStateSchema: JsonDocument? (JSON schema for state tracking)
├── + ContentComplexityScore: decimal? (0-1, computed at extraction)
├── + CriticalSectionsCoverage: decimal? (0-1, % of critical sections analyzed)
└── + AnalysisDurationMs: long? (total pipeline duration for performance tracking)

ContentComplexityScore (new value object, computed during extraction)
├── PageCount: int
├── TableCount: int
├── ImageRatio: decimal (images area / total area)
├── OcrRequired: bool
├── EstimatedComplexity: decimal (0-1, weighted composite)
└── RoutingDecision: enum (Synchronous, Background)

Routing formula:
  complexity = (pageCount/40)*0.3 + (tableCount/20)*0.25 + imageRatio*0.25 + (ocrRequired ? 0.2 : 0)
  if complexity > 0.4 → Background
  else → Synchronous
```

### Structured RAG Integration

```
Current RAG flow (vector-only):
  Query → Embed → Qdrant similarity search → Top-K chunks → LLM generation

Enhanced RAG flow (structured + vector fusion):
  Query → Classify intent
    ├── "mechanics" → RulebookAnalysis.KeyMechanics (structured) + vector chunks
    ├── "how to win" → RulebookAnalysis.VictoryConditions (structured) + vector chunks
    ├── "what is X" → RulebookAnalysis.KeyConcepts (structured, exact match) + vector chunks
    ├── "FAQ" → RulebookAnalysis.GeneratedQuestions (pre-computed) + vector chunks
    └── "general" → vector chunks only (default)

Fusion strategy:
  1. Structured results get boost weight (1.5x) in RRF ranking
  2. If structured result confidence > 0.85, present directly without vector fallback
  3. If structured result confidence < 0.6, fallback to vector-only
  4. Log structured vs vector contribution for quality monitoring
```

### API Endpoints

```
GET    /api/v1/games/{gameId}/analysis
  - Returns: RulebookAnalysisDto with all structured data
  - Auth: Game owner (private) or public (shared games)
  - Includes: mechanics, phases, victoryConditions, keyConcepts, generatedQuestions, gameStateSchema

GET    /api/v1/games/{gameId}/analysis/faq
  - Returns: List<GeneratedQuestionDto> sorted by confidence
  - Auth: Game owner or public
  - Query params: tag?, minConfidence?

GET    /api/v1/games/{gameId}/analysis/glossary
  - Returns: List<KeyConceptDto> sorted by term
  - Auth: Game owner or public
  - Query params: category?, search?

POST   /api/v1/admin/analysis/{analysisId}/reanalyze
  - Body: { chunkSize?: int, model?: string, includeExpansions?: bool }
  - Auth: Admin only
  - Side effect: Creates new RulebookAnalysis version, deactivates previous
  - Returns: 202 Accepted with job tracking URL
```

### Scenarios (Gherkin)

```gherkin
Scenario: Complete analysis of a simple rulebook
  Given "Catan" has a Rulebook PDF (12 pages, text-only, English)
  When the RulebookAnalysis pipeline completes
  Then the analysis contains:
    | Field              | Expected                                    |
    | KeyMechanics       | >= 5 mechanics including "Dice Rolling"     |
    | VictoryConditions  | "10 victory points" as primary              |
    | GamePhases         | >= 3 phases including "Resource Production" |
    | KeyConcepts        | >= 8 terms including "Settlement", "Road"   |
    | GeneratedQuestions  | >= 5 FAQ including setup and scoring        |
    | ConfidenceScore    | >= 0.75                                     |
  And each KeyConcept has a non-empty Definition
  And each GeneratedQuestion has a synthesized Answer (not verbatim from PDF)

Scenario: Expansion analyzed with base game context
  Given "Catan" base rulebook analysis exists (confidence 0.85)
  And "Catan: Seafarers" expansion PDF is uploaded with BaseDocumentId linking to base
  When the expansion RulebookAnalysis pipeline runs
  Then Phase 1 (Overview) receives base game mechanics and phases as context
  And the expansion analysis adds new mechanics: "Ship Building", "Island Discovery"
  And the expansion analysis references base mechanics it modifies
  And GeneratedQuestions include "How do ships interact with roads from the base game?"

Scenario: RAG query uses structured + vector fusion
  Given "Catan" has a complete RulebookAnalysis with VictoryConditions.Primary = "10 victory points"
  When a user asks "How do you win at Catan?"
  Then the RAG system:
    1. Classifies intent as "victory_conditions"
    2. Retrieves RulebookAnalysis.VictoryConditions (structured, confidence 0.90)
    3. Retrieves top-3 vector chunks matching "win" / "victory" (similarity > 0.7)
    4. Fuses results: structured result gets 1.5x weight
    5. Generates answer incorporating both structured conditions and contextual chunks
  And the answer includes: "10 VP", "settlements (1 VP each)", "cities (2 VP each)", "longest road", "largest army"
  And the answer does NOT hallucinate mechanics not in the rulebook

Scenario: Content complexity routing replaces file size routing
  Given a 30KB PDF with 5 complex tables and diagrams (complexity score 0.65)
  And a 120KB PDF that is pure text (complexity score 0.25)
  When both enter the pipeline
  Then the 30KB PDF is routed to Background (complexity > 0.4)
  And the 120KB PDF is routed to Synchronous (complexity < 0.4)

Scenario: Critical section failure blocks merge
  Given a rulebook analysis where Phase 3 processed 20 chunks
  And 18/20 succeeded (90% overall)
  But the "Victory Conditions" chunk failed analysis
  Then Phase 4 (Merge) is blocked
  And the analysis is marked as PartiallyComplete
  And the admin sees: "Critical section missing: Victory Conditions"
  And the analysis is still usable but flagged with missingSections: ["VictoryConditions"]
```

---

## Phase 3: Admin Queue Management

**Epic**: PDF Processing Queue Administration
**Priority**: P1
**Estimated Issues**: ~10
**Dependencies**: Phase 1 (DocumentCategory for priority routing)

### Problem Statement

The current processing queue is implicit — PDF state machine in PostgreSQL with a background worker. Admins can view status but cannot manage the queue: no priority control, no pause/resume, no capacity limits. As user uploads scale, the LLM analysis worker becomes a bottleneck with no visibility or control.

### Requirements

#### Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| QM-001 | Processing queue with priority levels | Must | Admin (SharedGame) = HIGH, User (private) = NORMAL, Reindex = LOW; FIFO within same priority |
| QM-002 | Admin can bump priority of any document | Must | Admin action: promote to HIGH priority; document moves to front of its priority lane |
| QM-003 | Admin can pause/resume global queue | Must | Pause: no new documents start processing; in-progress documents complete. Resume: queue restarts |
| QM-004 | Admin can cancel processing of a single document | Must | Cancel: abort current phase, mark as Cancelled state, release resources |
| QM-005 | Admin can bulk reindex all failed documents | Should | "Reindex All Failed" button; creates LOW priority jobs for each |
| QM-006 | Admin can preview extracted text before embedding | Should | After Extraction phase: "Preview Text" button showing raw extracted content |
| QM-007 | Rate limit per user | Must | Max 5 PDF uploads per hour per user; admin exempt |
| QM-008 | Configurable max concurrent processing | Must | Admin setting: 1-10 concurrent workers; default 3 |
| QM-009 | Backpressure when queue exceeds threshold | Should | Queue > 50 pending: new uploads return 429 with estimated wait time |
| QM-010 | User notification on processing completion | Must | Toast + notification: "Your Catan rulebook is ready" or "Processing failed for Catan" |

#### Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| QM-NFR-001 | Queue query latency | < 100ms for admin dashboard |
| QM-NFR-002 | Priority change propagation | Immediate (next worker pick-up) |
| QM-NFR-003 | Queue persistence | Survives Redis restart (PostgreSQL-backed) |
| QM-NFR-004 | Cancel propagation | < 5 seconds to abort in-progress phase |

### Data Model Changes

```
PdfDocument (ALTER existing entity)
├── + ProcessingPriority: enum (Low=0, Normal=1, High=2, Urgent=3)
├── + QueuedAt: DateTime? (when entered processing queue)
├── + ProcessingStartedAt: DateTime? (when worker picked it up)
├── + ProcessingCompletedAt: DateTime? (when Ready or Failed)
├── + CancelledAt: DateTime? (when admin cancelled)
├── + CancelledBy: Guid? (admin who cancelled)
└── ProcessingState: enum (+ Cancelled=7, + Paused=8)

ProcessingQueueConfig (new entity, singleton)
├── Id: Guid (PK)
├── IsPaused: bool (default false)
├── MaxConcurrentWorkers: int (default 3, range 1-10)
├── MaxQueueSize: int (default 100)
├── UserRateLimitPerHour: int (default 5)
└── UpdatedAt: DateTime

Indexes:
├── IX_PdfDocument_ProcessingPriority_QueuedAt (for priority-ordered dequeue)
└── IX_PdfDocument_ProcessingState_Partial (WHERE ProcessingState IN (0,1,2,3,4,5))
```

### API Endpoints

```
GET    /api/v1/admin/processing-queue
  - Returns: Paginated list of queued/in-progress documents with priority, state, ETA
  - Query params: state?, priority?, search?, page, pageSize
  - Auth: Admin only

POST   /api/v1/admin/processing-queue/pause
  - Auth: Admin only
  - Side effect: Sets IsPaused=true; in-progress docs finish, no new ones start
  - Returns: 200 OK with { queueSize, inProgressCount }

POST   /api/v1/admin/processing-queue/resume
  - Auth: Admin only
  - Returns: 200 OK

PATCH  /api/v1/admin/documents/{documentId}/priority
  - Body: { priority: "High" }
  - Auth: Admin only
  - Returns: 200 OK

POST   /api/v1/admin/documents/{documentId}/cancel
  - Auth: Admin only
  - Side effect: CancellationToken triggered, state → Cancelled
  - Returns: 200 OK

POST   /api/v1/admin/processing-queue/reindex-failed
  - Auth: Admin only
  - Side effect: All Failed documents re-queued as Low priority
  - Returns: 200 OK with { count }

GET    /api/v1/admin/documents/{documentId}/extracted-text
  - Auth: Admin only
  - Returns: Raw extracted text (if past Extraction phase)
  - Validation: 404 if document hasn't completed extraction

PATCH  /api/v1/admin/processing-queue/config
  - Body: { maxConcurrentWorkers?: int, maxQueueSize?: int, userRateLimitPerHour?: int }
  - Auth: Admin only
  - Returns: 200 OK with updated config
```

### Frontend Integration

```
Enhanced Admin Page: /admin/knowledge-base/processing-queue

Components:
├── QueueControlBar.tsx (Pause/Resume toggle, worker count slider, queue stats)
├── QueueStatsCards.tsx (KPIs: pending count, in-progress, avg time, failure rate)
├── ProcessingQueueTable.tsx (sortable table: document, game, category, priority, state, duration, actions)
├── QueueItemActions.tsx (dropdown: Bump Priority, Cancel, Preview Text, Reindex)
├── BulkActionsBar.tsx (Reindex All Failed, Pause Queue)
├── ExtractedTextPreviewModal.tsx (read-only view of extracted text)
├── QueueCapacityIndicator.tsx (visual bar: X/100 queue capacity, color-coded)
└── ProcessingTimelineChart.tsx (avg processing time per phase, last 24h/7d/30d)
```

### Scenarios (Gherkin)

```gherkin
Scenario: Admin pauses and resumes the queue
  Given 3 documents are in-progress and 12 are pending
  When admin clicks "Pause Queue"
  Then the 3 in-progress documents continue to completion
  But no pending documents start processing
  And the queue status shows "PAUSED" badge
  When admin clicks "Resume Queue"
  Then the next highest-priority pending document starts processing

Scenario: Admin bumps priority
  Given document "twilight-imperium.pdf" is position 15 in the Normal priority queue
  When admin sets its priority to High
  Then it moves ahead of all Normal and Low priority documents
  And the next available worker picks it up

Scenario: User rate-limited on upload
  Given user "alice" has uploaded 5 PDFs in the last hour
  When alice tries to upload a 6th PDF
  Then she receives 429 Too Many Requests
  With message: "Upload limit reached (5/hour). Next upload available in 37 minutes."
  And the upload button is disabled with a countdown timer

Scenario: Backpressure on full queue
  Given 55 documents are pending in the queue (threshold: 50)
  When a user uploads a new PDF
  Then upload succeeds but returns: 202 Accepted with estimatedWaitMinutes: 45
  And the UI shows: "Your document is queued. Estimated processing time: ~45 minutes"

Scenario: Admin cancels a stuck document
  Given "complex-game.pdf" has been in "Extracting" state for 12 minutes
  When admin clicks "Cancel Processing"
  Then the CancellationToken is triggered
  And the document state changes to "Cancelled"
  And the admin sees option: "Reindex" to retry
```

---

## Phase 4: Observability & Quality Monitoring

**Epic**: Processing Pipeline Observability
**Priority**: P2
**Estimated Issues**: ~8
**Dependencies**: Phase 3 (queue management provides data points)

### Problem Statement

The admin has basic visibility into individual document status but lacks aggregate metrics, trend analysis, and proactive alerting. Without observability, quality degradation (model changes, extraction failures) goes undetected until users complain.

### Requirements

#### Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| OB-001 | Dashboard: average processing time per phase | Must | Line chart, last 24h/7d/30d, per phase (extraction, chunking, analysis, embedding, indexing) |
| OB-002 | Dashboard: quality score distribution | Must | Histogram of RulebookAnalysis.ConfidenceScore across all documents |
| OB-003 | Dashboard: failure rate trend | Must | % of documents reaching Failed state, last 24h/7d/30d |
| OB-004 | Alert: document stuck > 10 minutes | Must | Background check every 2 minutes; emit event if any document exceeds threshold |
| OB-005 | Alert: queue depth > 20 pending | Should | Configurable threshold; emit event when crossed |
| OB-006 | Alert: failure rate > 15% in last hour | Must | Sliding window; admin notification |
| OB-007 | Per-phase timing stored on PdfDocument | Must | `ExtractionDurationMs`, `ChunkingDurationMs`, `AnalysisDurationMs`, `EmbeddingDurationMs`, `IndexingDurationMs` |
| OB-008 | Analysis comparison tool | Could | Side-by-side view of two analysis versions for same game (useful after re-analysis or model change) |

#### Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| OB-NFR-001 | Dashboard refresh rate | Real-time (SSE) or 10-second polling |
| OB-NFR-002 | Alert delivery latency | < 30 seconds from threshold breach |
| OB-NFR-003 | Historical data retention | 90 days of per-document metrics |

### API Endpoints

```
GET    /api/v1/admin/processing-queue/metrics
  - Returns: { avgTimeByPhase: {}, failureRate: {}, queueDepth, qualityDistribution: {} }
  - Query params: period (24h|7d|30d)
  - Auth: Admin only

GET    /api/v1/admin/processing-queue/alerts
  - Returns: List of active alerts (stuck docs, high failure rate, queue depth)
  - Auth: Admin only

GET    /api/v1/admin/analysis/{analysisId}/compare/{otherAnalysisId}
  - Returns: Diff of two analyses (added/removed mechanics, changed FAQ, score delta)
  - Auth: Admin only
```

---

## Phase 5: Fine-Tuned Model (Future)

**Epic**: Domain-Specific Rulebook Analysis Model
**Priority**: P3 (after Phases 1-4 prove value and build dataset)
**Estimated Issues**: ~20
**Dependencies**: Phase 2 complete + 200+ validated analyses as training data

### Problem Statement

Generic LLMs produce ~70-80% accuracy on mechanic extraction and generate generic FAQs. A fine-tuned model trained on validated rulebook analyses will achieve ~90-95% accuracy, consistent JSON output, lower latency (5-15s vs 30-120s), and lower cost per analysis.

### Approach

This phase is intentionally deferred. Phases 1-4 serve a dual purpose:
1. **Deliver value now** with the generic LLM
2. **Build the training dataset** — every validated `RulebookAnalysis` becomes a training example

### Prerequisites Before Starting Phase 5

| Prerequisite | Target | Measured By |
|-------------|--------|-------------|
| Validated analyses count | >= 200 | `SELECT COUNT(*) FROM rulebook_analyses WHERE is_active AND confidence_score > 0.7` |
| Human-reviewed analyses | >= 50 | Admin review + manual correction of top-50 most-played games |
| Language diversity | >= 3 languages with 30+ analyses each | Distribution query |
| Quality baseline established | Average confidence > 0.70 with generic LLM | Phase 4 metrics |

### High-Level Steps

1. **Dataset preparation**: Export validated analyses as instruction-tuning pairs
2. **Model selection**: Benchmark Llama 3.1 8B, Mistral 7B, Phi-3 on 50 test rulebooks
3. **LoRA/QLoRA training**: Fine-tune on training set (3-8 hours per run)
4. **Evaluation**: Compare against golden test set (50 held-out rulebooks)
5. **A/B deployment**: Run fine-tuned alongside generic, compare quality metrics
6. **Rollout**: Switch `HybridLlmService` routing to prefer fine-tuned model for rulebook analysis
7. **Iteration**: Monthly re-training as dataset grows

### Architecture Impact

```
Current:
  LlmRulebookAnalyzer → OpenRouter (generic LLM)

Phase 5:
  LlmRulebookAnalyzer → HybridLlmService routing:
    ├── RequestSource.RulebookAnalysis → Ollama (fine-tuned model)
    ├── RequestSource.RulebookAnalysis + Ollama unavailable → OpenRouter (fallback)
    └── Other sources → existing routing logic
```

No interface changes. `IRulebookAnalyzer` contract remains identical. Only the `LlmRulebookAnalyzer` implementation's prompt and model routing change.

---

## Implementation Roadmap

```
Phase 1: Document Classification (~12 issues)         ████████░░░░  ~2 weeks
  └── DocumentCategory enum, BaseDocumentId FK, language detection,
      copyright disclaimer, UI selectors, active toggle

Phase 2: Complete RulebookAnalysis (~15 issues)        ████████████  ~3 weeks
  └── ExtractKeyConcepts, GenerateQuestions, ExtractStateSchema,
      structured RAG fusion, complexity routing, critical section gates
  ⚠️ Depends on Phase 1 (category determines pipeline routing)

Phase 3: Admin Queue Management (~10 issues)           ████████░░░░  ~2 weeks
  └── Priority system, pause/resume, cancel, rate limiting,
      backpressure, admin UI
  ⚠️ Depends on Phase 1 (category determines priority)

Phase 4: Observability (~8 issues)                     ██████░░░░░░  ~1.5 weeks
  └── Metrics dashboard, alerts, per-phase timing, analysis comparison
  ⚠️ Depends on Phase 3 (queue provides data points)

Phase 5: Fine-Tuned Model (~20 issues)                 ░░░░░░░░░░░░  Future
  └── Dataset prep, model training, A/B testing, rollout
  ⚠️ Depends on 200+ validated analyses from Phases 1-4
```

### Dependency Graph

```
Phase 1 ──┬──→ Phase 2 ──→ Phase 5 (dataset from Phase 2 outputs)
           │
           └──→ Phase 3 ──→ Phase 4
```

Phases 2 and 3 can proceed in parallel after Phase 1 completes.

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Generic LLM accuracy insufficient for FAQ/glossary | Medium | High | Iterative prompt engineering; Phase 5 as fallback strategy |
| OCR quality too low for scanned rulebooks | Medium | Medium | SmolDocling fallback; manual text upload option for admin |
| Processing queue bottleneck at scale (>100 users) | Low | High | Configurable worker count; backpressure; priority lanes |
| Copyright legal challenge from publishers | Low | High | Disclaimer, private-only, transformative output; consult legal |
| Language detection false positives (reject valid PDF) | Low | Low | Manual override by admin; conservative detection (flag, don't reject) |
| Structured RAG fusion degrades answer quality | Low | Medium | A/B test; confidence-gated fallback to vector-only |

---

## Success Metrics

| Metric | Baseline (current) | Target (Phase 2 complete) | Target (Phase 5 complete) |
|--------|-------------------|--------------------------|--------------------------|
| Mechanic extraction accuracy | ~70% | > 80% | > 93% |
| FAQ relevance (human-rated) | N/A (no FAQ) | > 75% | > 90% |
| Glossary completeness | N/A (no glossary) | > 85% | > 95% |
| RAG answer quality (human-rated) | ~65% | > 78% (structured fusion) | > 88% |
| Analysis processing time (avg) | ~90s | < 75s | < 20s |
| Analysis failure rate | ~8% | < 5% | < 2% |
| Queue visibility | View-only | Full admin control | Full admin control |

---

**Last Updated**: 2026-03-08
**License**: Proprietary
