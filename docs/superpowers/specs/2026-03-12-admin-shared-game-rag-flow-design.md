# Admin Shared Game Creation + RAG Chat Testing — Specification

**Date**: 2026-03-12
**Status**: Draft
**Related Epics**: #236 (Admin Shared Game Content Management), #234 (Seeding Architecture)

## 1. User Story

> As an **Admin**, I want to add a game ("Descent: Journeys in the Dark") to the shared game catalog by:
> 1. Creating the game via BGG search/import
> 2. Uploading the PDF rulebook
> 3. Monitoring the embedding pipeline progress in real-time
> 4. Saving/publishing the game
> 5. Testing the RAG chat to verify the knowledge base works
>
> So that the game is fully available to users with AI-powered rulebook Q&A.

## 2. Current State Analysis

### 2.1 What Exists (✅)

| Step | Endpoint | Status |
|------|----------|--------|
| BGG Search | `GET /api/v1/bgg/search?q=Descent` | Functional, cached 7d |
| Game Import | `POST /admin/games/wizard/create` (BggId) | Creates SharedGame in Draft |
| PDF Upload | `POST /api/v1/ingest/pdf` (100MB limit) | Full chunked upload support |
| Launch Processing | `POST /admin/games/wizard/{id}/launch-processing` | Enqueues with AdminPriority |
| Progress Polling | `GET /api/v1/knowledge-base/{gameId}/status` | Maps 7-state → 6-state frontend |
| SSE Progress Stream | `GET /admin/games/wizard/{id}/progress/stream` | 1.5s polling, auto-close |
| Submit for Approval | `POST /admin/shared-games/{id}/submit-for-approval` | Draft → PendingApproval |
| Approve Publication | `POST /admin/shared-games/{id}/approve-publication` | PendingApproval → Published |
| Agent Chat (SSE) | `POST /api/v1/agents/{id}/chat` | Full RAG streaming |

### 2.2 What's Missing (❌)

| Gap | Impact | Severity |
|-----|--------|----------|
| **No Descent PDF** in `data/rulebook/` | Cannot demo the user story | 🔴 CRITICAL |
| **Agent auto-creation** (Wizard Phase 4 TODO) | Admin must manually create agent after KB ready | 🔴 CRITICAL |
| **Descent not in YAML manifests** | Seeding won't include Descent for dev/staging | 🟡 IMPORTANT |
| **No E2E test** for full admin flow | No automated regression coverage | 🟡 IMPORTANT |
| **No "one-click publish"** shortcut | Admin must do Submit + Approve separately | 🟢 RECOMMENDED |

### 2.3 Architecture Context

The flow crosses **4 bounded contexts**:

```
SharedGameCatalog ──→ DocumentProcessing ──→ KnowledgeBase ──→ AgentManagement
   (game CRUD)         (PDF pipeline)        (RAG vectors)     (chat agents)
```

**Orchestrator**: `AdminGameWizardEndpoints` coordinates the cross-BC flow via MediatR commands.

## 3. Spec Panel Expert Analysis

### 3.1 Karl Wiegers — Requirements Quality

**Issues identified**:
- User story lacks acceptance criteria for "successful RAG test"
- No defined threshold for embedding quality (e.g., minimum chunk count, retrieval accuracy)
- "Save the game" is ambiguous — does it mean Draft save or full Publish?

**Recommended acceptance criteria**:
1. BGG search returns "Descent: Journeys in the Dark" with correct BggId
2. PDF upload completes and transitions through all 7 processing states to `Ready`
3. Progress endpoint shows monotonically increasing percentage (0→100%)
4. Agent is automatically created with game-specific system prompt
5. RAG chat query "How does combat work in Descent?" returns passages from the uploaded rulebook
6. Game reaches `Published` status in shared catalog

### 3.2 Gojko Adzic — Specification by Example

**Concrete scenarios**:

```gherkin
Scenario: Admin imports Descent from BGG
  Given admin searches BGG for "Descent"
  When results are displayed
  Then "Descent: Journeys in the Dark (Second Edition)" appears with BggId
  And admin selects it to create a SharedGame in Draft status

Scenario: PDF upload triggers embedding pipeline
  Given Descent game exists in Draft status
  When admin uploads "descent_rulebook.pdf" (≤100MB)
  And admin clicks "Launch Processing"
  Then PdfProcessingState transitions: Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready
  And progress endpoint reports percentage 0% → 100%

Scenario: Agent auto-created on KB ready
  Given Descent PDF processing reaches Ready state
  When VectorDocument is created for the game
  Then a default RAG agent is auto-created with:
    | Field | Value |
    | GameId | <descent-game-id> |
    | Type | GameAssistant |
    | Strategy | RagBased |
    | SystemPrompt | Contains "Descent" game context |

Scenario: RAG chat returns rulebook content
  Given Descent has a Ready knowledge base and active agent
  When admin sends "How does line of sight work?"
  Then SSE stream returns token events with content from the Descent rulebook
  And response references specific rules or page sections
```

### 3.3 Michael Nygard — Production Reliability

**Failure modes to address**:
- **PDF extraction failure**: Retry mechanism exists (`RetryCount`, `RetryJobCommand`), but auto-retry count should be configurable
- **Embedding service unavailable**: Circuit breaker needed between API and embedding-service
- **Agent creation race condition**: If two admins process the same game simultaneously, agent deduplication needed
- **Stale progress**: SSE stream should timeout after max processing time (configurable, suggest 30min)

### 3.4 Martin Fowler — Architecture

**Recommendation**: Agent auto-creation should be an **event-driven side effect**, not wizard endpoint logic:
- `PdfProcessingState.Ready` → Domain event `KnowledgeBaseReadyEvent`
- Event handler creates default agent if none exists for the game
- This decouples the wizard from agent management and works for non-wizard flows too

## 4. Proposed Solution

### 4.1 Agent Auto-Creation (Critical Gap)

**Approach**: Event-driven via `KnowledgeBaseReadyEvent`

```
PdfDocument.TransitionToReady()
  → Raise KnowledgeBaseReadyEvent(GameId, SharedGameId, PdfDocumentId)
    → Handler: CreateDefaultAgentOnKbReadyHandler
      → Check: Agent with GameId exists? Skip : Create default agent
      → Agent config: GameAssistant type, RagBased strategy, default LLM config
```

**Why event-driven over wizard endpoint**:
- Works for seeding pipeline (PDFs processed by Quartz job)
- Works for bulk upload (issue #117)
- Works for future API-only flows
- Single source of truth for "KB ready → agent exists"

### 4.2 Descent PDF & Manifest

1. Acquire Descent rulebook PDF → `data/rulebook/descent_rulebook.pdf`
2. Add filename→game mapping in `PdfSeeder` dictionary
3. Add Descent entry to `dev.yml` and `staging.yml` YAML manifests
4. Seeding pipeline will auto-process: PDF → Pending → (Quartz job) → Ready → Agent

### 4.3 One-Click Publish (Optional)

New endpoint: `POST /admin/shared-games/{id}/quick-publish`
- Combines Submit + Approve in single operation
- Only available for admin roles
- Skips PendingApproval state (goes Draft → Published directly)

## 5. Issue Breakdown

### Epic: Admin Shared Game + RAG Flow (E2E)

| # | Title | Type | Priority | Blocked By |
|---|-------|------|----------|------------|
| 1 | Add Descent rulebook PDF to data/rulebook | task | 🔴 CRITICAL | — |
| 2 | Add Descent to YAML seeding manifests | task | 🟡 IMPORTANT | #1, Epic #234 |
| 3 | Auto-create agent on KnowledgeBaseReady event | feature | 🔴 CRITICAL | — |
| 4 | E2E integration test: admin game→PDF→RAG flow | test | 🟡 IMPORTANT | #3 |
| 5 | Quick-publish endpoint for admin games | enhancement | 🟢 RECOMMENDED | — |

### Dependency Graph

```
#1 Descent PDF ──→ #2 YAML Manifests
                         │
                         ↓
#3 Agent Auto-Create ──→ #4 E2E Integration Test

#5 Quick-Publish (independent)
```

## 6. Out of Scope

- Frontend wizard UI changes (covered by Epic #236, issue #118)
- Bulk PDF upload (covered by issue #117)
- Per-game document overview (covered by issue #119)
- Seeding architecture changes (covered by Epic #234)

## 7. Success Criteria

1. Admin can execute the full flow: BGG search → import → PDF upload → monitor progress → publish → chat with RAG
2. Agent is automatically created when KB reaches Ready state
3. Descent game is seeded in dev/staging environments with working RAG
4. E2E test validates the entire flow in CI

## 8. Technical Notes

- **CQRS**: All new endpoints must use `IMediator.Send()` exclusively
- **Event handler**: Use `INotificationHandler<KnowledgeBaseReadyEvent>` (MediatR notification)
- **Idempotency**: Agent creation must be idempotent (skip if agent exists for GameId)
- **Testing**: Unit tests for event handler + integration test with Testcontainers
- **PDF format**: Filename convention `descent_rulebook.pdf` (lowercase, underscore-separated)
