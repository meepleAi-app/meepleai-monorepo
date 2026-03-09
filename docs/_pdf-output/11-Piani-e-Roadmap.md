# MeepleAI - Piani e Roadmap

PRD, piani di implementazione, epic, roadmap funzionale, specifiche voice input.

**Data generazione**: 8 marzo 2026

**File inclusi**: 21

---

## Indice

1. plans/2026-03-06-admin-hub-completion-epic.md
2. plans/2026-03-06-agent-rag-context-improvement.md
3. plans/2026-03-06-mobile-first-ux-epic.md
4. plans/2026-03-06-rag-mock-removal-plan.md
5. plans/2026-03-06-toolkit-evolution-epic.md
6. plans/2026-03-07-game-ecosystem-prd.md
7. plans/2026-03-07-phase0-implementation-plan.md
8. plans/2026-03-07-publisher-db-schema.md
9. plans/2026-03-07-stack-optimization-design.md
10. plans/2026-03-07-stack-optimization-implementation.md
11. plans/2026-03-08-llm-system-improvement-prd.md
12. plans/2026-03-08-pdf-rulebook-implementation-plan.md
13. plans/2026-03-08-pdf-rulebook-processing-prd.md
14. plans/2026-03-08-rule-source-card-design.md
15. plans/epic-4746-live-game-session-plan.md
16. plans/epic-entity-link-toolkit-nav-plan.md
17. plans/mock-audit-implementation-list.md
18. plans/voice-input/voice-input-specification.md
19. plans/voice-input/web-speech-api-research.md
20. roadmap/p1-high-execution-plan.md
21. roadmap/playground-poc-rag-debug-sequence.md

---



<div style="page-break-before: always;"></div>

## plans/2026-03-06-admin-hub-completion-epic.md

# Admin Hub Completion Epic - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Complete the 4 stub admin hub pages (Monitor, Config, Analytics, AI) by wiring pre-built components into tab layouts, and fix navigation so all 10 admin sections are discoverable.

**Architecture:** Each hub page already exists with tab structure and NavConfig. The migration replaces placeholder divs with real tab content components. Each tab is a `'use client'` component that fetches data via `adminClient` and renders existing presentational components. Pattern: follow `content/SharedGamesTab.tsx`.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind 4, shadcn/ui, Zustand, React Query, Zod, adminClient API

**Source of truth:** Spec-panel analysis session 2026-03-06. Backend is 100% ready (60+ commands, 90+ queries, 150+ endpoints). This is purely frontend work.

---

## Phase 0: Quick Wins (Navigation + Route Consolidation)

**Branch:** `feature/issue-5040-admin-nav-completion`
**Parent:** `main-dev`
**Issues:** Part of #5040

### Task 0.1: Extend DASHBOARD_SECTIONS to include hub pages

**Files:**
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`
- Test: `apps/web/src/config/__tests__/admin-dashboard-navigation.test.ts` (create)

**Context:** Currently `DASHBOARD_SECTIONS` has 5 sections (overview, users, shared-games, agents, knowledge-base). The 5 hub pages (ai, analytics, config, content, monitor) are unreachable from navigation. Add them as sections so `getActiveSection()` resolves correctly and sidebar shows their items.

**Step 1: Write failing test**

Create `apps/web/src/config/__tests__/admin-dashboard-navigation.test.ts`:

*(blocco di codice rimosso)*

**Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/config/__tests__/admin-dashboard-navigation.test.ts`
Expected: FAIL — "should have 10 dashboard sections" (currently 5)

**Step 3: Add 5 new sections to navigation config**

Modify `apps/web/src/config/admin-dashboard-navigation.ts`:

Add imports at top (alongside existing ones):
*(blocco di codice rimosso)*

Add 5 new sections to `DASHBOARD_SECTIONS` array (after knowledge-base):

*(blocco di codice rimosso)*

**Step 4: Run tests and verify they pass**

Run: `cd apps/web && pnpm vitest run src/config/__tests__/admin-dashboard-navigation.test.ts`
Expected: PASS (10/10 tests)

**Step 5: Commit**
*(blocco di codice rimosso)*

---

### Task 0.2: Verify typecheck and existing tests still pass

**Step 1: Run typecheck**
Run: `cd apps/web && pnpm typecheck`
Expected: PASS (no new TS errors)

**Step 2: Run existing admin tests**
Run: `cd apps/web && pnpm vitest run --reporter=verbose 2>&1 | tail -20`
Expected: All existing tests still PASS

**Step 3: Commit if any adjustments needed**

---

## Phase 1: Monitor Hub Migration (#5053)

**Branch:** `feature/issue-5053-admin-monitor-hub`
**Parent:** `main-dev` (or from Phase 0 branch if not yet merged)
**Issues:** #5053

**Pre-built components to reuse:**
- `AlertRuleForm` (`components/admin/alert-rules/AlertRuleForm.tsx`)
- `AlertRuleList` (`components/admin/alert-rules/AlertRuleList.tsx`)
- `AlertsBanner` (`components/admin/AlertsBanner.tsx`)
- `ServiceHealthMatrix` (`components/admin/ServiceHealthMatrix.tsx`)
- `CommandCenterDashboard` (`components/admin/command-center/CommandCenterDashboard.tsx`)
- `GrafanaEmbed` (`components/admin/GrafanaEmbed.tsx`)

**AdminClient methods already available:**
- Alerts: `getStats()` has alert data, alert rules via dedicated schemas
- Cache: `clearKBCache()`, resource metrics via `getInfrastructureDetails()`
- Infrastructure: `getInfrastructureDetails()`, `getMetricsTimeSeries()`
- Services: data from infrastructure details response
- Testing: `getAccessibilityMetrics()`, `getPerformanceMetrics()`, `getE2EMetrics()`
- Export: `exportUsersToCSV()`, `exportAuditLogs()`, `exportApiKeysToCSV()`

### Task 1.1: Create AlertsTab component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/AlertsTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/AlertsTab.test.tsx`

**Step 1: Write failing test**

*(blocco di codice rimosso)*

**Step 2: Implement AlertsTab**

*(blocco di codice rimosso)*

**Step 3: Run test, verify pass, commit**

*(blocco di codice rimosso)*

---

### Task 1.2: Create InfrastructureTab component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/InfrastructureTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/InfrastructureTab.test.tsx`

**Step 1: Write failing test**

*(blocco di codice rimosso)*

**Step 2: Implement InfrastructureTab**

*(blocco di codice rimosso)*

**Step 3: Run test, verify pass, commit**

*(blocco di codice rimosso)*

---

### Task 1.3: Create CacheTab component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/CacheTab.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/CacheTab.test.tsx`

**Step 1: Write test, Step 2: Implement**

The CacheTab shows cache metrics (hit rate, memory usage, keys) and a "Clear Cache" button. Uses `adminClient.clearKBCache()` for the action and `adminClient.getInfrastructureDetails()` for metrics.

*(blocco di codice rimosso)*

**Step 3: Commit**
*(blocco di codice rimosso)*

---

### Task 1.4: Create CommandCenterTab and TestingTab wrappers

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/CommandCenterTab.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/TestingTab.tsx`

These are thin wrappers around existing components:

*(blocco di codice rimosso)*

*(blocco di codice rimosso)*

**Commit:**
*(blocco di codice rimosso)*

---

### Task 1.5: Wire tabs into Monitor hub page

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/monitor/__tests__/page.test.tsx`

**Step 1: Write failing test**

*(blocco di codice rimosso)*

**Step 2: Replace placeholder in page.tsx with real tab rendering**

Follow the `content/page.tsx` pattern exactly. Replace the placeholder `<div>` with a `renderTabContent()` switch that returns Suspense-wrapped tab components.

*(blocco di codice rimosso)*

**Step 3: Run tests, verify pass, commit**

*(blocco di codice rimosso)*

---

### Task 1.6: Typecheck + full test suite

**Step 1:** `cd apps/web && pnpm typecheck`
**Step 2:** `cd apps/web && pnpm vitest run`
**Step 3:** Fix any issues, commit

*(blocco di codice rimosso)*

---

## Phase 2: Config Hub Migration (#5052)

**Branch:** `feature/issue-5052-admin-config-hub`
**Parent:** `main-dev`

**Pre-built components:**
- `FeatureFlagsTab` (`components/admin/FeatureFlagsTab.tsx`) — FULLY BUILT with toggles, tier management, bulk actions
- `PdfLimitsConfig` (`components/admin/PdfLimitsConfig.tsx`)

**AdminClient methods:**
- Feature flags: uses `api.admin` config endpoints
- Rate limits: `AdminConfigEndpoints.cs` — GET/PUT pdf-limits per tier
- Session limits: `SessionLimitsConfigEndpoints.cs`
- PDF tier limits: `PdfTierUploadLimitsConfigEndpoints.cs`

### Task 2.1: Create FeatureFlagsWrapper (data-fetching wrapper)

Since `FeatureFlagsTab` already accepts `configurations: SystemConfigurationDto[]` props, create a wrapper that fetches and passes data.

*(blocco di codice rimosso)*

### Task 2.2: Create LimitsTab (PDF + Session limits)

Wraps `PdfLimitsConfig` and adds session limits config.

### Task 2.3: Create RateLimitsTab

New component using rate limit admin endpoints.

### Task 2.4: Wire tabs into Config hub page

Same pattern as Monitor hub: replace placeholder with `renderTabContent()` switch.

### Task 2.5: Tests + typecheck

---

## Phase 3: Analytics Hub Migration (#5051)

**Branch:** `feature/issue-5051-admin-analytics-hub`
**Parent:** `main-dev`

**AdminClient methods available:**
- `getAiRequests(params)` — AI request analytics
- `getAnalytics(params)` — general analytics
- `getAuditLogs(params)` + `exportAuditLogs(params)` — audit log
- `generateReport(request)` + `scheduleReport(request)` — reports
- `getApiKeysWithStats(params)` — API key management
- `getPdfAnalytics(days)`, `getChatAnalytics(days)`, `getModelPerformance(days)`

### Task 3.1: Create AiUsageTab

Charts showing AI request volume, cost breakdown, model performance. Reuses existing chart components from `components/admin/charts/`.

### Task 3.2: Create AuditLogTab

Searchable, filterable table of audit log entries with CSV export button. Uses `adminClient.getAuditLogs()` and `adminClient.exportAuditLogs()`.

### Task 3.3: Create ReportsTab

Report generation UI + scheduled reports list. Uses `adminClient.generateReport()`, `getScheduledReports()`, `getReportExecutions()`.

### Task 3.4: Create ApiKeysTab

API key management table with stats, delete, bulk import/export. Uses `adminClient.getApiKeysWithStats()`, `deleteApiKey()`, `exportApiKeysToCSV()`.

### Task 3.5: Wire tabs into Analytics hub page

### Task 3.6: Tests + typecheck

---

## Phase 4 (Optional): AI Hub Migration (#5048)

**Branch:** `feature/issue-5048-admin-ai-hub`
**Parent:** `main-dev`

Lower priority since most AI features are already accessible via `/admin/agents/*` section. The AI hub consolidates:
- Agent Typologies (existing page content)
- AI Lab (playground wrappers)
- Prompts (link to existing prompt management)
- Models (link to existing models page)
- RAG (link to existing pipeline/debug)

This phase may be deferred as it's more consolidation than new capability.

---

## Commit Strategy

Each phase = 1 PR to `main-dev`:
- Phase 0: `feat(web): extend admin navigation with hub sections (#5040)`
- Phase 1: `feat(web): complete Monitor hub with real tab content (#5053)`
- Phase 2: `feat(web): complete Config hub with real tab content (#5052)`
- Phase 3: `feat(web): complete Analytics hub with real tab content (#5051)`

## Definition of Done per Phase

- [ ] All tabs render real content (no placeholder divs)
- [ ] Tab skeleton loading states work
- [ ] Tests pass (unit + typecheck)
- [ ] No new TS errors introduced
- [ ] Navigation resolves correctly for all hub URLs
- [ ] PR to `main-dev`, code review, merge
- [ ] Close related GitHub issues

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| AdminClient methods don't match actual backend DTOs | Verify with `GET /scalar/v1` API docs before implementing |
| Existing components need props we can't provide | Add fallback/optional props, don't modify component contracts |
| Hub tab URLs (`?tab=x`) don't work with sidebar `isSidebarItemActive` | Test with regex patterns in `activePattern` |
| Existing tests break due to navigation changes | Run full test suite after Phase 0 before proceeding |


---



<div style="page-break-before: always;"></div>

## plans/2026-03-06-agent-rag-context-improvement.md

# Agent + RAG + Chat Context Improvement Plan

**Date**: 2026-03-06
**Epic**: Improve Agent RAG Pipeline & Chat Context Management
**Status**: SPEC COMPLETE - Ready for implementation
**Priority**: CRITICAL - Core agent functionality is non-functional without this

## Problem Statement

The agent system has 14 RAG strategies defined but **zero execute real logic**. The `AgentPromptBuilder` builds prompts WITHOUT retrieved documents. The `ChatWithSessionAgentCommandHandler` uses a non-streaming LLM path and ignores chat history entirely. Current golden set validation: **0/20 questions pass (0% accuracy)**.

## Decisions (from Spec Panel)

| # | Question | Decision |
|---|----------|----------|
| 1 | Context window size | Use maximum available (128K for llama3.3:70b) |
| 2 | AgentPromptBuilder layer | DELETE old Domain service, replace with Application layer service (needs infra deps) |
| 6 | Backward compatibility | NOT needed - app not distributed. Delete all legacy code. |
| 3 | First-token-time SLA | <2s acceptable (streaming preferred but buffered OK) |
| 4 | Show chunks to users | No - debug only for devs/admin |
| 5 | Golden test set | EXISTS: `tests/fixtures/agent-validation-questions.json` (20 chess Qs) |

## Architecture

### New: `RagPromptAssemblyService` (Application Layer)

Replaces current `AgentPromptBuilder` (Domain layer) as the orchestration hub.

*(blocco di codice rimosso)*

### Token Budget Allocation

For a model with `maxTokens` context window:
- Reserve 25% for response generation
- Remaining 75% split:
  - System prompt (persona + instructions): 15%
  - RAG context (retrieved chunks): 35%
  - Chat history (summary + recent): 20%
  - User message + formatting: 5%

When budget exceeded: truncate oldest history first, then reduce chunk count. NEVER truncate user question or system persona.

### Chat History Strategy

*(blocco di codice rimosso)*

### Chunk Formatting (for LLM, NOT shown to users)

*(blocco di codice rimosso)*

### Failure Modes

| Failure | Behavior |
|---------|----------|
| Qdrant unavailable | Return "I don't have access to game documents right now. Please try again." |
| No relevant chunks (all below minScore) | LLM responds with disclaimer: "I couldn't find specific rules. Based on general knowledge..." |
| Reranker timeout | Skip reranking, use raw Qdrant scores (graceful degradation) |
| Token budget exceeded | Truncate oldest history first, then reduce chunks. Never truncate question. |
| LLM failure | Circuit breaker + fallback. Return error via SSE Error event. |

---

## Implementation Phases

### Phase 0: Core Pipeline (CRITICAL - ~8h)

Goal: Make agents actually USE RAG context and chat history. Takes system from 0/20 to ~14/20.

#### 0.1 Create `RagPromptAssemblyService` (Application Layer)

**New file**: `BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`

**Interface**: `IRagPromptAssemblyService`
*(blocco di codice rimosso)*

**Returns**:
*(blocco di codice rimosso)*

**Dependencies** (injected):
- `IEmbeddingService` - generate query embedding
- `IQdrantService` - vector search
- `ILogger`

**Logic**:
1. Generate embedding for `userQuestion`
2. Search Qdrant with `gameId` filter, top-k=10, minScore=0.55
3. Format chunks into context string
4. Build system prompt: persona instructions + formatted chunks
5. Build user prompt: chat history (if any) + current question
6. Track citations for debug
7. Estimate total tokens, warn if exceeding budget

#### 0.2 Refactor `ChatWithSessionAgentCommandHandler`

**Current state** (line 147-151): Builds prompt with ONLY game state, NO RAG context, NO history.
**Current state** (line 155-163): Non-streaming LLM call, sends entire response as single Token event.

**Changes**:
1. Inject `IRagPromptAssemblyService` (replace `IAgentPromptBuilder`)
2. Inject `IGameRepository` (to resolve game title from `agentSession.GameSessionId`)
3. Call `AssemblePromptAsync()` with all context
4. Use `GenerateCompletionStreamAsync()` for real token streaming
5. Yield Token events as they arrive
6. Include chat history from `thread.Messages`
7. Track citations in Complete event metadata

**Streaming change** (critical for UX):
*(blocco di codice rimosso)*

Note: `yield return` cannot be in try-catch. Use the wrapper pattern already in place (HandleCore).

#### 0.3 Wire Chat History into Prompt

In `RagPromptAssemblyService.AssemblePromptAsync()`:

*(blocco di codice rimosso)*

#### 0.4 Wire ConversationSummary Generation

When `chatThread.Messages.Count > HISTORY_THRESHOLD` and `ConversationSummary` is stale:

*(blocco di codice rimosso)*

Summary prompt:
*(blocco di codice rimosso)*

### Phase 1: Quality Improvements (~6h)

#### 1.1 Wire Reranker Service

If `IRerankerService` exists and is available, rerank after Qdrant search:

*(blocco di codice rimosso)*

#### 1.2 Citation Extraction

Map used chunks to `ChunkCitation` records. Pass through SSE as debug metadata (not shown to users).

#### 1.3 Confidence Scoring

After LLM response, compute naive confidence:
- Base: average relevance score of used chunks
- Penalty: -0.1 if no chunks above 0.8 score
- Penalty: -0.1 if response contains hedge words ("forse", "probabilmente", "non sono sicuro")

#### 1.4 Query Expansion (optional)

Use LLM to expand query with synonyms before embedding:
*(blocco di codice rimosso)*

### Phase 2: Advanced Strategies (~8h)

#### 2.1 ChainOfThoughtRAG

Before generating final answer, have LLM reason step-by-step:
*(blocco di codice rimosso)*

#### 2.2 SentenceWindowRAG

When a chunk matches, also include surrounding chunks (chunk_index +/- 1) for more context.

#### 2.3 HybridSearch (Vector + PostgreSQL FTS)

Combine Qdrant vector search (semantic) with PostgreSQL full-text search (keyword) using Reciprocal Rank Fusion.

---

## Testing Plan

### Unit Tests (RagPromptAssemblyService)

| Test | Scenario |
|------|----------|
| `AssemblePrompt_WithChunks_IncludesFormattedContext` | 5 chunks formatted in prompt |
| `AssemblePrompt_WithNoChunks_IncludesDisclaimer` | Below minScore disclaimer |
| `AssemblePrompt_WithChatHistory_IncludesMessages` | 3 prior messages in prompt |
| `AssemblePrompt_WithLongHistory_UsesSummary` | 30 messages -> summary + recent 5 |
| `AssemblePrompt_QdrantUnavailable_ReturnsGracefulError` | Qdrant down handling |
| `AssemblePrompt_TokenBudget_TruncatesOldHistory` | Budget exceeded truncation |

### Integration Tests (Handler)

| Test | Scenario |
|------|----------|
| `ChatWithSessionAgent_StreamsTokens` | Verify multiple Token events (not single) |
| `ChatWithSessionAgent_IncludesRAGContext` | Prompt contains retrieved chunks |
| `ChatWithSessionAgent_PersistsHistory` | Messages saved to ChatThread |
| `ChatWithSessionAgent_HandlesLlmFailure` | Error event on LLM failure |

### Golden Set Validation

Run existing `RagQualityValidationTests` after Phase 0 complete.
Target: **>= 14/20 (70%)** after Phase 0, **>= 18/20 (90%)** after Phase 1.

File: `tests/fixtures/agent-validation-questions.json` (20 chess questions, already structured)
Runner: `RagQualityValidationTests.cs` (already exists, Skip attribute to be removed when backend is live)

---

## Files to Create/Modify

### New Files
| File | Description |
|------|-------------|
| `Application/Services/IRagPromptAssemblyService.cs` | Interface |
| `Application/Services/RagPromptAssemblyService.cs` | Implementation |
| `Application/Models/AssembledPrompt.cs` | Result record |
| `Application/Models/ChunkCitation.cs` | Citation record |
| `Application/Services/TokenBudgetAllocator.cs` | Token budget logic |

### Modified Files
| File | Changes |
|------|---------|
| `Application/Handlers/ChatWithSessionAgentCommandHandler.cs` | Inject IRagPromptAssemblyService, use streaming, include history |
| `Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | Register new services, remove old registrations |

### Deleted Files (no backward compat needed - app not distributed)
| File | Reason |
|------|--------|
| `Domain/Services/IAgentPromptBuilder.cs` | Replaced by IRagPromptAssemblyService |
| `Domain/Services/AgentPromptBuilder.cs` | Replaced by RagPromptAssemblyService |

### NOT Modified (kept as-is)
| File | Reason |
|------|--------|
| `AskAgentQuestionCommandHandler.cs` | POC endpoint, separate from session chat. Has its own BuildContextFromChunks. |
| Frontend SSE handling | Already parses Token events correctly |
| `ChatThread.cs` | ConversationSummary already exists |

---

## Success Metrics

| Metric | Current | Phase 0 Target | Phase 1 Target |
|--------|---------|----------------|----------------|
| Golden set accuracy | 0/20 (0%) | 14/20 (70%) | 18/20 (90%) |
| Avg confidence | 0.00 | >= 0.60 | >= 0.70 |
| Citation rate | 0% | >= 50% | >= 95% |
| First token time | N/A (buffered) | < 2s | < 1s |
| Chat history in prompt | No | Yes (last 10) | Yes (summary + recent) |
| Hallucination rate | N/A | < 10% | < 3% |

---

## Dependencies

- Qdrant running with indexed documents (chess rulebook for golden set)
- Embedding service running (port 8000)
- Ollama running (port 11434) OR OpenRouter API key configured
- Reranker service (port 8001) - optional, graceful degradation


---



<div style="page-break-before: always;"></div>

## plans/2026-03-06-mobile-first-ux-epic.md

# Epic: Mobile-First UX Redesign

> **Status**: PLANNED
> **Created**: 2026-03-06
> **Branch**: `feature/mobile-first-ux` (from `frontend-dev`)
> **Parent branch**: `frontend-dev`
> **Expert Panel**: Cockburn (UX), Fowler (Architecture), Wiegers (Requirements), Nygard (Production), Crispin (Quality)

---

## Problem Statement

The transition from BottomNav to FloatingActionBar caused a **critical mobile navigation regression**. Mobile users have no persistent global navigation — the only way to move between sections is through a hamburger menu (3 taps vs 1). Additionally, several spec features (SmartFAB, mobile breadcrumb, scroll behaviors) were never implemented, and public pages lack consistent responsive design.

## Evidence

- Screenshots: library.png, libraryMobile.png, createAgent.png
- Navigability analysis: docs/frontend/navigability-analysis.md
- Layout spec: docs/frontend/layout-spec.md (SmartFAB, ActionBar priority, breadcrumb)
- Deprecated BottomNav still in codebase (components/layout/BottomNav.tsx)
- FloatingActionBar: context actions only, NOT navigation — disappears when no actions configured

## Architecture

*(blocco di codice rimosso)*

---

## Sprint 1 — P0: Fix Broken Mobile Navigation

### Issue 1: MobileTabBar Component
**Priority**: CRITICAL
**Effort**: 2 days
**Dependencies**: None

**Description**: Create a new persistent bottom tab bar for mobile navigation, replacing the deprecated BottomNav with a design that matches the current glassmorphism design system.

**Acceptance Criteria**:
- [ ] New component: `src/components/layout/MobileTabBar/MobileTabBar.tsx`
- [ ] 5 tabs: Dashboard (`/dashboard`), Library (`/library`), Discover (`/games`), Chat (`/chat/new`), Profile (`/profile`)
- [ ] Visible only on mobile: `md:hidden`
- [ ] Fixed bottom, z-40
- [ ] Height: 72px + `env(safe-area-inset-bottom)`
- [ ] Glassmorphism: `bg-card/90 backdrop-blur-md border-t border-border/50`
- [ ] Active state: primary color + semibold + filled icon
- [ ] Inactive: muted-foreground + outline icon
- [ ] Auth-gated: Guest sees Dashboard + Discover only
- [ ] Touch targets: 44x44px minimum (WCAG 2.1 AA)
- [ ] `aria-label="Primary navigation"`, `aria-current="page"` on active
- [ ] Font: Nunito 10px labels
- [ ] Integrated into LayoutShell (below FloatingActionBar in DOM)
- [ ] Tests: component test + responsive behavior test

**Technical Notes**:
- Use `useAuthUser()` for auth gating (same pattern as old BottomNav)
- Use `usePathname()` for active state (prefix matching)
- Do NOT use deprecated BottomNav — new component from scratch
- Coexistence: FloatingActionBar moves to `bottom-[calc(72px+1.5rem)]` on mobile

**Files to create/modify**:
- CREATE: `src/components/layout/MobileTabBar/MobileTabBar.tsx`
- CREATE: `src/components/layout/MobileTabBar/index.ts`
- CREATE: `src/components/layout/MobileTabBar/__tests__/MobileTabBar.test.tsx`
- MODIFY: `src/components/layout/LayoutShell/LayoutShell.tsx` (add MobileTabBar)
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx` (adjust position)

---

### Issue 2: Dynamic Content Padding
**Priority**: CRITICAL
**Effort**: 0.5 days
**Dependencies**: Issue 1

**Description**: Replace the static `pb-24` in LayoutShell with dynamic padding that accounts for which bottom elements are visible.

**Acceptance Criteria**:
- [ ] Main content padding adapts to visible bottom bars
- [ ] MobileTabBar + FloatingActionBar visible: `pb-36` + safe area
- [ ] MobileTabBar only: `pb-20` + safe area
- [ ] FloatingActionBar only (desktop): `pb-24`
- [ ] Nothing visible: `pb-6`
- [ ] iOS safe area: `env(safe-area-inset-bottom)` on mobile
- [ ] No layout shift when FloatingActionBar appears/disappears
- [ ] Tests: padding calculation test

**Technical Notes**:
- Use `useResponsive()` hook (already exists) to detect mobile
- Track FloatingActionBar visibility via NavigationContext (actionBarActions.length > 0)
- Consider a `useBottomPadding()` hook for clean abstraction

**Files to modify**:
- MODIFY: `src/components/layout/LayoutShell/LayoutShell.tsx`
- CREATE: `src/hooks/useBottomPadding.ts` (optional, for clean abstraction)

---

### Issue 3: Public Pages — MobileTabBar + Responsive Landing
**Priority**: HIGH
**Effort**: 1.5 days
**Dependencies**: Issue 1

**Description**: Extend the MobileTabBar to public pages (with auth-gated tabs) and improve the landing/public page responsive design.

**Acceptance Criteria**:
- [ ] MobileTabBar visible on public pages (guest version: Home + Discover)
- [ ] Public layout (`(public)/layout.tsx`) integrates MobileTabBar
- [ ] Landing page: responsive hero section (text stacks on mobile)
- [ ] `/games` catalog: responsive card grid (1 col mobile, 2 sm, 3 lg)
- [ ] Login page: mobile-friendly form (full-width inputs, proper spacing)
- [ ] Footer: stacks on mobile, horizontal on desktop
- [ ] Tests: public page responsive tests

**Technical Notes**:
- PublicLayout uses UnifiedHeader — add MobileTabBar alongside
- Login page (screenshot): already looks decent, minor spacing improvements
- Game catalog: verify MeepleCard grid is truly mobile-first

**Files to modify**:
- MODIFY: `src/app/(public)/layout.tsx`
- MODIFY: Landing page components (hero, features, CTA sections)
- VERIFY: `src/app/(auth)/layout.tsx` (login/register responsive)

---

## Sprint 2 — P1: UX Enhancements

### Issue 4: FloatingActionBar — Auto-Hide on Scroll
**Priority**: HIGH
**Effort**: 0.5 days
**Dependencies**: Issue 1

**Description**: Hide the FloatingActionBar when the user scrolls down (reading mode), show it again on scroll up.

**Acceptance Criteria**:
- [ ] Scroll down > 50px: FloatingActionBar slides down + fades out (200ms)
- [ ] Scroll up: FloatingActionBar slides up + fades in (200ms)
- [ ] Tap on MobileTabBar: force-show FloatingActionBar
- [ ] Desktop: no auto-hide (always visible when actions present)
- [ ] `prefers-reduced-motion`: instant show/hide, no animation
- [ ] Tests: scroll behavior test

**Technical Notes**:
- Create `useScrollDirection()` hook or use existing scroll patterns
- Use `transform: translateY(calc(100% + 24px))` for GPU-accelerated hide
- Passive scroll listener for performance

**Files to create/modify**:
- CREATE: `src/hooks/useScrollDirection.ts`
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx`

---

### Issue 5: Mobile Breadcrumb / Back Navigation
**Priority**: HIGH
**Effort**: 1.5 days
**Dependencies**: None

**Description**: Add a lightweight breadcrumb bar on mobile (below MiniNav) that shows the navigation path and allows quick back navigation.

**Acceptance Criteria**:
- [ ] New component: `MobileBreadcrumb`
- [ ] Visible only on mobile: `md:hidden`
- [ ] Shows: `arrow-left icon + "Section > Subsection"`
- [ ] Tap arrow or parent label to navigate back
- [ ] Examples: "Library > Preferiti", "Catalogo > Carcassonne", "Admin > Agents"
- [ ] Collapses to "arrow-left Section" on viewport < 375px
- [ ] Height: 36px, bg-muted/50, text-sm, font-nunito
- [ ] Smooth transition on route change (fade 150ms)
- [ ] `role="navigation"` + `aria-label="Breadcrumb"`
- [ ] Tests: rendering + navigation tests
- [ ] Integrates in LayoutShell between MiniNav and main content

**Technical Notes**:
- Data source: `usePathname()` + route label mapping from config/navigation.ts
- Pattern: `config/breadcrumb-labels.ts` with route-to-label map
- Fallback: title-case segment name if no config match

**Files to create/modify**:
- CREATE: `src/components/layout/MobileBreadcrumb/MobileBreadcrumb.tsx`
- CREATE: `src/components/layout/MobileBreadcrumb/index.ts`
- CREATE: `src/config/breadcrumb-labels.ts`
- MODIFY: `src/components/layout/LayoutShell/LayoutShell.tsx`

---

### Issue 6: SmartFAB — Context-Aware Primary Action (Simplified)
**Priority**: HIGH
**Effort**: 1 day
**Dependencies**: Issue 1

**Description**: Implement a simplified SmartFAB (single floating button) that shows the primary action for the current context on mobile only.

**Acceptance Criteria**:
- [ ] New component: `SmartFAB`
- [ ] Visible only on mobile: `md:hidden`
- [ ] Position: right-4, above MobileTabBar (`bottom-[calc(72px+1rem)]`)
- [ ] Size: 56px diameter
- [ ] Icon changes based on route context:
  - `/library`: Plus (add game)
  - `/library/[id]`: Play (start session)
  - `/games`: Search (advanced search)
  - `/games/[id]`: Plus (add to library)
  - `/chat`: Plus (new chat)
  - `/sessions`: Plus (new session)
  - `/dashboard`: Sparkles (AI chat)
  - Default: MessageSquare (chat)
- [ ] Design: bg-primary, text-primary-foreground, shadow-lg, rounded-full
- [ ] Haptic-ready: `navigator.vibrate?.(10)` on tap
- [ ] Hides during fast scroll (reuses useScrollDirection)
- [ ] `aria-label` changes dynamically with context
- [ ] Tests: context mapping test + visibility test
- [ ] Long-press QuickMenu: **NOT in this issue** (deferred to P3)

**Technical Notes**:
- Context mapping: `config/smart-fab.ts` with route-to-action map
- Use `usePathname()` for context detection
- Coexists with FloatingActionBar (different z-index, different position)

**Files to create/modify**:
- CREATE: `src/components/layout/SmartFAB/SmartFAB.tsx`
- CREATE: `src/components/layout/SmartFAB/index.ts`
- CREATE: `src/config/smart-fab.ts`
- MODIFY: `src/components/layout/LayoutShell/LayoutShell.tsx`

---

## Sprint 3 — P2: Polish & Accessibility

### Issue 7: Touch-Friendly Tooltips
**Priority**: MEDIUM
**Effort**: 0.5 days
**Dependencies**: None

**Description**: Replace hover-only tooltips in FloatingActionBar with touch-friendly alternatives.

**Acceptance Criteria**:
- [ ] On mobile: show text labels below icons (like iOS tab bar) instead of tooltips
- [ ] On desktop: keep hover tooltips as-is
- [ ] Disabled actions: show disabledTooltip on long-press (mobile) or hover (desktop)
- [ ] Tests: tooltip accessibility test

**Files to modify**:
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx`

---

### Issue 8: prefers-reduced-motion Global Support
**Priority**: MEDIUM
**Effort**: 0.5 days
**Dependencies**: None

**Description**: Add global CSS rule to respect `prefers-reduced-motion`, and verify existing hook is used.

**Acceptance Criteria**:
- [ ] globals.css: `@media (prefers-reduced-motion: reduce)` rule kills all animations
- [ ] Verify `usePrefersReducedMotion()` hook is used in animated components
- [ ] FloatingActionBar: instant show/hide when reduced motion
- [ ] MiniNav: no smooth scrolling when reduced motion
- [ ] Page transitions: disabled when reduced motion
- [ ] Tests: reduced motion behavior test

**Files to modify**:
- MODIFY: `src/styles/globals.css`
- VERIFY: Components using animate-* classes

---

### Issue 9: FloatingActionBar Safe Area Fix
**Priority**: MEDIUM
**Effort**: 0.5 days
**Dependencies**: Issue 1

**Description**: Fix FloatingActionBar positioning on iOS devices with notch/home indicator.

**Acceptance Criteria**:
- [ ] FloatingActionBar respects `env(safe-area-inset-bottom)`
- [ ] On mobile with MobileTabBar: positioned above tab bar
- [ ] On desktop: positioned at bottom-6 (unchanged)
- [ ] No content clipping on iPhone 14/15 series
- [ ] Tests: visual regression test with safe area mocking

**Files to modify**:
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx`

---

### Issue 10: Scroll Listener Consolidation
**Priority**: MEDIUM
**Effort**: 1 day
**Dependencies**: Issue 4

**Description**: Consolidate duplicated scroll listeners into a shared hook.

**Acceptance Criteria**:
- [ ] New `useScrollState()` hook: direction, velocity, isScrolling, scrollY
- [ ] TopNavbar uses shared hook for shadow
- [ ] FloatingActionBar uses shared hook for auto-hide
- [ ] SmartFAB uses shared hook for hide on fast scroll
- [ ] Single passive scroll listener per page
- [ ] Performance: requestAnimationFrame throttling
- [ ] Tests: hook behavior test

**Files to create/modify**:
- CREATE: `src/hooks/useScrollState.ts`
- MODIFY: `src/components/layout/TopNavbar/TopNavbar.tsx`
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx`
- MODIFY: `src/components/layout/SmartFAB/SmartFAB.tsx`

---

## Sprint 4 — P3: Future Enhancements (Backlog)

| # | Feature | Effort | Description |
|---|---------|--------|-------------|
| 11 | SmartFAB QuickMenu (long-press) | 2 days | Long-press menu with 2-3 secondary actions per context |
| 12 | SmartFAB morph animation | 1 day | Icon rotation + scale transition on context change |
| 13 | Virtual keyboard detection | 0.5 days | Hide FAB/TabBar when keyboard open (visualViewport API) |
| 14 | Haptic feedback system | 0.5 days | navigator.vibrate on FAB tap, context switch, success/error |
| 15 | Sidebar strategy rework | 3 days | Split fixed nav zone + contextual zone (R6 from nav analysis) |
| 16 | Tablet split-view | 2 days | List + detail side-by-side on tablet landscape |
| 17 | Admin MobileTabBar | 1 day | Admin-specific bottom tabs (Overview, Users, Games, AI, KB) |
| 18 | Multi-select mode polish | 1 day | Batch action bar replaces FloatingActionBar during selection |
| 19 | Remove deprecated BottomNav | 0.5 days | Delete BottomNav.tsx + stories + tests after MobileTabBar stable |

---

## Implementation Order

*(blocco di codice rimosso)*

## Test Strategy

Each issue includes component tests. Additionally:
- **E2E tests** (Playwright): Mobile viewport (375x812) navigation flow
- **Visual regression**: Screenshot comparison of mobile layouts
- **Accessibility audit**: axe-core scan on mobile viewport
- **Performance**: Lighthouse mobile score before/after

## Definition of Done (per issue)

- [ ] Component implemented + tests passing
- [ ] Responsive behavior verified on 375px, 768px, 1280px viewports
- [ ] Dark mode verified
- [ ] Accessibility: aria-labels, focus management, touch targets
- [ ] PR created to `frontend-dev` (parent branch)
- [ ] Code review passed
- [ ] Merged + branch cleaned up

---

## Files Index

### New Files (to create)
*(blocco di codice rimosso)*

### Modified Files
*(blocco di codice rimosso)*

### Deprecated (to remove in P3)
*(blocco di codice rimosso)*


---



<div style="page-break-before: always;"></div>

## plans/2026-03-06-rag-mock-removal-plan.md

# RAG Mock Removal - Implementation Plan

**Epic**: #5309
**Date**: 2026-03-06
**Base Branch**: main-dev
**PR Target**: main-dev

---

## Issues

| Phase | Issue | Title | Effort | Type |
|-------|-------|-------|--------|------|
| 1 | #5310 | KB Tab PDF state fix | 30min | FE only |
| 2 | #5311 | RAG Config backend + frontend | 2-3h | Fullstack |
| 3 | #5312 | Pipeline save endpoint + frontend | 1-2h | Fullstack |
| 4 | #5313 | Session Agent LLM wiring | 3-4h | BE only |
| 5 | #5314 | Admin Strategy CRUD | 2-3h | BE only |

---

## Phase 1: KB Tab PDF State (#5310)

**Branch**: `fix/issue-5310-kb-pdf-state`
**Scope**: 1 file change, ~15 lines

### Steps

1. **Read** `KnowledgeBaseTab.tsx` and `knowledgeBaseClient.ts`
2. **Replace** hardcoded `pdfState = 'ready'` with:
   - Call `api.knowledgeBase.getEmbeddingStatus(gameId)` via `useQuery`
   - Map `KnowledgeBaseStatus.embeddingState` to `PdfState` enum
3. **Remove** TODO comment on line 175
4. **Test**: Update existing tests, verify status changes render correctly
5. **PR** → code review → merge

### Key Files
- `apps/web/src/components/shared-games/KnowledgeBaseTab.tsx` (edit)
- `apps/web/src/lib/api/clients/knowledgeBaseClient.ts` (read-only, already has endpoint)

---

## Phase 2: RAG Config Endpoints (#5311)

**Branch**: `feat/issue-5311-rag-config-api`
**Scope**: ~12 new files (BE) + 1 edit (FE)

### Backend Steps

1. **Domain Entity** — `RagUserConfig.cs`
   - Fields: UserId, GenerationParams (JSON), RetrievalParams (JSON), RerankerConfig (JSON), ActiveStrategy, ModelConfig (JSON)
   - Factory method: `RagUserConfig.Create(userId)`
   - Update method: `UpdateConfig(params)`

2. **EF Configuration** — `RagUserConfigConfiguration.cs`
   - Table: `RagUserConfigs`
   - JSONB columns for nested config objects
   - Unique index on UserId

3. **Migration** — `AddRagUserConfigTable`

4. **Repository** — `IRagConfigRepository` + `RagConfigRepository`
   - `GetByUserIdAsync(userId)`
   - `UpsertAsync(config)`

5. **CQRS Commands/Queries**:
   - `GetRagConfigQuery(UserId)` → `GetRagConfigQueryHandler` → returns DTO or defaults
   - `SaveRagConfigCommand(UserId, ConfigDto)` → `SaveRagConfigCommandHandler` → upsert
   - `SaveRagConfigCommandValidator` (FluentValidation)

6. **DTOs** — `RagConfigDto.cs` matching frontend store shape

7. **Endpoints** in `KnowledgeBaseEndpoints.cs`:
   - `GET /api/v1/rag/config` → GetRagConfigQuery (uses auth userId)
   - `PUT /api/v1/rag/config` → SaveRagConfigCommand (uses auth userId)

8. **DI Registration** in `KnowledgeBaseServiceExtensions.cs`

### Frontend Steps

9. **Edit** `ragConfigStore.ts`:
   - `saveConfig()`: Replace setTimeout with `httpClient.put('/api/v1/rag/config', config)`
   - `loadUserConfig()`: Replace setTimeout with `httpClient.get('/api/v1/rag/config')`
   - Remove TODO comments

10. **Tests**: Backend handler tests + verify frontend store calls

### Key Architecture Decision
- Store config as JSONB (flexible schema, matches frontend store shape)
- One config per user (not per agent — agent-specific config lives in AgentDefinition)
- Return sensible defaults when no saved config exists

---

## Phase 3: Pipeline Save Endpoint (#5312)

**Branch**: `feat/issue-5312-pipeline-save`
**Scope**: ~8 new files (BE) + 1 edit (FE)

### Backend Steps

1. **Check existing** `ICustomRagPipelineRepository` — already registered, verify interface methods

2. **EF Entity** — `CustomRagPipelineEntity.cs` (if not exists)
   - Id, Name, Description, SchemaVersion, DefinitionJson (JSONB), UserId, CreatedAt, UpdatedAt

3. **Migration** (if table doesn't exist)

4. **CQRS**:
   - `SavePipelineCommand(UserId, PipelineDefinitionDto)` → validate schema → persist
   - `GetPipelinesQuery(UserId)` → list user pipelines
   - `GetPipelineByIdQuery(Id)` → single pipeline
   - `DeletePipelineCommand(Id, UserId)` → soft delete

5. **Validation**: Use existing `PipelineSchemaValidator` to validate before save

6. **Endpoints**:
   - `POST /api/v1/rag/pipelines` → SavePipelineCommand
   - `GET /api/v1/rag/pipelines` → GetPipelinesQuery
   - `GET /api/v1/rag/pipelines/{id}` → GetPipelineByIdQuery
   - `DELETE /api/v1/rag/pipelines/{id}` → DeletePipelineCommand

### Frontend Steps

7. **Edit** `pipelineBuilderStore.ts`:
   - `savePipeline()`: Replace setTimeout with real POST call
   - Remove commented-out fetch code
   - Remove TODO

8. **Tests**: Backend handlers + frontend integration

### Key Architecture Decision
- Store full PipelineDefinition as JSONB (flexible, matches DAG model)
- Use existing PipelineSchemaValidator for validation
- Scoped to user (userId from auth)

---

## Phase 4: Session Agent LLM Wiring (#5313)

**Branch**: `feat/issue-5313-session-agent-llm`
**Scope**: 2 handler edits (BE), most complex phase

### Analysis First

1. **Read fully**:
   - `ChatWithSessionAgentCommandHandler.cs` — understand current flow, what context it builds
   - `ChatCommandHandlers.cs` (AskSessionAgent) — understand current flow
   - `HybridLlmService.cs` — understand GenerateCompletionStreamAsync API
   - `IHybridSearchEngine.cs` — understand search API for RAG
   - `IAgentPromptBuilder.cs` — understand prompt building

### Handler 1: ChatWithSessionAgentCommandHandler

2. **Inject dependencies**:
   - `ILlmService` (HybridLlmService)
   - `IAgentPromptBuilder` (for prompt construction)
   - Game repository (for game context)

3. **Replace placeholder** (lines 147-162):
   - Resolve game title from repository
   - Build system prompt from agent definition + game context
   - Call `GenerateCompletionStreamAsync(systemPrompt, userQuestion, userId)`
   - Map `StreamChunk` → `RagStreamingEvent(Token, ...)`
   - Yield streaming events

4. **Error handling**: Wrap LLM call in try/catch, yield Error event on failure

### Handler 2: AskSessionAgentCommandHandler

5. **Inject dependencies**:
   - `IHybridSearchEngine` (for RAG retrieval)
   - `ILlmService` (for answer generation)

6. **Replace stub** (lines 134-139):
   - Search: `IHybridSearchEngine.SearchAsync(question, gameId)`
   - Build prompt: question + retrieved context chunks
   - Generate: `ILlmService.GenerateCompletionAsync(prompt, userId)`
   - Return real answer with confidence score

7. **Tests**: Unit tests with mocked ILlmService + IHybridSearchEngine

### Key Architecture Decision
- Use existing `IAgentPromptBuilder` for prompt construction (not manual string building)
- Streaming handler yields Token events (matches frontend SSE consumer)
- Non-streaming handler (AskSessionAgent) uses synchronous completion
- Both handlers must handle LLM unavailability gracefully

---

## Phase 5: Admin Strategy CRUD (#5314)

**Branch**: `feat/issue-5314-admin-strategy-crud`
**Scope**: ~10 new files (BE)

### Steps

1. **Analyze existing**:
   - `AdminStrategyEndpoints.cs` — current placeholder routes
   - `ITierStrategyAccessRepository` — may already have what we need
   - `IStrategyModelMappingRepository` — related functionality
   - `AgentStrategy` value object — predefined strategies

2. **Domain**: Determine if we need a new aggregate or can use existing repos
   - If TierStrategyAccess already covers CRUD → wire to existing
   - If not → create `AdminStrategyConfig` entity

3. **CQRS**:
   - `ListStrategiesQuery` → handler returns all strategies with configs
   - `GetStrategyByIdQuery(Id)` → single strategy detail
   - `CreateStrategyCommand(Name, Config)` → create custom strategy
   - `UpdateStrategyCommand(Id, Config)` → update strategy
   - `DeleteStrategyCommand(Id)` → soft delete

4. **Validators**: FluentValidation for Create/Update

5. **Wire endpoints**: Replace lambda placeholders with `IMediator.Send()`

6. **Tests**: Handler tests for all 5 operations

### Key Architecture Decision
- Check if this is about TierStrategyAccess (tier→strategy mapping) or custom strategy definitions
- Follow pattern from other admin CQRS (AdminKnowledgeBaseEndpoints)
- Use RequireAdminSession() for auth

---

## Execution Strategy

### Per-phase workflow (following /implementa)

*(blocco di codice rimosso)*

### Parallelization Opportunities

- Phase 1 is independent (quick fix)
- Phases 2 and 3 are independent (different endpoints, different stores)
- Phase 4 depends on nothing (existing services)
- Phase 5 depends on nothing

**Optimal order**: 1 → (2 || 3) → 4 → 5
**Or sequential**: 1 → 2 → 3 → 4 → 5

### Risk Assessment

| Phase | Risk | Mitigation |
|-------|------|------------|
| 1 | Low | Simple 1-file change |
| 2 | Medium | New DB table + migration | Test with existing config shape |
| 3 | Low-Medium | Repository exists, needs wiring | Use PipelineSchemaValidator |
| 4 | High | LLM integration, streaming | HybridLlmService proven; test with Ollama |
| 5 | Medium | Unclear scope | Analyze existing repos first |

### MCP/Tool Selection

| Phase | Primary Tools |
|-------|---------------|
| 1 | Read, Edit (frontend only) |
| 2 | Edit, Write (fullstack), Context7 (EF patterns) |
| 3 | Edit, Write (fullstack) |
| 4 | Read (deep analysis), Edit, Sequential (complex wiring) |
| 5 | Read (analysis), Edit, Write |


---



<div style="page-break-before: always;"></div>

## plans/2026-03-06-toolkit-evolution-epic.md

# GameToolkit Evolution — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Evolve the GameToolkit system across 5 phases: AI-powered generation, template marketplace, real-time widget sync, whiteboard optimization, and session analytics.

**Architecture:** The GameToolkit system has two layers — an admin-managed `GameToolkit` aggregate (dice/card/timer/counter tools + templates) and a user-level `Toolkit` dashboard (6 generic widgets). Both persist via EF Core to PostgreSQL with JSONB configs. Live sessions use SSE streaming. AI integration leverages the existing `ILlmService` + RAG pipeline.

**Tech Stack:** .NET 9 (ASP.NET Minimal APIs, MediatR, EF Core, FluentValidation) | Next.js 16 (React 19, Zustand, Zod, Tailwind 4, shadcn/ui) | PostgreSQL 16 (JSONB) | Qdrant (vectors) | SSE streaming

**Parent Branch:** `main-dev`

---

## Phase Overview

| Phase | Scope | Issues | Branch |
|-------|-------|--------|--------|
| P0 | AI Toolkit Generation | 9 tasks | `feature/toolkit-ai-generation` |
| P1 | Template Marketplace | 10 tasks | `feature/toolkit-templates` |
| P2 | Widget Real-Time Sync | 6 tasks | `feature/toolkit-widget-sync` |
| P3 | Whiteboard Storage Fix | 2 tasks | `feature/toolkit-whiteboard-fix` |
| P4 | Session Analytics | 7 tasks | `feature/toolkit-session-analytics` |

---

# Phase 0 — AI Toolkit Generation

**Goal:** An admin/editor can click "Generate with AI" on any game with KB cards, and the LLM reads the game rules to auto-populate a GameToolkit draft (dice, counters, scoring, turn order, timers).

**Key Integration Points:**
- `ILlmService.GenerateJsonAsync<T>()` — structured JSON output from LLM
- `ITextChunkSearchService` — retrieve game rule chunks for context
- `GameToolkit.SetAgentConfig()` — already exists, stores AI config as string
- `GameToolkit.AddDiceTool/AddCounterTool/SetScoringTemplate/SetTurnTemplate` — existing mutators

---

### Task P0-1: Define the AI generation response DTO

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/DTOs/AiToolkitSuggestionDto.cs`

**Step 1: Create the DTO that the LLM will return as structured JSON**

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P0-2: Create the AI generation command + validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Validators/ToolkitValidators.cs`

**Step 1: Create the command**

*(blocco di codice rimosso)*

**Step 2: Add validator to ToolkitValidators.cs**

*(blocco di codice rimosso)*

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P0-3: Implement the AI generation handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Handlers/GenerateToolkitFromKbHandler.cs`

**Step 1: Write the handler**

This handler:
1. Fetches KB chunks for the game using `ITextChunkSearchService`
2. Builds a system prompt instructing the LLM to analyze game rules
3. Calls `ILlmService.GenerateJsonAsync<AiToolkitSuggestionDto>()`
4. Returns the suggestion (not persisted yet)

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P0-4: Create the "apply suggestion" command

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/ApplyAiToolkitSuggestionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Handlers/ApplyAiToolkitSuggestionHandler.cs`

**Step 1: Create command**

*(blocco di codice rimosso)*

**Step 2: Create handler**

The handler maps AI suggestion DTOs to domain tool configs and calls the existing `GameToolkit` mutators:

*(blocco di codice rimosso)*

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P0-5: Add API endpoints for AI generation

**Files:**
- Modify: `apps/api/src/Api/Routing/GameToolkitRoutes.cs`

**Step 1: Read the existing routes file first, then add two new endpoints**

Add inside `MapGameToolkitEndpoints()`:

*(blocco di codice rimosso)*

Also add a second endpoint variant that creates a NEW toolkit from a game ID (no existing toolkit needed):

*(blocco di codice rimosso)*

**Step 2: Add the request body record** (in ToolkitDtos.cs or inline):

*(blocco di codice rimosso)*

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P0-6: Backend unit tests for AI generation

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Handlers/GenerateToolkitFromKbHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Handlers/ApplyAiToolkitSuggestionHandlerTests.cs`

**Step 1: Test the generation handler**

*(blocco di codice rimosso)*

**Step 2: Test the apply handler**

*(blocco di codice rimosso)*

**Step 3: Run tests**

*(blocco di codice rimosso)*

**Step 4: Commit**

*(blocco di codice rimosso)*

---

### Task P0-7: Frontend — Add Zod schema + API client methods

**Files:**
- Modify: `apps/web/src/lib/api/schemas/toolkit.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/libraryClient.ts` (or create a dedicated `gameToolkitClient.ts`)

**Step 1: Add Zod schemas for AI suggestion**

*(blocco di codice rimosso)*

**Step 2: Add API client methods**

*(blocco di codice rimosso)*

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P0-8: Frontend — AI generation UI in toolkit configurator

**Files:**
- Create: `apps/web/src/components/toolkit/AiToolkitGenerator.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/private/[privateGameId]/toolkit/configure/client.tsx`

**Step 1: Create the AiToolkitGenerator component**

*(blocco di codice rimosso)*

**Step 2: Integrate into toolkit configurator page**

In the configure `client.tsx`, add the `AiToolkitGenerator` component above the tool sections. Wire `onGenerate` to the API client's `generateToolkitFromKb()` and `onApply` to `applyAiSuggestion()`, then refresh the toolkit state.

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P0-9: Frontend tests + PR

**Files:**
- Create: `apps/web/src/components/toolkit/__tests__/AiToolkitGenerator.test.tsx`

**Step 1: Write component tests**

*(blocco di codice rimosso)*

**Step 2: Run all tests**

*(blocco di codice rimosso)*

**Step 3: Commit and create PR**

*(blocco di codice rimosso)*

---

# Phase 1 — Template Marketplace (Admin/Editor with Approval)

**Goal:** Admins can create/manage toolkit templates. Editors can submit templates that require admin approval before becoming available.

**Key Design Decisions:**
- Templates are NOT a new entity — they are published `GameToolkit` records with a new `TemplateStatus` field
- Editors see "Submit for Review" instead of "Publish"
- Admins see a review queue and can approve/reject

---

### Task P1-1: Add TemplateStatus enum + migration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Enums/TemplateStatus.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/GameToolkit.cs`

**Step 1: Create enum**

*(blocco di codice rimosso)*

**Step 2: Add fields to GameToolkit entity**

*(blocco di codice rimosso)*

**Step 3: Create migration**

*(blocco di codice rimosso)*

**Step 4: Commit**

*(blocco di codice rimosso)*

---

### Task P1-2: Template commands + handlers

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/ToolkitCommands.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Handlers/ToolkitCommandHandlers.cs`

**Step 1: Add commands**

*(blocco di codice rimosso)*

**Step 2: Implement handlers for submit/approve/reject/clone**

Each handler follows the same pattern: load entity, call domain method, save, return DTO.
`CloneFromTemplateCommand` creates a new GameToolkit by copying all tools from the template.

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P1-3: Template query + repository methods

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Queries/ToolkitQueries.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Repositories/IGameToolkitRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/GameToolkitRepository.cs`

**Step 1: Add queries**

*(blocco di codice rimosso)*

**Step 2: Add repository methods**

*(blocco di codice rimosso)*

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P1-4: Template API endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/GameToolkitRoutes.cs`

**Step 1: Add endpoints**

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P1-5: Frontend — Template browser page

**Files:**
- Create: `apps/web/src/app/(authenticated)/toolkit/templates/page.tsx`

**Step 1: Create template browse page**

Shows approved templates in a grid. Each template card shows:
- Name, category badge, tool counts
- "Use This Template" button → calls `cloneFromTemplate(templateId, gameId)`
- Filter by `TemplateCategory`

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P1-6: Frontend — Submit for review UI

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/private/[privateGameId]/toolkit/configure/client.tsx`

**Step 1:** Add "Submit for Review" button (visible to editors, replaces "Publish" for non-admins). Shows status badge (Draft, Pending, Approved, Rejected + review notes).

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P1-7: Admin — Template review queue page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/agents/templates/page.tsx`

**Step 1:** Admin page showing pending templates with Approve/Reject actions + notes textarea.

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P1-8: Frontend — Zod schemas + API client methods for templates

**Files:**
- Modify: `apps/web/src/lib/api/schemas/toolkit.schemas.ts`
- Add methods to API client

**Step 1:** Add `TemplateStatus` schema, template list response schema, and 6 API client methods (getApproved, getPending, submit, approve, reject, clone).

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P1-9: Backend tests for template workflow

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Domain/TemplateWorkflowTests.cs`

**Step 1:** Test domain methods: SubmitForReview (from Draft/Rejected), ApproveTemplate, RejectTemplate, invalid state transitions throw ConflictException.

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P1-10: Frontend tests + PR

**Files:**
- Create: `apps/web/src/app/(authenticated)/toolkit/templates/__tests__/page.test.tsx`

**Step 1:** Test template browser: renders grid, filters by category, clone button calls API.

**Step 2: Commit + PR**

*(blocco di codice rimosso)*

---

# Phase 2 — Widget Real-Time Sync

**Goal:** Extend existing SSE infrastructure to sync the most-used toolkit widgets between session participants. Priority: Scores (already done) > Turns > Dice > Resources > Notes.

**Key Pattern:** The existing `useSessionSync` hook connects to `/api/v1/game-sessions/{sessionId}/stream`. Widget state changes are broadcast as SSE events with type `session:widget-state`.

---

### Task P2-1: Backend — Widget state broadcast SSE event type

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpdateWidgetStateCommand.cs`

**Step 1:** Add `WidgetStateUpdated` event type to SSE mapper. When `UpdateWidgetStateCommand` is handled, broadcast the updated widget state to all session participants via the existing SSE channel.

**Step 2:** Add `BroadcastWidgetStateAsync` method to the session SSE service that emits:
*(blocco di codice rimosso)*

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P2-2: Backend — Widget state update endpoint

**Files:**
- Check existing: `apps/api/src/Api/Routing/` for session widget state routes
- Add if missing: `PATCH /api/v1/game-sessions/{sessionId}/widget-state/{widgetType}`

**Step 1:** Create or verify endpoint that accepts `{stateJson: string}`, validates the widget type, persists to `ToolkitSessionState`, and broadcasts SSE.

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P2-3: Frontend — useWidgetSync hook

**Files:**
- Create: `apps/web/src/lib/hooks/useWidgetSync.ts`

**Step 1: Create the hook**

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P2-4: Integrate sync into TurnManager + ScoreTracker widgets

**Files:**
- Modify: `apps/web/src/components/toolkit/TurnManagerWidget.tsx`
- Modify: `apps/web/src/components/toolkit/ScoreTrackerWidget.tsx`

**Step 1:** Add optional `sessionId` prop. When provided, use `useWidgetSync` to:
- Call `broadcastState()` on every `persistState()` / `persist()` call
- Listen for remote updates and apply to local state (merge, not overwrite)

**Step 2:** Ensure idempotency: ignore remote updates that match the last local update (prevent echo).

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P2-5: Integrate sync into ResourceManager + NoteManager widgets

**Files:**
- Modify: `apps/web/src/components/toolkit/ResourceManagerWidget.tsx`
- Modify: `apps/web/src/components/toolkit/NoteManagerWidget.tsx`

**Step 1:** Same pattern as P2-4. ResourceManager uses 500ms debounce for counter changes. NoteManager only syncs public notes (private notes stay local).

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P2-6: Tests + PR

**Files:**
- Create: `apps/web/src/lib/hooks/__tests__/useWidgetSync.test.ts`
- Update existing widget tests for sync props

**Step 1:** Test useWidgetSync: broadcastState calls fetch, SSE events trigger onRemoteUpdate, debouncing works.

**Step 2: Run all tests, commit, PR**

*(blocco di codice rimosso)*

---

# Phase 3 — Whiteboard Storage Fix

**Goal:** Prevent whiteboard PNG base64 from bloating the database. Debounce saves and compress output.

---

### Task P3-1: Frontend — Debounce + compress whiteboard saves

**Files:**
- Modify: `apps/web/src/components/toolkit/WhiteboardWidget.tsx`

**Step 1:** Replace immediate `onStateChange` call on `stopDraw` with a debounced save:

*(blocco di codice rimosso)*

Replace `onStateChange` in `stopDraw` and `clear` with `debouncedSave()`.

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P3-2: Backend — Limit widget state size + cleanup job

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/ToolkitSessionState.cs`

**Step 1:** Add size validation in `UpdateWidgetState`:

*(blocco di codice rimosso)*

**Step 2: Commit + PR**

*(blocco di codice rimosso)*

---

# Phase 4 — Session Analytics Dashboard

**Goal:** Users can view their play statistics: win rates, score trends, game frequency, session duration.

---

### Task P4-1: Backend — Session statistics query

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionStatisticsDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionStatisticsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Handlers/GetSessionStatisticsHandler.cs`

**Step 1: Create DTOs**

*(blocco di codice rimosso)*

**Step 2: Create query + handler**

*(blocco di codice rimosso)*

Handler aggregates data from `LiveGameSession` + `ToolkitSessionState` tables using EF Core LINQ group-by queries.

**Step 3: Commit**

*(blocco di codice rimosso)*

---

### Task P4-2: Backend — Per-game statistics query

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetGameStatisticsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Handlers/GetGameStatisticsHandler.cs`

**Step 1: Create per-game query**

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P4-3: Backend — Statistics endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/SessionStatisticsRoutes.cs`
- Modify: `apps/api/src/Api/Program.cs` (register routes)

**Step 1: Add endpoints**

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P4-4: Frontend — Zod schemas + API client

**Files:**
- Create: `apps/web/src/lib/api/schemas/session-statistics.schemas.ts`
- Create: `apps/web/src/lib/api/clients/sessionStatisticsClient.ts`

**Step 1: Add schemas and client**

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P4-5: Frontend — Statistics overview page

**Files:**
- Create: `apps/web/src/app/(authenticated)/toolkit/stats/page.tsx`

**Step 1:** Create dashboard page with:
- KPI cards: Total Sessions, Games Played, Win Rate, Avg Duration
- Monthly activity bar chart (shadcn/ui Recharts)
- Most played games list
- Recent score trends line chart

Use existing design tokens: glassmorphic cards, font-quicksand headings, amber accents.

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P4-6: Frontend — Per-game stats section

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` (or create a stats tab)

**Step 1:** Add a "Statistics" section to the game detail page showing:
- Play count, win rate, high score
- Score history chart
- Average session duration

**Step 2: Commit**

*(blocco di codice rimosso)*

---

### Task P4-7: Tests + PR

**Files:**
- Create backend tests for statistics handlers
- Create frontend tests for stats page

**Step 1: Backend tests**

*(blocco di codice rimosso)*

**Step 2: Frontend tests**

*(blocco di codice rimosso)*

**Step 3: Run all tests, commit, PR**

*(blocco di codice rimosso)*

---

# Dependency Graph

*(blocco di codice rimosso)*

**Recommended execution order:**
1. P3 (smallest, 2 tasks, immediate value)
2. P0 + P2 in parallel (P0 = 9 tasks, P2 = 6 tasks)
3. P1 after P0 (10 tasks, builds on AI generation)
4. P4 any time (7 tasks, fully independent)

---

# Summary

| Phase | Tasks | New Files | Modified Files | Tests |
|-------|-------|-----------|----------------|-------|
| P0 | 9 | ~6 | ~4 | ~15 test cases |
| P1 | 10 | ~5 | ~5 | ~12 test cases |
| P2 | 6 | ~2 | ~5 | ~10 test cases |
| P3 | 2 | 0 | ~2 | ~3 test cases |
| P4 | 7 | ~8 | ~3 | ~12 test cases |
| **Total** | **34** | **~21** | **~19** | **~52 test cases** |


---



<div style="page-break-before: always;"></div>

## plans/2026-03-07-game-ecosystem-prd.md

# PRD: MeepleAI Game Ecosystem Evolution

**Date**: 2026-03-07
**Status**: Draft
**Author**: Spec Panel (Wiegers, Adzic, Cockburn, Fowler, Nygard, Crispin)
**Stakeholders**: Product, Engineering, AI Team

---

## Executive Summary

This PRD defines 4 interconnected epics that evolve MeepleAI from a game catalog + AI assistant into a **complete board game lifecycle platform** — from publisher rule-testing through live multi-day play sessions with photo state capture.

### Strategic Decisions (Locked)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Publisher pricing | Free basic, premium analytics | Lower barrier to entry |
| Photo retention | 90 days post-session-completion | Balance cost vs. usefulness |
| Session architecture | Merge SessionTracking into LiveGameSession (future) | Reduce duplication |
| Publisher catalog access | Self-service for testing, admin gate for public | Trust but verify |
| Rulebook analysis model | Dedicated fine-tuned model | Higher quality, domain-specific |

---

## Phase 0: Session Photo Attachments

**Epic**: Session Board State Capture
**Priority**: P0 (highest — enables campaign play UX)
**Estimated Issues**: ~15
**Dependencies**: None (builds on existing LiveGameSession + IBlobStorageService)

### Problem Statement

Players of campaign/multi-session games (Gloomhaven, Risk Legacy, Pandemic Legacy) need to save physical board state between play sessions. Currently, LiveGameSession supports JSON state snapshots but no visual evidence. Players resort to taking photos on personal phones with no linkage to the session record.

### Requirements

#### Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| SP-001 | Players can upload photos during a session | Must | Max 5 photos/player/snapshot, JPEG/PNG, max 10MB each |
| SP-002 | Photos are linked to session snapshots | Must | Each photo has optional SnapshotIndex FK |
| SP-003 | Photos are categorized by type | Should | Types: PlayerArea, BoardState, CharacterSheet, ResourceInventory, Custom |
| SP-004 | Host can upload "main board" photos | Must | BoardState type, visible to all players |
| SP-005 | Thumbnails are auto-generated | Should | 300px resize on upload, stored alongside original |
| SP-006 | Photos display in session resume flow | Must | Gallery view with player attribution on Resume |
| SP-007 | Photos are auto-deleted 90 days after session completion | Must | Background job, S3 lifecycle policy |
| SP-008 | Players can add captions to photos | Could | Max 200 chars per photo |
| SP-009 | Photos accessible in session history | Must | Read-only gallery for completed sessions |
| SP-010 | Upload progress feedback | Should | XHR progress bar (same pattern as PDF upload) |

#### Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| SP-NFR-001 | Upload latency P95 | < 3s for 10MB photo on 50Mbps connection |
| SP-NFR-002 | Thumbnail generation | < 500ms server-side |
| SP-NFR-003 | Storage cost per session | < $0.05/month average (5 players x 3 photos x 5MB avg) |
| SP-NFR-004 | Gallery load time P95 | < 1s for 20 thumbnails |
| SP-NFR-005 | Concurrent uploads | Support 5 simultaneous uploads per session |

### Data Model

*(blocco di codice rimosso)*

### API Endpoints

*(blocco di codice rimosso)*

### Frontend Integration

*(blocco di codice rimosso)*

### Background Jobs

*(blocco di codice rimosso)*

### Scenarios (Gherkin)

*(blocco di codice rimosso)*

---

## Phase 1: Publisher Role & Test Campaigns

**Epic**: Board Game Publisher Portal
**Priority**: P1
**Estimated Issues**: ~25 (3 sub-phases)
**Dependencies**: Phase 0 (photos enable test session evidence)

### Problem Statement

Board game publishers and designers lack tools to validate their rulebooks before printing. Current process: print prototype, find playtesters, observe confusion, manually note issues, revise rulebook, repeat. This is slow (weeks per iteration), expensive, and subjective.

MeepleAI can offer **AI-powered rulebook analysis + structured playtesting** — a unique value proposition with no direct competitor.

### Actor Definition

*(blocco di codice rimosso)*

### Requirements

#### Phase 1A: Publisher Onboarding

| ID | Requirement | Priority |
|----|-------------|----------|
| PB-001 | User can request Publisher role upgrade | Must |
| PB-002 | Request includes: company name, website, description, contact email | Must |
| PB-003 | Admin reviews and approves/rejects publisher request | Must |
| PB-004 | Approved publisher sees "Publisher Dashboard" in nav | Must |
| PB-005 | Publisher can update company profile | Should |
| PB-006 | Publisher profile visible on published games | Could |

**Data Model — PublisherProfile:**
*(blocco di codice rimosso)*

**API Endpoints:**
*(blocco di codice rimosso)*

#### Phase 1B: Rulebook Analysis Pipeline

| ID | Requirement | Priority |
|----|-------------|----------|
| PB-010 | Publisher creates draft game in SharedGameCatalog | Must |
| PB-011 | Publisher uploads rulebook PDF (uses existing DocumentProcessing) | Must |
| PB-012 | System auto-generates RulebookAnalysis with quality metrics | Must |
| PB-013 | Analysis includes: clarity, completeness, consistency, predicted FAQs | Must |
| PB-014 | Publisher views analysis dashboard | Must |
| PB-015 | System auto-generates KB cards from rulebook | Must |
| PB-016 | System auto-creates Tutor agent for the game | Should |
| PB-017 | Publisher can upload revised rulebook versions | Must |
| PB-018 | System provides version comparison (v1 vs v2) | Should |

**Extended RulebookAnalysis (augment existing entity):**
*(blocco di codice rimosso)*

**Analysis Pipeline Flow:**
*(blocco di codice rimosso)*

**API Endpoints:**
*(blocco di codice rimosso)*

#### Phase 1C: Test Campaigns

| ID | Requirement | Priority |
|----|-------------|----------|
| PB-020 | Publisher creates test campaign for a game | Must |
| PB-021 | Campaign generates unique invite code | Must |
| PB-022 | Testers join via invite code (creates LiveGameSession) | Must |
| PB-023 | During play, testers can flag "rule confusion" moments | Must |
| PB-024 | Flags capture: rule section, game context (turn, phase), description | Must |
| PB-025 | Testers can rate rule clarity per section (1-5) | Should |
| PB-026 | Post-session: feedback report auto-generated | Must |
| PB-027 | Publisher views aggregate feedback across campaigns | Must |
| PB-028 | Publisher can export feedback report as PDF | Could |

**Data Model — TestCampaign:**
*(blocco di codice rimosso)*

**API Endpoints:**
*(blocco di codice rimosso)*

### Frontend — Publisher Dashboard

*(blocco di codice rimosso)*

---

## Phase 2: KB Pipeline Self-Service

**Epic**: Publisher Knowledge Base Self-Service
**Priority**: P1 (parallel with Phase 1B)
**Estimated Issues**: ~12
**Dependencies**: Phase 1B (RulebookAnalysis)

### Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| KB-001 | Auto-generate KB cards when publisher uploads rulebook | Must |
| KB-002 | Auto-create Tutor agent linked to game's KB cards | Must |
| KB-003 | Publisher can review and edit auto-generated FAQs | Should |
| KB-004 | Publisher can approve/reject individual FAQ suggestions | Should |
| KB-005 | Auto-FAQ generation uses fine-tuned model + RAG context | Must |
| KB-006 | Version comparison shows KB card changes between versions | Could |

### Integration Architecture

*(blocco di codice rimosso)*

---

## Phase 3: Real-Time Session Sync (Future)

**Epic**: LiveGameSession Real-Time Multiplayer
**Priority**: P2
**Estimated Issues**: ~10
**Dependencies**: SessionTracking Phase 3 (SSE infrastructure)

### Architecture Decision: SSE First, SignalR Later

**Phase 3A: SSE (MVP)**
- Extend existing SSE infrastructure (used for chat streaming)
- Server pushes: score updates, turn advances, player join/leave, photo uploads
- Client polls for state on reconnect (HTTP fallback)
- Sufficient for turn-based games (not real-time action games)

**Phase 3B: SignalR (v2)**
- Bidirectional communication for whiteboard sync, instant updates
- SignalR hub: `/hubs/live-session`
- Groups by SessionCode
- Fallback to SSE for read-only spectators

### Session Architecture Merge Plan

*(blocco di codice rimosso)*

---

## Fine-Tuned Rulebook Analysis Model

### Model Specification

| Aspect | Detail |
|--------|--------|
| **Base model** | TBD (candidates: Llama 3.1 8B, Mistral 7B, Phi-3) |
| **Training data** | Board game rulebooks (public domain) + annotated clarity labels |
| **Output format** | Structured JSON with scores and suggestions |
| **Deployment** | Dedicated service container alongside embedding-service |
| **Inference** | < 30s for a typical 20-page rulebook |
| **Fine-tuning approach** | LoRA on instruction-tuned base |

### Training Data Requirements

*(blocco di codice rimosso)*

### Analysis Output Schema

*(blocco di codice rimosso)*

---

## Cross-Phase Dependencies

*(blocco di codice rimosso)*

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Session photo uploads/week | 100+ (month 3) | Analytics event tracking |
| Publisher registrations | 20+ (month 6) | Admin dashboard |
| Rulebook analyses completed | 50+ (month 6) | RulebookAnalysis count |
| Test campaigns created | 10+ (month 6) | TestCampaign count |
| Avg. rulebook score improvement (v1→v2) | +1.5 points | Version comparison |
| Session resume rate (paused→resumed) | > 60% | Session lifecycle tracking |
| Photo storage cost | < $50/month | S3 billing |

---

## Risk Register

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Fine-tuned model quality insufficient | Medium | High | Start with GPT-4/Claude analysis, fine-tune incrementally |
| Publishers upload copyrighted content | High | High | ToS, admin review gate, DMCA process |
| Photo storage costs spiral | Medium | Medium | 90-day retention, compression, thumbnail-only after 30 days |
| Low publisher adoption | Medium | Medium | Free tier, partner with indie publishers first |
| Session merge breaks existing features | Low | High | Feature flags, 6-month sunset, A/B testing |
| Real-time sync complexity | Medium | Medium | SSE first (simple), SignalR only if SSE insufficient |


---



<div style="page-break-before: always;"></div>

## plans/2026-03-07-phase0-implementation-plan.md

# Implementation Plan: Phase 0 — Session Photo Attachments

**Epic**: #5358
**Issues**: #5359–#5374 (16 issues)
**Branch**: `main-dev`
**Date**: 2026-03-07
**Method**: `/implementa` per issue with dependency-ordered execution

---

## Dependency Graph

*(blocco di codice rimosso)*

---

## Execution Order

### Sprint 1: Backend Foundation (Issues 1-3, sequential)

#### 1. `/implementa #5359 --base-branch main-dev`
**[SP-01] SessionAttachment domain entity + value objects + repository**

- **Scope**: Domain layer only, no persistence
- **Key files to create**:
  - `BoundedContexts/GameManagement/Domain/Entities/SessionAttachment.cs`
  - `BoundedContexts/GameManagement/Domain/ValueObjects/AttachmentType.cs`
  - `BoundedContexts/GameManagement/Domain/Repositories/ISessionAttachmentRepository.cs`
- **Reference pattern**: `SessionMedia.cs` (SessionTracking BC) — same FileId/ThumbnailFileId string pattern
- **Estimated complexity**: Low
- **Branch**: `feature/issue-5359-session-attachment-entity`

#### 2. `/implementa #5360 --base-branch main-dev`
**[SP-02] EF Core configuration + migration**

- **Scope**: Infrastructure persistence layer
- **Key files to create**:
  - `Infrastructure/Configurations/GameManagement/SessionAttachmentEntityConfiguration.cs`
  - `Infrastructure/Entities/GameManagement/SessionAttachmentEntity.cs`
  - `Infrastructure/Repositories/GameManagement/SessionAttachmentRepository.cs`
  - Migration: `AddSessionAttachments`
- **Reference pattern**: `SessionMediaEntityConfiguration.cs` — snake_case, soft delete filter, cascade
- **DB schema**: `docs/plans/2026-03-07-publisher-db-schema.md` (lines 14-63)
- **Branch**: `feature/issue-5360-session-attachment-ef`

#### 3. `/implementa #5361 --base-branch main-dev`
**[SP-03] ISessionAttachmentService — upload, thumbnail, S3**

- **Scope**: Application service wrapping IBlobStorageService
- **Key files to create**:
  - `BoundedContexts/GameManagement/Application/Services/ISessionAttachmentService.cs`
  - `BoundedContexts/GameManagement/Application/Services/SessionAttachmentService.cs`
- **Reference pattern**: `IBlobStorageService` — StoreAsync returns BlobStorageResult with FileId
- **Key decisions**:
  - Storage path: `session_photos/{sessionId}/{fileId}_{sanitizedFileName}` (not `pdf_uploads/`)
  - Thumbnail: Resize to 300x300 max (use System.Drawing or ImageSharp)
  - Content type validation: image/jpeg, image/png only
  - File size limit: 1KB–10MB
- **Branch**: `feature/issue-5361-attachment-service`

---

### Sprint 2: CQRS Commands/Queries (Issues 4-6, parallelizable)

#### 4. `/implementa #5362 --base-branch main-dev`
**[SP-04] UploadSessionAttachment command + handler + validator**

- **Scope**: CQRS command with MediatR
- **Key files**:
  - `Application/Commands/UploadSessionAttachmentCommand.cs`
  - `Application/Commands/UploadSessionAttachmentCommandHandler.cs`
  - `Application/Commands/UploadSessionAttachmentCommandValidator.cs`
- **Validation**: Session exists + active/paused, player is member, content type, file size
- **Branch**: `feature/issue-5362-upload-attachment-cmd`

#### 5. `/implementa #5363 --base-branch main-dev`
**[SP-05] List/Get session attachment queries**

- **Scope**: CQRS queries
- **Key files**:
  - `Application/Queries/GetSessionAttachmentsQuery.cs` (list by session)
  - `Application/Queries/GetSessionAttachmentByIdQuery.cs` (single with presigned URL)
- **Returns**: DTOs with thumbnail URLs, presigned download URLs
- **Branch**: `feature/issue-5363-attachment-queries`

#### 6. `/implementa #5364 --base-branch main-dev`
**[SP-06] Delete session attachment command**

- **Scope**: Soft delete + blob cleanup
- **Key files**:
  - `Application/Commands/DeleteSessionAttachmentCommand.cs`
  - `Application/Commands/DeleteSessionAttachmentCommandHandler.cs`
- **Authorization**: Only uploader or session host can delete
- **Branch**: `feature/issue-5364-delete-attachment`

---

### Sprint 3: API + Background (Issues 7-9, parallelizable)

#### 7. `/implementa #5365 --base-branch main-dev`
**[SP-07] Session attachment API endpoints**

- **Scope**: Minimal API endpoints in LiveSession routing
- **Endpoints**:
  - `POST /api/v1/sessions/{id}/attachments` (multipart upload)
  - `GET /api/v1/sessions/{id}/attachments` (list)
  - `GET /api/v1/sessions/{id}/attachments/{attachmentId}` (detail + presigned URL)
  - `DELETE /api/v1/sessions/{id}/attachments/{attachmentId}`
- **Reference**: `LiveSessionEndpoints.cs` — add attachment sub-routes
- **Branch**: `feature/issue-5365-attachment-endpoints`

#### 8. `/implementa #5366 --base-branch main-dev`
**[SP-08] SessionAttachmentCleanupJob — 90-day retention**

- **Scope**: Background hosted service
- **Key files**:
  - `BoundedContexts/GameManagement/Infrastructure/BackgroundServices/SessionAttachmentCleanupJob.cs`
- **Logic**: Find attachments where `created_at < NOW() - 90 days AND is_deleted = false` on completed sessions → soft delete + blob delete
- **Schedule**: Daily at 3 AM UTC
- **Branch**: `feature/issue-5366-attachment-cleanup`

#### 9. `/implementa #5367 --base-branch main-dev`
**[SP-09] Extend SessionSnapshot metadata for attachment IDs**

- **Scope**: Add attachment references to snapshot delta metadata
- **Changes**:
  - SessionSnapshot: Add `AttachmentIds` (List<Guid>) to snapshot metadata
  - When snapshot is created, capture current attachment IDs
  - Resume flow: Load attachments from snapshot metadata
- **Branch**: `feature/issue-5367-snapshot-attachments`

---

### Sprint 4: Frontend Components (Issues 10-12, parallelizable)

#### 10. `/implementa #5368 --base-branch main-dev`
**[SP-10] PhotoUploadModal — camera/gallery picker with progress**

- **Scope**: React component with XHR upload progress
- **Key files**:
  - `components/session/PhotoUploadModal.tsx`
- **Features**: Camera capture (mobile), gallery pick, drag-drop, upload progress bar, caption input, attachment type selector
- **Pattern**: XHR with relative URL (avoid CORS, route through Next.js proxy)
- **Branch**: `feature/issue-5368-photo-upload-modal`

#### 11. `/implementa #5369 --base-branch main-dev`
**[SP-11] SessionPhotoGallery — grid thumbnails with lightbox**

- **Scope**: Photo gallery component
- **Key files**:
  - `components/session/SessionPhotoGallery.tsx`
- **Features**: Thumbnail grid (3-col mobile, 4-col desktop), lightbox viewer, caption display, delete button (uploader only), filter by attachment type
- **Branch**: `feature/issue-5369-photo-gallery`

#### 12. `/implementa #5370 --base-branch main-dev`
**[SP-12] ResumePhotoReview — board restoration confirmation**

- **Scope**: Session resume helper showing last photos
- **Key files**:
  - `components/session/ResumePhotoReview.tsx`
- **Features**: Show photos from last snapshot, "Board restored?" confirmation, side-by-side current vs saved state
- **Branch**: `feature/issue-5370-resume-photo-review`

---

### Sprint 5: Integration (Issues 13-14, parallelizable)

#### 13. `/implementa #5371 --base-branch main-dev`
**[SP-13] ToolRail integration — Camera tool button**

- **Scope**: Add camera tool to existing ToolRail
- **Changes**: Add Camera icon button to ToolRail, opens PhotoUploadModal
- **Reference**: Existing ToolRail component with tool switching via Zustand sessionStore
- **Branch**: `feature/issue-5371-toolrail-camera`

#### 14. `/implementa #5372 --base-branch main-dev`
**[SP-14] Session pause flow — photo upload prompt**

- **Scope**: Prompt user to take photos before pausing session
- **Changes**: Intercept pause action, show "Take a photo of the board?" dialog, optional photo upload, then proceed with pause
- **Branch**: `feature/issue-5372-pause-photo-prompt`

---

### Sprint 6: Test Suites (Issues 15-16, parallelizable)

#### 15. `/implementa #5373 --base-branch main-dev`
**[SP-15] Backend tests — entity, service, endpoints**

- **Scope**: Comprehensive backend test coverage
- **Test categories**:
  - Unit: SessionAttachment entity (creation, validation, soft delete, state)
  - Unit: SessionAttachmentService (upload, thumbnail, delete)
  - Unit: Command handlers + validators
  - Integration: Repository with Testcontainers
  - Integration: API endpoints (upload, list, delete)
  - Background: Cleanup job (retention logic)
- **Target**: 90%+ coverage on new code
- **Branch**: `feature/issue-5373-backend-tests`

#### 16. `/implementa #5374 --base-branch main-dev`
**[SP-16] Frontend tests — components + E2E upload flow**

- **Scope**: Frontend test coverage
- **Test categories**:
  - Vitest: PhotoUploadModal (validation, progress, error handling)
  - Vitest: SessionPhotoGallery (rendering, filtering, delete)
  - Vitest: ResumePhotoReview (photo display, confirmation)
  - Vitest: ToolRail camera integration
  - Playwright E2E: Full upload flow (open modal → select file → upload → gallery)
- **Target**: 85%+ coverage on new components
- **Branch**: `feature/issue-5374-frontend-tests`

---

## Execution Commands

*(blocco di codice rimosso)*

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Thumbnail generation library choice | Use SkiaSharp (cross-platform) — already in .NET ecosystem |
| S3 storage path conflict with PDF uploads | Use `session_photos/` prefix instead of `pdf_uploads/` |
| Large photo uploads timeout | XHR with chunked upload, 10MB limit, progress tracking |
| Mobile camera API differences | Use HTML5 `<input type="file" accept="image/*" capture="environment">` |
| Cleanup job deleting active session photos | Only clean completed sessions older than 90 days |
| Snapshot metadata size growth | Store only attachment IDs (UUID[]), not full metadata |

## Estimated Effort

| Sprint | Issues | Estimated LOC | Focus |
|--------|--------|---------------|-------|
| 1 | 3 | ~800 | Domain + persistence |
| 2 | 3 | ~600 | CQRS commands/queries |
| 3 | 3 | ~700 | API + background |
| 4 | 3 | ~1200 | React components |
| 5 | 2 | ~400 | Integration |
| 6 | 2 | ~1500 | Tests |
| **Total** | **16** | **~5200** | |


---



<div style="page-break-before: always;"></div>

## plans/2026-03-07-publisher-db-schema.md

# Database Schema: Publisher Portal & Session Photos

**Date**: 2026-03-07
**Status**: Draft
**Related PRD**: `docs/plans/2026-03-07-game-ecosystem-prd.md`

---

## Phase 0: Session Photo Attachments

### Table: `session_attachments`

*(blocco di codice rimosso)*

### EF Core Configuration

*(blocco di codice rimosso)*

---

## Phase 1A: Publisher Profile

### Table: `publisher_profiles`

*(blocco di codice rimosso)*

### EF Core Configuration

*(blocco di codice rimosso)*

---

## Phase 1B: Extended RulebookAnalysis

### ALTER existing `rulebook_analyses` table

*(blocco di codice rimosso)*

---

## Phase 1C: Test Campaigns

### Table: `test_campaigns`

*(blocco di codice rimosso)*

### Table: `test_session_feedback`

*(blocco di codice rimosso)*

---

## Entity Relationship Diagram

*(blocco di codice rimosso)*

---

## Migration Plan

### Migration 1: `AddSessionAttachments` (Phase 0)
- Create `session_attachments` table
- Create all indexes
- No data migration needed

### Migration 2: `AddPublisherProfiles` (Phase 1A)
- Create `publisher_profiles` table
- Add `PublisherPolicy` to authorization config
- No data migration needed

### Migration 3: `ExtendRulebookAnalysis` (Phase 1B)
- ALTER `rulebook_analyses` — add new columns
- All new columns nullable or have defaults → no data issues
- Backfill `version_number = 1` for existing records

### Migration 4: `AddTestCampaigns` (Phase 1C)
- Create `test_campaigns` table
- Create `test_session_feedback` table
- No data migration needed

### Migration Order
*(blocco di codice rimosso)*

---

## Storage Estimates

### Session Photos (Phase 0)
*(blocco di codice rimosso)*

### Publisher Data (Phase 1)
*(blocco di codice rimosso)*


---



<div style="page-break-before: always;"></div>

## plans/2026-03-07-stack-optimization-design.md

# Stack Optimization Design: Enterprise to MVP

**Date**: 2026-03-07
**Status**: Approved
**Goal**: Reduce from 18+ containers / ~42 GB RAM to 6 containers / ~8.5 GB RAM
**Target hosting**: Hetzner CAX31 (8 ARM cores, 16 GB RAM, EUR 9.30/month)
**Breakeven**: 3 paying users at EUR 4.99-12.99/month

## Architecture

### Target Stack (6 containers)

| Service | Role | RAM |
|---------|------|-----|
| PostgreSQL 16 + pgvector | Relational DB + vector search + FTS | 1.5 GB |
| Redis 7.4 | Cache L1+L2, sessions, rate limiting, token bucket | 0.5 GB |
| .NET 9 API | RAG pipeline, CQRS, PdfPig in-process | 1.5 GB |
| Next.js (standalone) | Frontend SSR+CSR | 0.4 GB |
| Ollama | LLM (Qwen2.5-1.5B) + Embedding (mxbai-embed-large 1024d) | 2.2 GB |
| Caddy | Reverse proxy, auto-TLS | 0.1 GB |
| **Total** | | **~8.5 GB** (7.5 GB headroom on 16 GB) |

### Eliminated Services (12 containers)

| Service | RAM Freed | Replacement |
|---------|-----------|-------------|
| embedding-service (Python, e5-large) | 4 GB | Ollama mxbai-embed-large (1024d, same dimensions) |
| reranker-service (Python, bge-reranker-v2-m3) | 2 GB | Graceful degradation to RRF scores (already in code) |
| unstructured-service (Python, PDF extraction) | 2 GB | PdfPig .NET in-process + offline pre-processing |
| smoldocling-service (Python, VLM PDF) | 4 GB | Claude Vision API on-demand (EUR 0.01/page) |
| orchestration-service (Python, LangGraph) | 4 GB | Removed (not wired to main flow) |
| Qdrant | 4 GB | pgvector HNSW index in PostgreSQL |
| Prometheus + Grafana + AlertManager + cAdvisor + node-exporter | 4.3 GB | Serilog file JSON + UptimeRobot free |
| n8n | 1 GB | Removed (no critical workflows) |
| MinIO | 512 MB | Filesystem local, Cloudflare R2 in future |
| Mailpit | 128 MB | Removed (dev only) |
| Traefik | 256 MB | Caddy (simpler config, auto-TLS) |

## Key Design Decisions

### 1. Embedding: Ollama mxbai-embed-large

- **Dimensions**: 1024 (identical to current e5-large, no DB schema change)
- **Quality**: MTEB 64.7 vs e5-large 61.5 (mxbai is better)
- **RAM**: +0.2 GB on top of existing Ollama (vs +1.7 GB for Python service)
- **Trade-off**: Sequential embedding (no batch), mitigated by async PDF processing
- **Fallback**: If quality insufficient for Italian, add Python e5-large container back

### 2. Vector Search: pgvector replaces Qdrant

- pgvector already enabled in DbContext (UseVector())
- VectorDocumentEntity already has vector(1024) column
- New: PgVectorStoreAdapter implementing same interface as QdrantVectorStoreAdapter
- New: HNSW index (m=16, ef_construction=200) via EF migration
- Performance equivalent for <50K vectors; reintroduce Qdrant if needed at scale

### 3. Reranker: Disabled with Graceful Degradation

- ResilientRetrievalService already handles reranker absence
- Circuit breaker: 3 failures triggers automatic fallback to RRF scores
- Quality impact: -7-11% on ambiguous queries, zero on direct factual Q&A
- Config change only: `EnableReranking: false` in appsettings.json

### 4. PDF Processing: Hybrid Offline + Runtime

- **Offline** (pre-launch): Unstructured CLI on dev machine for top 50-100 games
- **Runtime** (production): PdfPig .NET (UglyToad.PdfPig, MIT) for user uploads
- **Edge cases**: Claude Vision API on-demand for complex scanned PDFs
- No Python service needed in production

### 5. LLM Routing: Unchanged

The HybridLlmService routing logic stays identical:
- Cache L1 (memory) + L2 (Redis): instant response, EUR 0
- FAST/BALANCED: Ollama Qwen2.5-1.5B local, EUR 0
- PRECISE+: DeepSeek/Claude via OpenRouter, EUR 0.008-0.13/query
- Budget alerts and auto-downgrade already implemented

### 6. Monitoring: Simplified

- Serilog structured JSON logging (file rotation)
- /admin/health endpoint (already exists)
- AdminBudgetService for LLM cost tracking (already exists)
- UptimeRobot free tier for uptime monitoring
- Reintroduce Prometheus/Grafana when >50 paying users

## Data Migration

### Re-embedding (e5-large to mxbai-embed-large)

Both models produce 1024-dimensional vectors but in different semantic spaces.
All documents must be re-embedded before launch.

- Estimated volume: 50-100 games x ~50 chunks = ~5K vectors
- Estimated time: ~25 minutes via Ollama sequential embedding
- One-time operation, offline, before launch

### Qdrant to pgvector

- Implement PgVectorStoreAdapter (same interface)
- EF Migration: CREATE INDEX USING hnsw
- Re-index all vectors (combined with re-embedding step)
- Remove Qdrant.Client NuGet package

## Quality Impact Matrix

| Functionality | Enterprise Stack | MVP Stack | Quality Delta |
|--------------|-----------------|-----------|:---:|
| Simple Q&A ("how many VP to win?") | Claude Sonnet 4.5 | Cache/Ollama local | 0% |
| Medium Q&A ("port + knight interaction") | DeepSeek + reranker | Ollama + RRF | -5-8% |
| Complex Q&A ("compare strategies") | Claude multi-agent | DeepSeek cloud | -10-15% |
| Vector search (<50K vectors) | Qdrant HNSW | pgvector HNSW | 0% |
| PDF extraction (top games) | Unstructured real-time | Pre-processed offline | 0% |
| PDF upload (user) | Unstructured + SmolDocling | PdfPig + Claude Vision | -10-20% on complex PDFs |
| Embedding quality | e5-large 1024d | mxbai-embed-large 1024d | +3% (mxbai better) |
| Response latency (cache miss) | 1-3s cloud | 7-12s local + streaming | Noticeable |

## Cost Model

### Monthly Costs

| Item | Cost |
|------|------|
| Hetzner CAX31 | EUR 9.30 |
| Domain + DNS | EUR 1.50 |
| OpenRouter (pay-per-use) | EUR 5-15 |
| Cloudflare free tier | EUR 0 |
| UptimeRobot free tier | EUR 0 |
| **Total** | **EUR 16-26** |

### Breakeven

- Starter (EUR 4.99): 4 users
- Pro (EUR 12.99): 2 users
- Realistic mix: **3 paying users**

## Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|------|:-:|:-:|------------|
| mxbai quality insufficient for Italian | Low | Medium | Benchmark 50 IT queries pre-launch. Fallback: add Python e5-large |
| PdfPig fails on complex PDFs | Medium | Low | Queue for manual review + Claude Vision API |
| pgvector slow at >50K vectors | Low | Low | Months away. Reintroduce Qdrant when needed |
| Ollama model swapping delay | Medium | Low | OLLAMA_KEEP_ALIVE=24h keeps both in RAM |
| Qwen2.5-1.5B quality too low | Medium | High | Upgrade to 3B (+0.5 GB) or route all to cloud |
| ARM64 compatibility issues | Low | High | .NET 9 + Ollama support ARM64 natively. Test before deploy |

## Monetization: Token Bucket Model

| Plan | Price | Queries/month | Cost per query |
|------|-------|:---:|:---:|
| Free | EUR 0 | 5/day (FAST only) | EUR 0 (local) |
| Starter | EUR 4.99 | 100 | EUR 0.001-0.008 |
| Pro | EUR 12.99 | 500 | EUR 0.001-0.008 |
| Top-up | EUR 2.99 | +50 | Same |

Margin: 83-98% depending on cache hit rate and query tier distribution.

## Implementation Phases

1. **Eliminate non-essential services** (1-2 days) - Remove from docker-compose, verify graceful degradation
2. **Switch embedding to Ollama mxbai** (2-3 days) - Config change, re-embed documents
3. **Disable reranker** (1 day) - Config change, test RAG quality
4. **Replace Qdrant with pgvector** (3-5 days) - New adapter, migration, re-index
5. **PDF processing transition** (2-3 days) - PdfPig integration, offline pre-processing
6. **Deploy on Hetzner CAX31** (1-2 days) - docker-compose.mvp.yml, Caddy, DNS

**Total estimated effort**: ~2 weeks


---



<div style="page-break-before: always;"></div>

## plans/2026-03-07-stack-optimization-implementation.md

# Stack Optimization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Reduce MeepleAI from 18+ containers (~42 GB RAM) to 6 containers (~8.5 GB RAM) for EUR 9.30/month hosting.

**Architecture:** Replace 5 Python AI services with Ollama (mxbai-embed-large + Qwen2.5-1.5B), replace Qdrant with pgvector HNSW, add PdfPig for .NET-native PDF extraction, replace Traefik with Caddy, remove monitoring stack.

**Tech Stack:** .NET 9, PostgreSQL 16 + pgvector, Redis 7.4, Ollama, Caddy, Next.js standalone, UglyToad.PdfPig

**Design Doc:** `docs/plans/2026-03-07-stack-optimization-design.md`

---

## Phase 1: Eliminate Non-Essential Services (docker-compose)

### Task 1.1: Create MVP docker-compose

**Files:**
- Create: `infra/docker-compose.mvp.yml`
- Reference: `infra/docker-compose.yml` (full enterprise version)

**Step 1: Read the current docker-compose to understand service definitions**

Read `infra/docker-compose.yml` — note service names, volumes, networks, health checks for the 6 services to KEEP: postgres, redis, ollama, api, web, and the network definition.

**Step 2: Create docker-compose.mvp.yml with only 6 services**

Create `infra/docker-compose.mvp.yml` containing:
- `postgres` (from lines 7-40 of original, keep pgvector image, reduce RAM limit to 1.5GB)
- `redis` (from lines 70-94, reduce maxmemory to 512mb, RAM limit 512MB)
- `ollama` (from lines 181-218, add mxbai-embed-large pull, set OLLAMA_KEEP_ALIVE=24h, RAM limit 2.5GB)
- `api` (from lines 696-760, remove depends_on for qdrant/embedding/reranker/unstructured/smoldocling, RAM limit 1.5GB)
- `web` (from lines 762-800, RAM limit 512MB)
- `caddy` (NEW service replacing traefik)

Key changes in the api service environment:
*(blocco di codice rimosso)*

**Step 3: Create Caddyfile**

Create `infra/Caddyfile`:
*(blocco di codice rimosso)*

**Step 4: Create Ollama model initialization script**

Create `infra/scripts/ollama-init.sh`:
*(blocco di codice rimosso)*

**Step 5: Verify docker-compose.mvp.yml is valid**

Run: `cd infra && docker compose -f docker-compose.mvp.yml config --quiet`
Expected: No errors (validates YAML syntax and service references)

**Step 6: Commit**

*(blocco di codice rimosso)*

---

## Phase 2: Switch Embedding to Ollama mxbai-embed-large

### Task 2.1: Update appsettings for Ollama embedding

**Files:**
- Modify: `apps/api/src/Api/appsettings.json` (lines 46-60, Embedding section)
- Modify: `apps/api/src/Api/appsettings.Development.json` (add Embedding override)

**Step 1: Read current appsettings Embedding config**

Read `apps/api/src/Api/appsettings.json` — find the `"Embedding"` section (around line 46).

**Step 2: Update Embedding section for OllamaMxbai**

Change the Embedding section in `appsettings.json`:
*(blocco di codice rimosso)*

Key changes:
- `Provider`: `"External"` -> `"OllamaMxbai"`
- `FallbackProvider`: `"OpenRouterSmall"` -> `"OllamaNomic"`
- `BatchSize`: `10` -> `1` (Ollama is sequential)
- `TimeoutSeconds`: `60` -> `120` (Ollama cold start may be slow)
- `Model`: `null` -> `"mxbai-embed-large"`
- `Dimensions`: `null` -> `1024`

**Step 3: Verify the EmbeddingConfiguration class supports OllamaMxbai**

Read `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/EmbeddingConfiguration.cs` — confirm `EmbeddingProviderType.OllamaMxbai` exists and maps to `mxbai-embed-large` with 1024 dimensions.

**Step 4: Verify OllamaEmbeddingProvider handles OllamaMxbai type**

Read `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/Providers/OllamaEmbeddingProvider.cs` — confirm constructor accepts `EmbeddingProviderType.OllamaMxbai` (line 26-28 already validates this).

**Step 5: Commit**

*(blocco di codice rimosso)*

### Task 2.2: Disable reranker via config

**Files:**
- Modify: `apps/api/src/Api/appsettings.json` (lines 332-346, ResilientRetrieval section)

**Step 1: Read current reranking config**

Read `apps/api/src/Api/appsettings.json` — find `"ResilientRetrieval"` section.

**Step 2: Set EnableReranking to false**

Change in `appsettings.json`:
*(blocco di codice rimosso)*

Only change: `"EnableReranking": true` -> `"EnableReranking": false`

**Step 3: Verify ResilientRetrievalService handles disabled reranking**

Read `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Reranking/ResilientRetrievalService.cs` — confirm it checks `EnableReranking` and falls back to RRF scores when disabled.

**Step 4: Commit**

*(blocco di codice rimosso)*

---

## Phase 3: Replace Qdrant with pgvector

### Task 3.1: Write PgVectorStoreAdapter tests

**Files:**
- Reference: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/External/IQdrantVectorStoreAdapter.cs`
- Reference: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/External/QdrantVectorStoreAdapter.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/PgVectorStoreAdapterTests.cs`

**Step 1: Read the IQdrantVectorStoreAdapter interface**

Read the interface to understand all methods that PgVectorStoreAdapter must implement:
- `SearchAsync(gameId, queryVector, topK, minScore, documentIds?, cancellationToken)`
- `IndexBatchAsync(embeddings, cancellationToken)`
- `DeleteByVectorDocumentIdAsync(vectorDocumentId, cancellationToken)`
- `CollectionExistsAsync(gameId, cancellationToken)`
- `EnsureCollectionExistsAsync(gameId, vectorDimension, cancellationToken)`

**Step 2: Read the existing QdrantVectorStoreAdapter for behavior reference**

Read the implementation to understand:
- How search filters by gameId
- How batch indexing maps chunks to points
- How deletion works

**Step 3: Write unit tests for PgVectorStoreAdapter**

Create test file with tests for:
- `SearchAsync_ReturnsRankedResults_FilteredByGameId`
- `SearchAsync_RespectsMinScore`
- `SearchAsync_FiltersbyDocumentIds`
- `IndexBatchAsync_InsertsVectors`
- `DeleteByVectorDocumentIdAsync_RemovesVectors`
- `CollectionExistsAsync_ReturnsTrueWhenVectorsExist`

Use Testcontainers with PostgreSQL + pgvector for integration tests.

**Step 4: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "PgVectorStoreAdapter" -v n`
Expected: FAIL (PgVectorStoreAdapter class does not exist yet)

**Step 5: Commit failing tests**

*(blocco di codice rimosso)*

### Task 3.2: Implement PgVectorStoreAdapter

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`

**Step 1: Read VectorDocumentEntity to understand the existing schema**

Read the entity that maps to the `vector_documents` table — note column names, types, relationships.

**Step 2: Implement PgVectorStoreAdapter**

Create `PgVectorStoreAdapter` implementing `IQdrantVectorStoreAdapter` (reuse interface — rename later if needed):

Key implementation details:
- Use `MeepleAiDbContext` for DB access
- `SearchAsync`: Raw SQL with pgvector cosine distance `<=>` operator
  *(blocco di codice rimosso)*
- `IndexBatchAsync`: EF Core bulk insert of VectorDocumentEntity rows
- `DeleteByVectorDocumentIdAsync`: EF Core delete by ID
- `CollectionExistsAsync`: Check if any vectors exist for gameId
- `EnsureCollectionExistsAsync`: No-op (pgvector doesn't need collection creation)

**Step 3: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "PgVectorStoreAdapter" -v n`
Expected: PASS

**Step 4: Register in DI**

Modify `KnowledgeBaseServiceExtensions.cs`:
- Replace: `services.AddScoped<IQdrantVectorStoreAdapter, QdrantVectorStoreAdapter>();`
- With: `services.AddScoped<IQdrantVectorStoreAdapter, PgVectorStoreAdapter>();`

**Step 5: Commit**

*(blocco di codice rimosso)*

### Task 3.3: Add HNSW index migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/{timestamp}_AddHnswVectorIndex.cs` (via dotnet ef)

**Step 1: Create EF migration for HNSW index**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddHnswVectorIndex`

**Step 2: Edit the migration to add HNSW index**

The auto-generated migration will be empty. Add the HNSW index creation:

*(blocco di codice rimosso)*

**Step 3: Apply migration locally**

Run: `cd apps/api/src/Api && dotnet ef database update`
Expected: Migration applied, index created

**Step 4: Commit**

*(blocco di codice rimosso)*

### Task 3.4: Remove Qdrant dependencies

**Files:**
- Modify: `apps/api/src/Api/Api.csproj` (remove Qdrant.Client NuGet)
- Modify: `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs` (remove Qdrant HTTP client)
- Modify: `apps/api/src/Api/appsettings.json` (remove Qdrant config section)

**Step 1: Remove Qdrant.Client NuGet package**

Run: `cd apps/api/src/Api && dotnet remove package Qdrant.Client`

**Step 2: Remove Qdrant HTTP client registration**

Read `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs` — find and remove the `AddHttpClient("Qdrant")` registration block.

**Step 3: Remove Qdrant config from appsettings**

Read `apps/api/src/Api/appsettings.json` — find and remove any `"Qdrant"` or `"QdrantUrl"` configuration sections.

**Step 4: Build to verify no compilation errors**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded. If Qdrant.Client types are referenced elsewhere, those files need updating (replace with pgvector calls).

**Step 5: Run full test suite**

Run: `cd apps/api && dotnet test --filter "Category!=Integration" -v n`
Expected: All unit tests pass (integration tests may need Qdrant-specific mocks updated)

**Step 6: Commit**

*(blocco di codice rimosso)*

---

## Phase 4: PDF Processing Transition

### Task 4.1: Add PdfPig NuGet and implement PdfPigTextExtractor

**Files:**
- Modify: `apps/api/src/Api/Api.csproj` (add UglyToad.PdfPig)
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/PdfPigTextExtractor.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/PdfPigTextExtractorTests.cs`

**Step 1: Add PdfPig NuGet package**

Run: `cd apps/api/src/Api && dotnet add package UglyToad.PdfPig`

**Step 2: Read IPdfTextExtractor interface**

Read `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/IPdfTextExtractor.cs` to understand the contract.

**Step 3: Write failing tests**

Create test for:
- `ExtractText_FromSimplePdf_ReturnsText`
- `ExtractText_FromEmptyPdf_ReturnsEmpty`
- `ExtractChunks_RespectsMaxChunkSize`

Use a small test PDF file or generate one in-memory with PdfPig.

**Step 4: Implement PdfPigTextExtractor**

*(blocco di codice rimosso)*

**Step 5: Run tests**

Run: `cd apps/api && dotnet test --filter "PdfPigTextExtractor" -v n`
Expected: PASS

**Step 6: Register in DI as the default extractor**

Modify the PDF processing DI registration to use PdfPigTextExtractor as primary:
*(blocco di codice rimosso)*

**Step 7: Update PdfProcessing config in appsettings.json**

*(blocco di codice rimosso)*

**Step 8: Commit**

*(blocco di codice rimosso)*

### Task 4.2: Create offline PDF pre-processing script

**Files:**
- Create: `scripts/preprocess-pdfs.py`
- Create: `scripts/import-chunks.sh`

**Step 1: Create Python pre-processing script**

This script runs on the dev machine (NOT in production) using the existing unstructured library:

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

---

## Phase 5: Update Background Services and Cleanup

### Task 5.1: Disable Qdrant-dependent background services

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`

**Step 1: Read KnowledgeBaseServiceExtensions.cs**

Identify any hosted services or Quartz jobs that depend on Qdrant, embedding-service, or reranker-service.

**Step 2: Guard background services**

For any services that call Qdrant directly, either:
- Update them to use the new PgVectorStoreAdapter (via the same interface), or
- Disable them via configuration if not needed for MVP

Most services use the `IQdrantVectorStoreAdapter` interface, so they should work automatically with the new PgVectorStoreAdapter.

**Step 3: Build and run unit tests**

Run: `cd apps/api/src/Api && dotnet build && cd ../../../.. && cd apps/api && dotnet test --filter "Category=Unit" -v n`
Expected: Build + all unit tests pass

**Step 4: Commit**

*(blocco di codice rimosso)*

### Task 5.2: Update LLM routing for MVP models

**Files:**
- Modify: `apps/api/src/Api/appsettings.json` (LlmRouting section)

**Step 1: Update LlmRouting to use Qwen2.5-1.5B for local inference**

*(blocco di codice rimosso)*

Changes:
- Anonymous/User/Editor: `meta-llama/llama-3.3-70b-instruct:free` / `llama3:8b` -> `qwen2.5:1.5b` (local Ollama)
- Admin/Premium: Keep cloud models but use DeepSeek (cheaper than Claude)

**Step 2: Update budget limits for MVP scale**

*(blocco di codice rimosso)*

Changes: $50/day -> $5/day, $1000/month -> $50/month (matches EUR 16-26 budget)

**Step 3: Commit**

*(blocco di codice rimosso)*

---

## Phase 6: Integration Testing and Deploy Preparation

### Task 6.1: Create MVP smoke test

**Files:**
- Create: `scripts/smoke-test-mvp.sh`

**Step 1: Create smoke test script**

*(blocco di codice rimosso)*

**Step 2: Commit**

*(blocco di codice rimosso)*

### Task 6.2: Update project documentation

**Files:**
- Modify: `CLAUDE.md` (update Quick Reference, Stack section, Docker commands)

**Step 1: Read current CLAUDE.md**

Note sections that reference eliminated services (Qdrant, Python services, monitoring).

**Step 2: Add MVP deployment section to CLAUDE.md**

Add a new section or update existing:
*(blocco di codice rimosso)*

**Step 3: Commit**

*(blocco di codice rimosso)*

### Task 6.3: Final build verification

**Step 1: Full backend build**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded, 0 errors

**Step 2: Run unit tests**

Run: `cd apps/api && dotnet test --filter "Category=Unit" -v n`
Expected: All tests pass

**Step 3: Frontend build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeded

**Step 4: Start MVP stack locally**

Run: `cd infra && docker compose -f docker-compose.mvp.yml up -d`
Expected: All 6 containers healthy

**Step 5: Run smoke test**

Run: `bash scripts/smoke-test-mvp.sh`
Expected: All checks OK

**Step 6: Tag the release**

*(blocco di codice rimosso)*

---

## Summary: Task Dependency Graph

*(blocco di codice rimosso)*

**Estimated total effort: 12-15 working days**

**Parallelization opportunities:**
- Phase 2 tasks can run in parallel
- Phase 3 and Phase 4 can run in parallel
- Phase 1 has no dependencies, start immediately


---



<div style="page-break-before: always;"></div>

## plans/2026-03-08-llm-system-improvement-prd.md

# LLM System Improvement PRD

**Date**: 2026-03-08
**Status**: Draft
**Author**: Spec Panel (Nygard, Fowler, Newman, Wiegers, Crispin, Hightower, Godin, Adzic)
**Overall Quality Score**: 5.9/10 → Target: 8.0/10

## Executive Summary

Deep analysis of MeepleAI's LLM subsystem revealed 8 critical gaps across operational maturity, architecture quality, transparency, testing, compliance, and scalability. This PRD defines 7 epics with ~40 issues to address these gaps systematically.

The system's foundation is solid — HybridLlmService with circuit breaker, fallback chain, tier-based routing, and comprehensive admin dashboards. But operational levers are missing, configuration is fragmented across 4 layers, the service is a 1100+ LOC monolith, and GDPR compliance has significant gaps.

## Current Architecture Summary

### LLM Stack
- **HybridLlmService** (14 constructor params, 1100+ LOC): Routes between Ollama (local) and OpenRouter (cloud)
- **Circuit Breaker**: 3-state (Closed→Open→HalfOpen), 5 failures to open, 30s recovery
- **Routing**: User tier × RagStrategy → (Provider, Model), with budget overrides
- **Monitoring**: 6 background services, Redis-backed rate limiting, 30-day request logs
- **Admin**: 40+ pages — agent CRUD, pipeline explorer, debug console, strategy config, usage monitoring

### Quality Assessment (as-is)

| Dimension | Score | Target |
|-----------|-------|--------|
| Architecture Clarity | 7.5/10 | 8.5 |
| Operational Readiness | 5.5/10 | 8.0 |
| Configuration Management | 4.5/10 | 7.5 |
| Testability | 6.0/10 | 8.0 |
| Resilience | 7.0/10 | 8.5 |
| Admin Experience | 6.5/10 | 8.0 |
| Requirements Coverage | 4.0/10 | 7.0 |
| GDPR Compliance | 3.0/10 | 8.0 |
| **Overall** | **5.9/10** | **8.0** |

---

## Epic A: LLM Operational Maturity

**Goal**: Give admins operational levers for incident response + observability
**Priority**: 🔴 Critical
**Estimated Issues**: 5

### A1: Admin Emergency Controls API

**Problem**: Admin sees a problem in Debug Console but has no immediate remediation path — must file a ticket to change config and redeploy.

**Solution**: New admin endpoint for immediate operational actions.

*(blocco di codice rimosso)*

**Acceptance Criteria**:
- Admin can force Ollama-only mode (no OpenRouter traffic)
- Admin can reset circuit breaker state for a specific provider
- Admin can flush free model quota cache in Redis
- Admin can pause OpenRouter (all traffic → Ollama)
- All actions are audit-logged with admin ID, reason, timestamp
- Actions auto-revert after configurable duration (default 30min)
- Frontend: Emergency Controls panel in Strategy Config page

### A2: Redis Failure Detection & Alert

**Problem**: When Redis is unavailable, `FreeModelQuotaTracker` and `RateLimitTracker` return null/0 — silently disabling all rate limiting. OpenRouter bills accumulate unchecked.

**Solution**: Detect Redis failures and alert admins.

**Acceptance Criteria**:
- When Redis operations fail, publish `InfrastructureDegradedEvent`
- Admin dashboard shows "⚠️ Rate Limiting Disabled" banner
- Option: hard-fail mode that blocks OpenRouter when Redis is down
- Health endpoint includes Redis rate-limiting subsystem status
- Background service monitors Redis connectivity every 30s

### A3: LLM Subsystem NFR Documentation

**Problem**: No documented SLAs for response time, availability, cost ceilings.

**Solution**: Formal NFR specification.

*(blocco di codice rimosso)*

**Deliverable**: `docs/architecture/adr/adr-XXX-llm-nfr.md`

### A4: Prometheus Metrics Export from .NET

**Problem**: Prometheus + Grafana deployed but not scraping LLM-specific metrics. Admin dashboard is custom React polling REST endpoints — duplicates Grafana's native capability for operational alerting.

**Solution**: Export .NET metrics to Prometheus via `System.Diagnostics.Metrics`.

**Metrics to export**:
- `llm_requests_total{provider, model, status, source}`
- `llm_latency_seconds{provider, model}` (histogram)
- `circuit_breaker_state{provider}` (gauge: 0=closed, 1=open, 2=halfopen)
- `openrouter_balance_usd` (gauge)
- `openrouter_rpm_utilization` (gauge 0-1)
- `free_model_quota_remaining{model}` (gauge)
- `llm_cost_usd_total{provider, model}` (counter)

### A5: Chaos/Integration Test Suite for Resilience Paths

**Problem**: Unit tests strong, but no integration or chaos tests for critical failure paths.

**Solution**: Testcontainers-based chaos tests.

**Test scenarios**:
1. Kill Ollama mid-request → verify circuit opens → verify OpenRouter receives traffic
2. Exhaust OpenRouter RPD → verify Ollama fallback activates
3. Kill Redis → verify graceful degradation (no cost overruns) → verify alert fires
4. OpenRouter 429 burst → verify RPD tracking → verify Ollama fallback
5. All providers down → verify meaningful error to user (not 500)
6. Circuit breaker half-open → verify probe request → verify recovery

---

## Epic B: HybridLlmService Refactoring

**Goal**: Decompose the 14-param god service + unify configuration
**Priority**: 🟡 Important
**Estimated Issues**: 5

### B1: Extract ILlmProviderSelector

**Problem**: HybridLlmService mixes routing, circuit breaking, and quota checking with request orchestration.

**Solution**: Extract provider selection into dedicated service.

*(blocco di codice rimosso)*

**Encapsulates**:
- `HybridAdaptiveRoutingStrategy` calls
- Circuit breaker state checks
- Free model quota checks (RPD exhaustion)
- Fallback chain traversal
- Model exclusion list

### B2: Extract ILlmCostService

**Problem**: Cost logging, budget enforcement, and alerting are mixed into the request flow.

**Solution**: Dedicated cost tracking service.

*(blocco di codice rimosso)*

### B3: Slim HybridLlmService to Orchestrator

**Problem**: 1100+ LOC, 14 constructor params → hard to test and maintain.

**Target**: ~200-300 LOC, 5 constructor params.

*(blocco di codice rimosso)*

### B4: Unified Configuration Dashboard

**Problem**: LLM config scattered across appsettings.json, secrets, DB, Redis — no single view.

**Solution**: New admin page `/admin/agents/config` showing all 4 layers.

| Layer | Content | Editable? |
|-------|---------|-----------|
| Database | Strategy mappings, tier access | ✅ Yes |
| appsettings.json | Circuit breaker, budgets, fallback chain | ❌ Read-only ("requires redeploy") |
| Redis | RPD exhaustion, RPM counters, circuit breaker state | 🔄 Flushable |
| Secrets | API key status, last rotation date | ❌ Read-only |

### B5: Move Budget/CircuitBreaker Config to Database

**Problem**: Changing circuit breaker thresholds or budget limits requires redeployment.

**Solution**: Database-backed config with `appsettings.json` as defaults.

*(blocco di codice rimosso)*

---

## Epic C: LLM Transparency & Editor Experience

**Goal**: Users see quality-appropriate badges, editors/admins see full technical details
**Priority**: 🟡 Important
**Estimated Issues**: 4

### Design Decision: 3-Level Transparency

| Level | Shows | Audience |
|-------|-------|----------|
| **Zero** | Nothing — "MeepleAI Assistant" | N/A (rejected) |
| **Soft** | Quality badge: ⚡Fast / 🎯Balanced / 💎Premium | All users |
| **Full** | Provider, model, tokens, cost, latency, sources | Editor/Admin |

**Decision**: Soft for all users + Full for editor/admin.

### C1: ResponseMetaBadge Component

Soft quality badge shown on every AI response.

*(blocco di codice rimosso)*

**Acceptance Criteria**:
- Badge appears on every AI chat response
- Tooltip shows brief description of the strategy tier
- No technical details visible to non-editor users
- Mobile responsive

### C2: Technical Details Panel (Editor/Admin)

Expandable panel below each AI response showing full metadata.

**Fields**: Provider, Model, Tokens (prompt + completion), Latency, Cost, Strategy, Sources (chunks + scores), Deep link to Debug Console.

**Acceptance Criteria**:
- Only visible to users with Editor or Admin role
- Collapsible by default (click to expand)
- "View in Debug Console" link opens Debug Console filtered to this request
- Copy-to-clipboard for debugging

### C3: Editor Self-Service Usage Page

**Problem**: Editors use LLM daily but have zero visibility into their usage/cost impact.

**Solution**: New page at `/dashboard/my-ai-usage` (not admin-only).

**Content**:
- Personal request count (today, 7d, 30d)
- Tokens used (prompt + completion)
- Cost impact estimate
- Model distribution pie chart
- Strategy distribution
- Recent requests table (own only)

### C4: Deep Link from Chat to Debug Console

When an editor/admin sees a problematic response, they can click "View details" → opens Debug Console pre-filtered to that specific execution ID.

**Requires**: Response metadata includes `executionId` for editor/admin users.

---

## Epic D: A/B Testing Playground

**Goal**: Editors/admins can blind-compare model quality with structured evaluation
**Priority**: 🟡 Important
**Estimated Issues**: 7

### Domain Model

*(blocco di codice rimosso)*

### D1: AbTestSession Domain Entity + Migration

Domain entity, EF configuration, migration.

### D2: A/B Test CQRS Commands & Queries

- `CreateAbTestCommand` → Generates N variants in parallel, stores blind
- `EvaluateAbTestCommand` → Saves scores for all variants
- `RevealAbTestQuery` → Returns models (only after evaluation)
- `GetAbTestsQuery` → Paginated list with filters
- `GetAbTestAnalyticsQuery` → Aggregated win rates, Elo, scores

### D3: A/B Test Backend Endpoints

*(blocco di codice rimosso)*

### D4: Frontend — New A/B Test Page

`/admin/agents/ab-testing/new`

- Query input with KB selector
- Model selection checkboxes (2-4 models)
- "Generate" button → shows loading state → redirects to evaluation

### D5: Frontend — Blind Evaluation Page

`/admin/agents/ab-testing/[id]`

- Side-by-side response columns (A, B, C, D)
- Per-variant scoring: Accuracy, Completeness, Clarity, Tone (1-5 stars)
- Optional notes textarea
- "Submit Evaluation" → reveal models

### D6: Frontend — Analytics Dashboard

`/admin/agents/ab-testing/results`

- **Win rate** per model (% times highest score)
- **Elo rating** (pairwise comparison)
- **Score breakdown** per dimension (accuracy, completeness, clarity, tone)
- **Cost-per-quality-point** (cost / avg score)
- **Segmentation** by KB type (rules, FAQ, mechanics)
- Date range filter

### D7: RequestSource.ABTesting + Budget Isolation

- New `RequestSource.ABTesting` enum value
- Separate daily budget for A/B testing (configurable, default $5/day)
- Rate limit: max 50 tests/day per editor, 200 per admin
- 24h response caching for same query+model

---

## Epic E: Model Versioning & Availability Monitoring

**Goal**: Detect deprecated/unavailable models before they impact users
**Priority**: 🟡 Important
**Estimated Issues**: 5

### E1: ModelAvailabilityCheckJob

Quartz job running every 6 hours.

**Logic**:
1. Read all strategy-model mappings from DB
2. For each OpenRouter model: `GET /api/v1/models` → verify exists
3. If model absent or marked deprecated:
   - Publish `ModelDeprecatedEvent`
   - Create admin notification with suggested replacement
   - If mapping has fallbackModels → activate fallback automatically

### E2: Model Compatibility Matrix (DB Entity)

*(blocco di codice rimosso)*

### E3: Admin Notification on Model Deprecation

Admin receives actionable notification:
*(blocco di codice rimosso)*

### E4: Auto-Fallback on Deprecated Model

If a strategy mapping points to a deprecated model AND the mapping has `fallbackModels`, automatically switch to the first available fallback. Log the switch, notify admin.

### E5: Admin UI — Model Health Page

Add health indicators to existing `/admin/agents/models` page:
- Last verified timestamp per model
- Availability status badge (✅ Available, ⚠️ Deprecated, ❌ Unavailable)
- "Check Now" button to trigger immediate verification
- History of model changes (when swapped, by whom/auto)

---

## Epic F: GDPR Compliance for LLM Subsystem

**Goal**: Ensure legal compliance for AI data processing
**Priority**: 🔴 Critical (legal requirement)
**Estimated Issues**: 11

### Regulatory Context

When MeepleAI sends prompts to OpenRouter (USA-based), it transfers potentially personal data to an external processor. Under GDPR:
- Art. 6: Need legal basis for processing
- Art. 13-14: Transparency obligations
- Art. 17: Right to erasure
- Art. 25: Privacy by design
- Art. 30: Records of processing
- Art. 35: DPIA may be required

### F1: DPA with OpenRouter (Legal)

**Action**: Verify OpenRouter provides a Data Processing Agreement. If not, evaluate alternatives or implement additional safeguards.

**Deliverable**: Documented DPA status + risk assessment.

### F2: Transfer Impact Assessment (Legal)

**Action**: Conduct Transfer Impact Assessment for EU→USA data transfer via OpenRouter.

**Deliverable**: `docs/compliance/transfer-impact-assessment.md`

### F3: DeleteUserLlmDataCommand (Right to Erasure)

*(blocco di codice rimosso)*

### F4: PII Detection/Stripping before OpenRouter

**Problem**: User prompts may contain names, emails, phone numbers. These get sent to OpenRouter.

**Solution**: PII detector that strips/masks PII before sending to external providers.

*(blocco di codice rimosso)*

**Scope**: Only for OpenRouter calls. Ollama (local) doesn't need PII stripping.

**Detection targets**: Email regex, phone regex, Italian fiscal code (codice fiscale), names (NER-based or pattern matching).

### F5: Log Pseudonymization after 7 Days

After 7 days, replace `UserId` in `LlmRequestLogEntity` with SHA-256 hash. Preserves analytics capability while reducing PII exposure.

### F6: AI Consent Tracking

Track user consent for AI processing.

*(blocco di codice rimosso)*

### F7: AI Opt-Out Mechanism

Users can disable AI features and use traditional search only.

**UI**: Toggle in user settings: "Use AI-powered answers" (default: on).
**Backend**: If opted out, RAG pipeline returns only document search results (no LLM generation).

### F8: Privacy Policy Update (AI Section)

Add AI-specific section to privacy policy:
- What data is sent to AI providers
- Which providers are used (OpenRouter, Ollama)
- Data retention periods
- How to opt out
- How to request data deletion

**Deliverable**: Legal text for privacy policy page.

### F9: Record of Processing Document

Permanent record of LLM processing activities (Art. 30).

**Deliverable**: `docs/compliance/record-of-processing-llm.md`

*(blocco di codice rimosso)*

### F10: OpenRouterFileLogger JSONL Erasure

Include JSONL log files in user data deletion flow.

**Options**:
- A: Grep + redact user-specific entries (complex)
- B: Accept 30-day auto-cleanup as sufficient (pragmatic)
- C: Switch to structured DB logging instead of file-based (cleanest)

**Recommendation**: Option C (migrate to DB) for new entries, Option B for existing.

### F11: DPIA Simplified Assessment

Conduct a simplified Data Protection Impact Assessment.

**Risk assessment**:
- Data sensitivity: Low (board game preferences, not health/financial)
- Profiling: Minimal (tier-based, not behavioral)
- External processing: Medium risk (OpenRouter USA)
- Mitigation: Strong (80% local, PII stripping, pseudonymization)

**Deliverable**: `docs/compliance/dpia-llm.md`

---

## Epic G: Multi-Region Preparation

**Goal**: Prepare architecture for future geographic expansion without over-engineering
**Priority**: 🟢 Low (future preparation)
**Estimated Issues**: 3

### Scaling Roadmap

*(blocco di codice rimosso)*

### G1: Add UserRegion to LlmRoutingDecision

*(blocco di codice rimosso)*

No behavioral change — field is populated but ignored by routing strategy.

### G2: Region-Aware Routing Strategy (No-Op)

Add region parameter to routing interface. Current implementation ignores it. Future implementation can use it for geographic routing.

### G3: Multi-Region Architecture Document

**Deliverable**: `docs/architecture/adr/adr-XXX-multi-region-strategy.md`

Covers: Ollama placement, Qdrant replication, embedding service location, CDN strategy, cost projections per phase.

---

## Implementation Priority Matrix

| Epic | Priority | Effort | Risk if Delayed | Recommended Phase |
|------|----------|--------|-----------------|-------------------|
| **F: GDPR** | 🔴 Critical | High | Legal liability | Phase 1 (immediate) |
| **A: Operational Maturity** | 🔴 Critical | Medium | Incident response gap | Phase 1 |
| **C: Transparency** | 🟡 Important | Medium | UX/trust debt | Phase 2 |
| **B: Refactoring** | 🟡 Important | High | Tech debt acceleration | Phase 2 |
| **E: Model Versioning** | 🟡 Important | Medium | Silent failures | Phase 2 |
| **D: A/B Testing** | 🟡 Important | High | Quality measurement gap | Phase 3 |
| **G: Multi-Region** | 🟢 Low | Low | None (preparation) | Phase 3 |

### Phase 1 (Immediate — 2-4 weeks)
- F1, F2, F3, F6, F8, F9 (GDPR critical path)
- A1, A2 (emergency controls + Redis detection)
- A3 (NFR documentation)

### Phase 2 (Short-term — 4-8 weeks)
- F4, F5, F7, F10, F11 (GDPR completion)
- C1, C2, C3, C4 (transparency)
- B1, B2, B3 (service decomposition)
- E1, E2, E3, E4, E5 (model versioning)
- A4, A5 (Prometheus + chaos tests)

### Phase 3 (Medium-term — 8-12 weeks)
- B4, B5 (config unification)
- D1-D7 (A/B testing)
- G1, G2, G3 (multi-region prep)

---

## Dependencies

*(blocco di codice rimosso)*

## Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Admin incident response time | ~30min (redeploy) | <2min (UI action) | Time from alert to remediation |
| Configuration layers requiring redeploy | 2 of 4 | 0 of 4 | Count of non-runtime-editable configs |
| GDPR compliance items addressed | 0/13 | 13/13 | Checklist completion |
| HybridLlmService constructor params | 14 | 5 | Code metric |
| HybridLlmService LOC | 1100+ | ~250 | Code metric |
| Chaos test coverage for failure paths | 0 | 6 scenarios | Test count |
| Model deprecation detection time | ∞ (manual) | <6h (automated) | Job frequency |
| A/B test evaluations per month | 0 | 100+ | Analytics count |

---

## Appendix: Expert Panel Attribution

| Expert | Domain | Key Contributions |
|--------|--------|-------------------|
| Michael Nygard | Production resilience | Circuit breaker gaps, Redis SPOF, chaos testing |
| Martin Fowler | Architecture | HybridLlmService decomposition, config fragmentation |
| Sam Newman | Distributed systems | Service boundary analysis, operational levers |
| Karl Wiegers | Requirements | Missing NFRs, editor role gap, acceptance criteria |
| Lisa Crispin | Testing | Testability analysis, integration test gaps |
| Kelsey Hightower | Cloud native | Prometheus export, secret management, multi-region |
| Seth Godin | Remarkability | Transparency as differentiator |
| Gojko Adzic | Specification by example | A/B testing scenarios, concrete examples |


---



<div style="page-break-before: always;"></div>

## plans/2026-03-08-pdf-rulebook-implementation-plan.md

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

*(blocco di codice rimosso)*

---

## Execution Strategy

### Sprint Allocation

*(blocco di codice rimosso)*

### Branch Strategy

*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

**Confidence-gated logic**:
*(blocco di codice rimosso)*

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
*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

After all phase issues merged:
*(blocco di codice rimosso)*

---

## Migration Strategy

**Single combined migration per phase** (recommended):

*(blocco di codice rimosso)*

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


---



<div style="page-break-before: always;"></div>

## plans/2026-03-08-pdf-rulebook-processing-prd.md

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

*(blocco di codice rimosso)*

### API Endpoints

*(blocco di codice rimosso)*

### Frontend Integration

*(blocco di codice rimosso)*

### Scenarios (Gherkin)

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

### Structured RAG Integration

*(blocco di codice rimosso)*

### API Endpoints

*(blocco di codice rimosso)*

### Scenarios (Gherkin)

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

### API Endpoints

*(blocco di codice rimosso)*

### Frontend Integration

*(blocco di codice rimosso)*

### Scenarios (Gherkin)

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

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

*(blocco di codice rimosso)*

No interface changes. `IRulebookAnalyzer` contract remains identical. Only the `LlmRulebookAnalyzer` implementation's prompt and model routing change.

---

## Implementation Roadmap

*(blocco di codice rimosso)*

### Dependency Graph

*(blocco di codice rimosso)*

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


---



<div style="page-break-before: always;"></div>

## plans/2026-03-08-rule-source-card-design.md

# RuleSourceCard Component Design

**Date**: 2026-03-08
**Status**: Approved
**Parent Epic**: Content Strategy / Rule Arbiter UX

## Problem

When the AI answers a game rule question, it includes `Citation` objects with `documentId`, `pageNumber`, `snippet`, and `relevanceScore`. Currently these render as small inline `CitationBadge` pills (`p.12`, `p.45`) that open a `PdfPageModal`. This is functional but doesn't surface the actual quoted text, publisher attribution, or relevance confidence — all of which build user trust in the answer.

## Solution

Replace inline `CitationBadge` pills with a single collapsible `RuleSourceCard` block at the end of each assistant message that groups all citations, shows quoted text, and links to both the internal PDF viewer and the publisher's official rulebook page.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Layout | Inline collapsible (not sidebar/modal) | Keeps reading flow, no context switch |
| Multi-citation | Tab chips (one selected at a time) | Compact, scannable, familiar pattern |
| Actions | Both PdfPageModal + publisher link | Internal detail view + legal attribution |
| Relevance display | Casual=hidden, Power=colored border+% | Respects user preference, progressive disclosure |

## User Mode System

A new user setting `appMode: 'casual' | 'power'` controls UI detail level across the app. The RuleSourceCard is the first consumer:

- **Casual**: No relevance scores, clean minimal display
- **Power**: Chip borders colored by relevance (green >= 0.8, amber >= 0.5, default otherwise), percentage badge on chips

This setting will be stored in Zustand user preferences store (localStorage-persisted).

## Component API

*(blocco di codice rimosso)*

## Visual States

### 1. Collapsed (default)
*(blocco di codice rimosso)*

### 2. Expanded — Casual Mode (3 citations, chip "p.12" selected)
*(blocco di codice rimosso)*

### 3. Expanded — Power Mode
Same as casual but chips show relevance:
- `[ p.12 92% ]` with green left border
- `[ p.23 67% ]` with amber left border
- `[ p.45 41% ]` with default border

### 4. Single Citation (no chips)
When only 1 citation, skip chip row — show quote directly.

## Styling

*(blocco di codice rimosso)*

## Integration Point

In `ChatThreadView.tsx`, replace the current citation rendering:

*(blocco di codice rimosso)*

`CitationBadge` is NOT deleted — it remains available for other contexts. `RuleSourceCard` is the new default for chat messages.

## File Structure

*(blocco di codice rimosso)*

## Dependencies

- `shadcn/ui` Collapsible (already in project)
- `lucide-react` icons: BookOpen, FileText, ExternalLink, ChevronRight
- Existing `PdfPageModal` component
- Existing `Citation` type from `@/types/domain`
- New: `useAppMode()` hook from user preferences store

## Accessibility

- Collapsible header: `role="button"`, `aria-expanded`, keyboard Enter/Space
- Chip row: `role="tablist"` + `role="tab"` with arrow key navigation
- Quote block: `role="blockquote"`
- Publisher link: `target="_blank" rel="noopener noreferrer"`
- All interactive elements focusable with visible focus rings

## Out of Scope

- Backend changes (Citation type already has all needed fields)
- PDF hosting/redistribution (links to publisher site only)
- User settings page UI (just the Zustand store + hook for now)
- Localization (hardcoded Italian strings, i18n later)


---



<div style="page-break-before: always;"></div>

## plans/epic-4746-live-game-session-plan.md

# Piano di Implementazione - Epic #4746: Live Game Session System

## Stato Attuale

| Fase | Totale | Completate | Rimanenti |
|------|--------|-----------|-----------|
| Phase 1 - Core MVP | 6 | 4 (BE) | 2 (FE) |
| Phase 2 - Toolkit | 5 | 0 | 5 (3 BE + 2 FE) |
| Phase 3 - Advanced | 5 | 0 | 5 (3 BE + 2 FE) |
| Phase 4 - SSE | 4 | 0 | 4 (3 BE + 1 FE) |
| **Totale** | **20** | **4** | **16** |

### Codice Esistente (Phase 1 BE - completato)
- **Domain**: `GameManagement/Domain/Entities/LiveGameSession.cs`, `LiveSessionPlayer.cs`, `LiveSessionTeam.cs`
- **Enums**: `LiveSessionStatus.cs` (Setup, InProgress, Paused, Completed)
- **Events**: 12 domain events (`LiveSessionCreatedEvent`, `StartedEvent`, etc.)
- **Commands**: 10 commands (Create, Start, Pause, Resume, RecordScore, EditScore, etc.)
- **Queries**: 5 queries (GetSession, GetByCode, GetActiveSessions, GetPlayers, GetScores)
- **Handlers**: 6 handler files (Create, Lifecycle, Player, ScoreAndTurn, Team, Query)
- **Validators**: 5 validator files
- **Repository**: `ILiveSessionRepository` + `LiveSessionRepository`
- **Endpoints**: `Routing/LiveSessionEndpoints.cs`
- **Migration**: `20260219112100_AddLiveGameSession`
- **Tests**: `LiveSessionCommandHandlerTests.cs`, `LiveSessionQueryHandlerTests.cs`, `LiveSessionValidatorTests.cs`

---

## Piano di Esecuzione

### Branch Strategy
- **Base branch**: `main-dev`
- Per ogni issue: `feature/issue-{id}-{desc}` → PR a `main-dev`

---

## Phase 1 - Core MVP (Frontend) — 2 issue

### Issue 1: #4751 - MeepleCard Session Front + Relationship Links Footer
**Tipo**: Frontend | **Dipende da**: #4749 (✅ completata)
**Branch**: `feature/issue-4751-meeplecard-session-front`

**Scope**:
- Estendere `meeple-card.tsx` con `entity="session"`
- Status badge per 4 stati (Setup, InProgress, Paused, Completed)
- Score table (Player x Round matrix)
- Turn sequence (player chips con colori)
- Action buttons context-sensitive per stato
- Relationship Links Footer (4 visible + overflow dropdown)
- Mini MeepleCard Player popup su hover
- Score editing modal

**Agenti `/implementa`**:
- `frontend-architect` → implementazione componenti
- `quality-engineer` → test Vitest componenti

---

### Issue 2: #4752 - MeepleCard Session Back + Tests + Code Review
**Tipo**: Frontend + Testing | **Dipende da**: #4751
**Branch**: `feature/issue-4752-meeplecard-session-back`

**Scope**:
- Card back con statistiche, ranking, timeline
- Status-specific back content (4 varianti)
- Backend unit tests ≥90% coverage
- Frontend component tests (Vitest)
- Code review automatico (5 agenti paralleli)

**Agenti `/implementa`**:
- `frontend-architect` → card back
- `quality-engineer` → test suite completa
- `code-review:code-review` → review automatica

---

## Phase 2 - Toolkit + Snapshot (5 issue)

### Issue 3: #4753 - GameToolkit Bounded Context - Domain Model + CQRS
**Tipo**: Backend | **Dipende da**: Phase 1
**Branch**: `feature/issue-4753-game-toolkit-bc`

**Scope**:
- Nuovo BC `GameToolkit` (Domain + Application + Infrastructure)
- Entities: `GameToolkit`, `ToolkitTool`
- CQRS: Commands/Queries/Handlers per CRUD toolkit
- Repository + EF Configuration
- DI registration

**Agenti `/implementa`**:
- `backend-architect` → domain model + CQRS pattern
- `quality-engineer` → unit tests

---

### Issue 4: #4754 - ToolState Entity + Toolkit ↔ Session Integration
**Tipo**: Backend | **Dipende da**: #4753
**Branch**: `feature/issue-4754-toolstate-integration`

**Scope**:
- `ToolState` entity (stato strumenti per sessione)
- Integration tra GameToolkit e LiveGameSession
- Domain events per toolkit state changes
- Handlers per sincronizzazione

**Agenti `/implementa`**:
- `backend-architect` → entity + integration
- `quality-engineer` → integration tests

---

### Issue 5: #4755 - SessionSnapshot - Delta-based History + State Reconstruction
**Tipo**: Backend | **Dipende da**: #4754
**Branch**: `feature/issue-4755-session-snapshot`

**Scope**:
- `SessionSnapshot` entity con delta compression
- State reconstruction dal chain di snapshots
- Trigger automatici per snapshot (score change, turn advance)
- Query per timeline/history

**Agenti `/implementa`**:
- `backend-architect` → snapshot system
- `quality-engineer` → unit + integration tests

---

### Issue 6: #4757 - ExtraMeepleCard Component Base + Session Tabs
**Tipo**: Frontend | **Dipende da**: #4752
**Branch**: `feature/issue-4757-extra-meeplecard`

**Scope**:
- `ExtraMeepleCard` component (expanded view)
- Tab system per session: Overview, Scores, Tools, Timeline
- Integration con toolkit API
- Responsive design

**Agenti `/implementa`**:
- `frontend-architect` → component system
- `quality-engineer` → Vitest tests

---

### Issue 7: #4758 - Snapshot History Slider UI + Time Travel Mode + Phase 2 Tests
**Tipo**: Frontend + Testing | **Dipende da**: #4755, #4757
**Branch**: `feature/issue-4758-snapshot-slider`

**Scope**:
- History slider UI (timeline scrubber)
- Time travel mode (visualizza stato a punto specifico)
- Test completi Phase 2 (BE + FE)

**Agenti `/implementa`**:
- `frontend-architect` → slider + time travel
- `quality-engineer` → test suite Phase 2

---

## Phase 3 - Advanced Toolkit + Media + AI (5 issue)

### Issue 8: #4759 - CardToolConfig + TimerToolConfig + StateTemplate
**Tipo**: Backend | **Dipende da**: #4753
**Branch**: `feature/issue-4759-tool-configs`

**Scope**:
- `CardToolConfig` domain model (deck management)
- `TimerToolConfig` domain model (timers per turno/fase)
- `StateTemplate` per configurazione stati personalizzati
- CQRS per gestione configurazioni

**Agenti `/implementa`**:
- `backend-architect` → domain models
- `quality-engineer` → unit tests

---

### Issue 9: #4760 - SessionMedia Entity + RAG Agent Integration + Shared Chat
**Tipo**: Backend | **Dipende da**: #4755
**Branch**: `feature/issue-4760-media-rag-chat`

**Scope**:
- `SessionMedia` entity (foto/video/audio per sessione)
- Integration con RAG agent (KnowledgeBase BC)
- Shared chat per sessione (real-time messaging)
- Domain events per media + chat

**Agenti `/implementa`**:
- `backend-architect` → media + RAG integration
- `quality-engineer` → tests

---

### Issue 10: #4761 - Turn Phases from TurnTemplate + Event-Triggered Snapshots
**Tipo**: Backend | **Dipende da**: #4759, #4755
**Branch**: `feature/issue-4761-turn-phases`

**Scope**:
- `TurnTemplate` con fasi strutturate (draw, action, scoring)
- Event-triggered snapshots automatici
- Turn phase progression logic
- Domain events per cambio fase

**Agenti `/implementa`**:
- `backend-architect` → turn system
- `quality-engineer` → tests

---

### Issue 11: #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
**Tipo**: Frontend | **Dipende da**: #4757, #4760
**Branch**: `feature/issue-4762-extra-tabs`

**Scope**:
- Media Tab (gallery foto/video/audio)
- AI Tab (chat con RAG agent)
- Support per altri entity types nel ExtraMeepleCard
- Integration con API media e chat

**Agenti `/implementa`**:
- `frontend-architect` → tabs + integration
- `quality-engineer` → Vitest tests

---

### Issue 12: #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
**Tipo**: Frontend + Testing | **Dipende da**: #4759, #4762
**Branch**: `feature/issue-4763-interactive-cards-timer`

**Scope**:
- Interactive card deck UI (draw, discard, shuffle)
- Timer component (countdown, countup, per-turn)
- Events timeline UI (visual event log)
- Test completi Phase 3

**Agenti `/implementa`**:
- `frontend-architect` → interactive components
- `quality-engineer` → test suite Phase 3

---

## Phase 4 - Multi-Device Real-time SSE (4 issue)

### Issue 13: #4764 - SSE Streaming Infrastructure + Session State Broadcasting
**Tipo**: Backend | **Dipende da**: Phase 2 BE
**Branch**: `feature/issue-4764-sse-infrastructure`

**Scope**:
- SSE endpoint infrastructure (.NET 9)
- Session state broadcasting (push updates a client)
- Connection management (heartbeat, reconnect)
- Event serialization + filtering

**Agenti `/implementa`**:
- `backend-architect` → SSE infrastructure
- `quality-engineer` → integration tests

---

### Issue 14: #4765 - Player Action Endpoints + Host Validation + Conflict Resolution
**Tipo**: Backend | **Dipende da**: #4764
**Branch**: `feature/issue-4765-player-actions`

**Scope**:
- Player action endpoints (move, vote, roll, etc.)
- Host validation (solo host può certe azioni)
- Conflict resolution (concurrent edits)
- Optimistic concurrency control

**Agenti `/implementa`**:
- `backend-architect` → endpoints + validation
- `security-engineer` → authorization review
- `quality-engineer` → tests

---

### Issue 15: #4766 - Session Join via Code + Active Player Roles
**Tipo**: Backend | **Dipende da**: #4765
**Branch**: `feature/issue-4766-join-code-roles`

**Scope**:
- Join session via alphanumeric code
- Player roles (Host, Player, Spectator)
- Role-based access control per session
- Auto-assignment di ruoli

**Agenti `/implementa`**:
- `backend-architect` → join + roles system
- `security-engineer` → RBAC review
- `quality-engineer` → tests

---

### Issue 16: #4767 - SSE Client + Player/Spectator Mode UI + Real-time Notifications + Phase 4 Tests
**Tipo**: Frontend + Testing | **Dipende da**: #4764, #4766
**Branch**: `feature/issue-4767-sse-client-ui`

**Scope**:
- SSE client (EventSource + reconnect logic)
- Player mode UI (interactive, can take actions)
- Spectator mode UI (read-only, real-time updates)
- Real-time notification toasts
- Test completi Phase 4 (BE + FE + E2E)

**Agenti `/implementa`**:
- `frontend-architect` → SSE client + modes
- `quality-engineer` → test suite Phase 4
- `code-review:code-review` → review finale

---

## Dependency Graph

*(blocco di codice rimosso)*

## Ordine di Esecuzione Ottimale

L'ordine rispetta le dipendenze e massimizza il parallelismo BE/FE:

| Step | Issue | Tipo | Parallelo con |
|------|-------|------|---------------|
| 1 | #4751 | FE | #4753 (BE) |
| 2 | #4752 | FE | #4754 (BE) |
| 3 | #4753 | BE | #4751 (FE) |
| 4 | #4754 | BE | #4752 (FE) |
| 5 | #4755 | BE | #4757 (FE) |
| 6 | #4757 | FE | #4755 (BE) |
| 7 | #4759 | BE | #4758 (FE) |
| 8 | #4758 | FE | #4759 (BE) |
| 9 | #4760 | BE | — |
| 10 | #4761 | BE | #4762 (FE) |
| 11 | #4762 | FE | #4761 (BE) |
| 12 | #4763 | FE | #4764 (BE) |
| 13 | #4764 | BE | #4763 (FE) |
| 14 | #4765 | BE | — |
| 15 | #4766 | BE | — |
| 16 | #4767 | FE | — |

## Workflow per ogni Issue (`/implementa`)

Per ogni issue, il workflow `/implementa` esegue:

1. **Setup**: Crea branch `feature/issue-{id}-{desc}` da `main-dev`, setta parent
2. **Analisi**: Legge issue GitHub, identifica requirements e acceptance criteria
3. **Implementazione**: Usa agenti specializzati (backend-architect, frontend-architect)
4. **Testing**: quality-engineer per unit/integration/component tests
5. **Code Review**: code-review automatica
6. **PR**: Crea PR verso `main-dev` con summary
7. **Merge**: Dopo review, merge e cleanup branch
8. **Chiusura**: Aggiorna stato issue locale + GitHub

## Rischi e Mitigazioni

| Rischio | Mitigazione |
|---------|-------------|
| Incompatibilità con SessionTracking BC esistente | Verificare che GameManagement e SessionTracking non abbiano conflitti |
| SSE performance con molti client | Rate limiting + connection pooling in Phase 4 |
| Delta snapshot complexity | Iniziare con full snapshots, ottimizzare dopo |
| MeepleCard component size | Lazy loading dei tab, code splitting |
| Conflitti merge tra issue parallele | Merge frequente di main-dev, piccoli PR |


---



<div style="page-break-before: always;"></div>

## plans/epic-entity-link-toolkit-nav-plan.md

# Piano Implementazione — EntityLink + GameToolkit + Card Navigation

> **Creato**: 2026-02-23
> **Epic**: #5127 (A), #5128 (B), #5129 (C)
> **Issue totali**: 36
> **Design spec**: `docs/frontend/entity-link-card-relationships.md`

---

## Visione d'insieme

*(blocco di codice rimosso)*

**Epic A e B possono partire in parallelo.**
**Epic C parte dopo A8 (endpoints user disponibili).**

---

## FASE 1 — Fondamenta Backend + Toolkit Domain

> **Parallelizzabile**: A e B simultaneamente su branch diversi

### 🔵 Epic A — Sprint 1: BC + Domain

*(blocco di codice rimosso)*

**DoD Fase 1A**: BC creato, domain testato, migration applicata su dev DB.

---

### 🟢 Epic B — Sprint 1: Toolkit Domain (parallelo con A)

*(blocco di codice rimosso)*

**DoD Fase 1B**: Toolkit domain testato, migration applicata.

---

## FASE 2 — CQRS Layer

> A3 (migration) completata → si sblocca A4,A5,A6,A7 in parallelo
> B2 (migration) completata → si sblocca B3,B4,B5 in parallelo

### 🔵 Epic A — Sprint 2: Commands + Queries (4 in parallelo)

*(blocco di codice rimosso)*

**Strategia**: Aprire 4 branch contemporaneamente, implementare in parallelo, merge in sequenza su main-dev.

---

### 🟢 Epic B — Sprint 2: Auto-create + Queries (3 in parallelo)

*(blocco di codice rimosso)*

---

## FASE 3 — Endpoints REST

> A4-A7 merge completati → si sbloccano A8, A9

### 🔵 Epic A — Sprint 3: Endpoints (2 in parallelo)

*(blocco di codice rimosso)*

> ⚠️ **Checkpoint**: Dopo A8 merge → Epic C può iniziare Sprint 1

---

## FASE 4 — Fix di Business Rules + BGG Import + adminClient

> A8, A9 completate → si sbloccano A10, A11, A12, A13 in parallelo

### 🔵 Epic A — Sprint 4: Business Rules + Integrations

*(blocco di codice rimosso)*

---

## FASE 5 — Componenti Frontend EntityLink (Epic C)

> **Prerequisito**: A8 completata (endpoint user disponibile)
> **Branch target**: frontend-dev

### 🔴 Epic C — Sprint 1: Componenti atomici (3 in parallelo)

*(blocco di codice rimosso)*

---

### 🔴 Epic C — Sprint 2: Sezioni composite (3 in parallelo)

*(blocco di codice rimosso)*

---

## FASE 6 — Widget Toolkit (Epic B)

> B3-B5 completati → widget possono partire in parallelo

### 🟢 Epic B — Sprint 3: Widget (6 in parallelo)

*(blocco di codice rimosso)*

**Strategia**: I 6 widget sono completamente indipendenti tra loro. Implementare in sprint separati o tutti assieme se le risorse lo permettono.

---

## FASE 7 — Graph View + Nav Graph + toolkit Card

### 🔴 Epic C — Sprint 3: Graph View

*(blocco di codice rimosso)*

### 🔴 Epic C — Sprint 4: Navigation Graph

*(blocco di codice rimosso)*

### 🟢 Epic B — Sprint 4: toolkit Card

*(blocco di codice rimosso)*

---

## FASE 8 — Tests

> Tutte le implementazioni complete → test di chiusura

### Test finali (3 in parallelo)

*(blocco di codice rimosso)*

---

## Riepilogo Sprint Timeline

| Sprint | Issue | Dipende da | Branch |
|--------|-------|-----------|--------|
| **S1** | A1(#5130), A2(#5131), B1(#5144) | — | main-dev, frontend-dev |
| **S2** | A3(#5132), B2(#5145) | S1 | main-dev, frontend-dev |
| **S3** | A4-A7(#5133-5136), B3-B5(#5146-5148) | S2 | main-dev, frontend-dev |
| **S4** | A8(#5137), A9(#5138) | S3(A) | main-dev |
| **S5** | C3(#5159) | S4(A8) | frontend-dev |
| **S6** | A10-A13(#5139-5142), C1(#5157), C4(#5160) | S4,S5 | main-dev, frontend-dev |
| **S7** | B6-B11(#5149-5154), C2(#5158), C5(#5161), C6(#5162) | S3(B), S5 | frontend-dev |
| **S8** | C7(#5163), C8(#5164), B12(#5155) | S6,S7 | frontend-dev |
| **S9** | A14(#5143), B13(#5156), C9(#5165) | tutti | main-dev, frontend-dev |

---

## Checklist Pre-Implementazione

Per ogni `/implementa {issue}`:

*(blocco di codice rimosso)*

---

## Comandi di avvio rapido

*(blocco di codice rimosso)*

---

## Note tecniche

| Area | Nota |
|---|---|
| `@xyflow/react` | Installare prima di C7: `cd apps/web && pnpm add @xyflow/react` |
| A10 (Session.Games) | Migration NOT NULL — gestire dati esistenti con default temporaneo |
| A12 (BGG) | BGG API pubblica, no auth richiesta, rate limit: 1 req/2s |
| C7 (React Flow) | Usare `@xyflow/react` v12+, non la versione legacy `reactflow` |
| B11 (Whiteboard) | Usare `react-sketch-canvas` (leggero) o `tldraw` (completo) |
| EntityLinkChip | Implementare PRIMA di tutti gli altri componenti C (è dipendenza comune) |


---



<div style="page-break-before: always;"></div>

## plans/mock-audit-implementation-list.md

# Mock/Fake Audit - Implementation List

**Date**: 2026-03-06
**Scope**: Full codebase scan (frontend + backend)

---

## Priority Legend

- **P0 - Critical**: Core feature broken/non-functional, user-facing
- **P1 - High**: Important feature stub, blocks functionality
- **P2 - Medium**: Missing integration, degraded UX
- **P3 - Low**: Nice-to-have, polish items

---

## BACKEND (7 actionable items)

### BE-01 [P0] Trending Games - Mock Data
- **File**: `apps/api/.../SharedGameCatalog/Application/Handlers/GetTrendingGamesQueryHandler.cs:14-26`
- **Problem**: Returns hardcoded list (Wingspan, Terraforming Mars, etc.) with fake scores
- **Implementation**: Create `GameTrendingScores` table, background job to compute trending based on play count/recency, real query

### BE-02 [P0] Session Agent Chat - Placeholder Response
- **File**: `apps/api/.../KnowledgeBase/Application/Handlers/ChatWithSessionAgentCommandHandler.cs:147-162`
- **Problem**: Returns static string "Session agent ready. Full LLM streaming coming in AGT-011" instead of LLM response
- **Implementation**: Integrate HybridLlmService, build proper prompt with game context, implement real streaming

### BE-03 [P0] Session Agent RAG - Stub Response
- **File**: `apps/api/.../SessionTracking/Application/Handlers/ChatCommandHandlers.cs:134-139`
- **Problem**: Returns `[Stub] RAG agent integration pending` instead of real answer
- **Implementation**: Call KnowledgeBase RAG pipeline (vector search + LLM), return real answer with confidence score (Issue #4761)

### BE-04 [P1] Admin Strategy Endpoints - All Placeholder
- **File**: `apps/api/src/Api/Routing/AdminStrategyEndpoints.cs:14-35`
- **Problem**: All 5 CRUD endpoints return empty arrays or echo back IDs
- **Implementation**: Create CQRS handlers, domain entities, repository, validation (Issue #3850)

### BE-05 [P2] System Health Report - Hardcoded Zeros
- **File**: `apps/api/.../Administration/Infrastructure/Services/ReportGeneratorService.SystemHealth.cs:74-75`
- **Problem**: `errorRate = 0.0`, `responseTime = 0.0` always
- **Implementation**: Calculate from LlmRequestLog or Prometheus metrics

### BE-06 [P2] Invite QR Code - SVG Placeholder
- **File**: `apps/api/.../SessionTracking/Application/Handlers/InviteHandlers.cs:152-156`
- **Problem**: Generates placeholder SVG instead of real QR code
- **Implementation**: Add QRCoder NuGet package, encode actual invite URL

### BE-07 [P3] Free Quota Daily Limit - Hardcoded
- **File**: `apps/api/.../KnowledgeBase/Application/Handlers/GetUsageFreeQuotaQueryHandler.cs:20`
- **Problem**: `DefaultDailyLimit = 1000` hardcoded constant
- **Implementation**: Move to appsettings.json or SystemConfiguration table, make configurable per tier

---

## FRONTEND - API/Data Mocks (13 items)

### FE-01 [P0] BGG API - Complete Mock
- **File**: `apps/web/src/lib/api/bgg.ts:10-146`
- **Problem**: Entire module uses `MOCK_GAMES[]` array (5 games) with simulated delays
- **Implementation**: Call real BGG XML API (`/xmlapi2/search`, `/xmlapi2/thing`), parse XML responses, handle rate limits

### FE-02 [P1] RAG Config Store - Fake Save/Load
- **File**: `apps/web/src/stores/ragConfigStore.ts:218,242`
- **Problem**: `saveConfig()` and `loadUserConfig()` use setTimeout instead of API calls
- **Implementation**: Wire to `PUT/GET /api/v1/rag/config` endpoints

### FE-03 [P1] Pipeline Builder Store - Fake Save
- **File**: `apps/web/src/stores/pipelineBuilderStore.ts:107-114`
- **Problem**: `savePipeline()` uses setTimeout, real fetch is commented out
- **Implementation**: Wire to `POST /api/v1/rag/pipelines` endpoint

### FE-04 [P1] Security Page - 2FA Non-Functional
- **File**: `apps/web/src/app/(authenticated)/settings/security/page.tsx:28-157`
- **Problems**:
  - 2FA status hardcoded `useState(false)`
  - Recovery codes hardcoded array
  - QR code shows "QR Code Here" text
  - Download codes not implemented
- **Implementation**: Fetch 2FA status from API, generate real codes on enable, use `qrcode.react` library

### FE-05 [P1] Achievements Page - Mock Data
- **File**: `apps/web/src/app/(authenticated)/profile/achievements/page.tsx:15-56`
- **Problem**: `mockAchievements[]` array with 3 hardcoded achievements
- **Implementation**: Create `useAchievements()` hook, fetch from `/api/v1/achievements`

### FE-06 [P2] Contact Form - No API Call
- **File**: `apps/web/src/app/(public)/contact/page.tsx:61`
- **Problem**: `await new Promise(resolve => setTimeout(resolve, 1500))` instead of API
- **Implementation**: Call `POST /api/v1/contact` to send email via backend

### FE-07 [P2] Settings Page - Privacy + Avatar
- **File**: `apps/web/src/app/(public)/settings/page.tsx:395,770`
- **Problems**:
  - Privacy settings show success toast but don't call API
  - Avatar upload is optimistic-only (no backend persistence)
- **Implementation**: Wire to privacy and avatar upload endpoints when available

### FE-08 [P2] Collection Search - Frontend Only
- **File**: `apps/web/src/app/(authenticated)/library/CollectionPageClient.tsx:142`
- **Problem**: `handleSearchChange()` only sets local state, filters client-side
- **Implementation**: Pass `searchQuery` to `useLibrary(filters)` for server-side search

### FE-09 [P2] KB Tab - Hardcoded PDF State
- **File**: `apps/web/src/components/shared-games/KnowledgeBaseTab.tsx:175`
- **Problem**: `const pdfState: PdfState = 'ready'` always
- **Implementation**: Fetch actual PDF indexing state from document status API

### FE-10 [P2] Library Game Card - Missing Stats
- **File**: `apps/web/src/components/library/MeepleLibraryGameCard.tsx:290-341`
- **Problem**: `formatPlayCount(0)`, `formatWinRate(null)`, `timesPlayed: 0` hardcoded
- **Implementation**: Add playCount/winRate to library API response, wire to card

### FE-11 [P2] Collection Dashboard - State Counts Zero
- **File**: `apps/web/src/components/collection/CollectionDashboard.tsx:629-631`
- **Problem**: `nuovo: 0, inPrestito: 0, wishlist: 0` with TODO comments
- **Implementation**: Extend library stats API to include state breakdown

### FE-12 [P2] SignalR Game State - Not Connected
- **File**: `apps/web/src/hooks/useGameStateSignalR.ts:41,47`
- **Problem**: Hub URL and auth token are TODO placeholders
- **Implementation**: Configure SignalR hub URL from env, get auth token from session

### FE-13 [P3] Impersonation Store - Cookie Security
- **File**: `apps/web/src/store/impersonation/store.ts:101`
- **Problem**: Token in localStorage instead of HttpOnly cookie
- **Implementation**: Migrate to backend Set-Cookie header for proper security

---

## FRONTEND - Unimplemented UI Features (12 items)

### FE-UI-01 [P1] Entity Actions - 5 Stubs
- **File**: `apps/web/src/hooks/use-entity-actions.ts:241-409`
- **Stubs**: Copy session code, trigger download, export chat, open invite modal, RSVP event
- **Implementation**: Implement each action with real clipboard/download/API calls

### FE-UI-02 [P1] Agent Config Modal - Test Agent
- **File**: `apps/web/src/components/library/AgentConfigModal.tsx:146`
- **Problem**: "Test agent" button shows info toast only
- **Implementation**: Call test endpoint when backend provides it

### FE-UI-03 [P1] Agent Info Card - Chat History
- **File**: `apps/web/src/components/agent/AgentInfoCard.tsx:297`
- **Problem**: Chat history section is placeholder
- **Implementation**: Fetch and display real chat history list

### FE-UI-04 [P2] Categories Admin Table - No CRUD
- **File**: `apps/web/src/components/admin/shared-games/categories-table.tsx:30-40`
- **Problem**: Edit, delete, add category all unimplemented (3 TODOs)
- **Implementation**: Add dialogs with API calls for category management

### FE-UI-05 [P2] Session Form - Missing Features
- **File**: `apps/web/src/components/play-records/SessionCreateForm.tsx:304,415`
- **Problems**: Can't load user groups, no dynamic dimension inputs
- **Implementation**: Fetch groups from API, add key-value input for dimensions

### FE-UI-06 [P2] Player Manager - No Autocomplete
- **File**: `apps/web/src/components/play-records/PlayerManager.tsx:194`
- **Problem**: Manual player name entry, no search/autocomplete
- **Implementation**: Add user search with combobox component

### FE-UI-07 [P2] Agent Playground - RAG Filter
- **File**: `apps/web/src/app/admin/(dashboard)/agents/definitions/playground/page.tsx:143,358`
- **Problem**: Can't filter games by RAG-readiness
- **Implementation**: Add `documentCount` to SharedGame API, filter on frontend

### FE-UI-08 [P2] Agent Message - PDF Viewer
- **File**: `apps/web/src/components/agent/AgentMessage.tsx:124`
- **Problem**: PDF references can't be viewed in-app (Issue #4130)
- **Implementation**: Integrate PDF viewer component for source citations

### FE-UI-09 [P2] Shared Games Import - Replace Logic
- **File**: `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step4EnrichAndConfirm.tsx:366`
- **Problem**: Can't replace existing games during import (Issue #4167)
- **Implementation**: Add replace confirmation dialog + API call

### FE-UI-10 [P2] Locked Slot - Waitlist
- **File**: `apps/web/src/components/agent/slots/LockedSlotCard.tsx:194`
- **Problem**: Waitlist signup button does nothing
- **Implementation**: Create waitlist API endpoint, wire up signup flow

### FE-UI-11 [P3] Game State Store - Template Management
- **File**: `apps/web/src/lib/stores/game-state-store.ts:184`
- **Problem**: Template not exposed via state endpoint
- **Implementation**: Backend should return template with state response

### FE-UI-12 [P3] Admin Hub Pages - Content Migration
- **Files**: `apps/web/src/app/admin/(dashboard)/{ai,analytics,config,monitor}/page.tsx`
- **Problem**: 4 stub pages with placeholder content
- **Implementation**: Migrate full content with tab-based layout + ActionBar (Issues #505x)

---

## Summary

| Category | P0 | P1 | P2 | P3 | Total |
|----------|----|----|----|----|-------|
| Backend  | 3  | 1  | 2  | 1  | 7     |
| FE Data  | 1  | 4  | 6  | 2  | 13    |
| FE UI    | 0  | 3  | 7  | 2  | 12    |
| **Total**| **4** | **8** | **15** | **5** | **32** |

### Recommended Implementation Order

**Phase 1 - Core Features** (P0):
1. BE-02 + BE-03: Session Agent real LLM/RAG integration
2. FE-01: BGG API real integration
3. BE-01: Trending games real data

**Phase 2 - Important Features** (P1):
4. FE-04: 2FA security page
5. FE-02 + FE-03: RAG config/pipeline persistence
6. BE-04: Admin strategy CRUD
7. FE-05: Achievements real data
8. FE-UI-01: Entity quick actions

**Phase 3 - Polish** (P2+P3):
9. Remaining items by area


---



<div style="page-break-before: always;"></div>

## plans/voice-input/voice-input-specification.md

# Voice Input Feature Specification

**Document Version**: 1.0
**Date**: 2026-03-07
**Status**: Draft
**Authors**: Expert Specification Panel (Wiegers, Adzic, Fowler, Nygard, Cockburn, Crispin)
**Prerequisite Research**: `docs/plans/voice-input/web-speech-api-research.md`

---

## 1. Executive Summary

Voice input is MeepleAI's differentiating feature: a board gamer at the table speaks a rule question into their phone and gets an answer in seconds. This is the "TikTok moment" -- the feature that gets filmed and shared.

**Strategic value**: No competitor in the board game companion space offers voice-first Q&A during gameplay. This transforms MeepleAI from "another chat app" into a hands-free game assistant.

**Two-phase delivery**:

| Phase | Technology | Timeline | Browser Support | Cost |
|-------|-----------|----------|-----------------|------|
| **Phase 1 (MVP)** | Web Speech API (browser STT) + SpeechSynthesis (browser TTS) | 2-3 weeks | Chrome/Edge only (~75% market) | $0/month |
| **Phase 2 (Production)** | MediaRecorder + WebSocket + Deepgram Nova-3 (server STT) + SpeechSynthesis (browser TTS) | 4-6 weeks after Phase 1 | All browsers with microphone support (~95% market) | ~$18/month per 1000 sessions |

**Key architectural constraint**: The voice layer is a new input/output channel for the existing chat pipeline. It MUST NOT modify the chat message flow, the SSE streaming architecture, or the RAG pipeline. Voice produces text input; the chat system processes it identically to typed text. The response text is optionally spoken aloud via TTS.

**Integration point**: The existing `ChatThreadView.tsx` component has a pre-defined but unimplemented action `{id: 'voice', icon: 'mic', action: 'chat:voice'}` in `config/actions.ts:482-487`. This specification defines what happens when that action is activated.

---

## 2. User Stories & Use Cases

### 2.1 Primary Actor: Board Game Player

**Actor profile**: A person playing a physical board game at a table with 2-6 other people. Their phone is on the table or in hand. They have a rule question and want an answer without stopping the game to search through the rulebook or type a message.

### 2.2 User Stories

| ID | Story | Priority | Phase |
|----|-------|----------|-------|
| US-V-001 | As a player, I want to ask a rule question by voice so I can keep my hands free for the game. | P0 | 1 |
| US-V-002 | As a player, I want to hear the answer spoken aloud so I don't have to read my phone screen. | P1 | 1 |
| US-V-003 | As a player, I want to interrupt the spoken answer by tapping the screen or speaking again. | P1 | 1 |
| US-V-004 | As a player, I want to see my spoken question as text in the chat so I can verify what was heard. | P0 | 1 |
| US-V-005 | As a player, I want a quick-ask mode where I can voice a question without navigating to a specific chat thread. | P2 | 1 |
| US-V-006 | As a player, I want voice input to work in noisy environments (game table with multiple people). | P0 | 2 |
| US-V-007 | As a player on Firefox/Safari, I want voice input to work even though my browser lacks SpeechRecognition. | P1 | 2 |
| US-V-008 | As a player, I want to choose whether answers are spoken aloud or just shown as text. | P1 | 1 |
| US-V-009 | As a player, I want visual feedback showing that the system is listening to me. | P0 | 1 |
| US-V-010 | As a player, I want to switch between Italian and English for voice input. | P2 | 2 |

### 2.3 Use Case: Ask a Rule Question by Voice During Gameplay

**Primary Actor**: Board Game Player
**Goal**: Get an answer to a rule question without interrupting gameplay flow
**Scope**: MeepleAI chat interface (existing thread or Quick Ask)
**Level**: User goal
**Preconditions**: Player has an active chat thread with an agent linked to a game that has uploaded rulebook(s). Browser supports required APIs. Microphone permission granted.

**Main Success Scenario (MSS)**:

1. Player taps the mic button in the chat input area.
2. System requests microphone permission (first time only; browser remembers).
3. System displays listening state: pulsing ring around mic icon, "Listening..." label.
4. Player speaks their question: "Can I build two roads in one turn?"
5. System displays interim transcript as the player speaks.
6. Player stops speaking. System detects 2 seconds of silence and auto-stops listening.
7. System displays final transcript in the input area and shows "Sending..." state.
8. System sends the transcript text to the existing chat pipeline via `handleSendMessage(transcript)`.
9. User message appears in the chat thread (identical to a typed message).
10. SSE streaming begins; assistant response streams in (existing flow, unchanged).
11. When the complete response is available, system speaks it aloud via SpeechSynthesis.
12. System returns to idle state.

**Extensions**:

| Step | Extension | Handling |
|------|-----------|----------|
| 2a | Permission denied | Show persistent banner: "Microphone access required for voice input. [Enable in Settings]". Fall back to text input. |
| 3a | Browser does not support SpeechRecognition (Phase 1) | Show toast: "Voice input requires Chrome or Edge. Use text input instead." Mic button shows disabled state with tooltip. |
| 3b | Browser does not support SpeechRecognition (Phase 2) | Silently use MediaRecorder + server-side STT path. No user-visible difference. |
| 5a | No speech detected for 5 seconds | Auto-stop. Show: "No speech detected. Tap the mic to try again." Return to idle. |
| 5b | Recognition error (network, audio) | Show error inline: "Couldn't process voice. Try again or type your question." Return to idle. |
| 6a | Player taps mic button again to manually stop | System stops listening immediately, processes what was captured so far. |
| 6b | Transcript is empty after processing | Show: "Didn't catch that. Tap the mic to try again." Return to idle. |
| 8a | Player edits transcript before sending | Player can modify the transcript text in the input field, then tap send. This is the "review before send" option. |
| 11a | Player taps screen during TTS playback | `speechSynthesis.cancel()` called. TTS stops. Response text remains visible. |
| 11b | Player starts speaking during TTS playback (barge-in) | STT `onspeechstart` triggers `speechSynthesis.cancel()`. New listening session begins. |
| 11c | TTS auto-read preference is OFF | Skip step 11. Response is text-only. |

### 2.4 Use Case: Quick Ask Mode

**Primary Actor**: Board Game Player
**Goal**: Ask a voice question without navigating to a specific chat thread
**Scope**: Standalone `/ask` route
**Level**: User goal
**Preconditions**: Player is logged in and has at least one game with an uploaded rulebook.

**Main Success Scenario**:

1. Player navigates to `/ask` (or taps a floating "Quick Ask" button on the dashboard).
2. System shows a minimal UI: large mic button, game selector dropdown, last-used game pre-selected.
3. Player taps the mic button.
4. Steps 3-11 from the primary use case execute.
5. Response is shown in a single-message view (not a full chat thread).
6. System auto-creates a chat thread in the background for history purposes.
7. Player can tap "Continue in Chat" to open the full thread, or ask another question.

---

## 3. Functional Requirements

### 3.1 Voice Input (STT)

| ID | Requirement | Acceptance Criteria | Phase |
|----|------------|---------------------|-------|
| FR-V-001 | The system SHALL provide a mic button in the chat input area that activates voice recognition. | Mic button visible next to the text input. Tapping it starts listening. Button uses the existing `chat:voice` action definition from `config/actions.ts`. | 1 |
| FR-V-002 | The system SHALL display interim transcription results in real-time as the user speaks. | Interim text appears in the input textarea within 500ms of speech. Text updates as recognition refines. | 1 |
| FR-V-003 | The system SHALL auto-stop listening after 2 seconds of silence. | Recognition stops automatically. Final transcript is placed in the input field. Silence threshold is configurable (default: 2000ms). | 1 |
| FR-V-004 | The system SHALL allow manual stop by tapping the mic button again. | Tapping mic during listening immediately stops recognition and processes captured audio. | 1 |
| FR-V-005 | The system SHALL auto-send the transcript after recognition completes (configurable). | Default behavior: auto-send after 500ms delay (allowing user to review). User can toggle "Review before send" in voice settings. | 1 |
| FR-V-006 | The system SHALL support Italian (`it-IT`) and English (`en-US`) speech recognition. | Language selection follows the app's current locale. Manual override available in voice settings. | 1 |
| FR-V-007 | The system SHALL display the final transcript as a user message in the chat thread, identical to typed messages. | The message object is `{role: 'user', content: transcript}`. No visual difference from typed messages. Optional "via voice" indicator (small mic icon on message). | 1 |
| FR-V-008 | The system SHALL handle the case where no speech is detected within a timeout period. | If no `speechstart` event fires within 5 seconds, recognition stops. Error message displayed. | 1 |
| FR-V-009 | The system SHALL cancel any in-progress STT when the user navigates away from the chat. | Navigation (route change, tab close) calls `recognition.abort()` and cleans up all audio resources. | 1 |
| FR-V-010 | The system SHALL provide server-side STT via MediaRecorder + WebSocket + Deepgram for browsers without SpeechRecognition support. | MediaRecorder captures audio as `audio/webm;codecs=opus`. Audio chunks sent via WebSocket to `.NET API`. API forwards to Deepgram Nova-3 streaming endpoint. Transcript returned via WebSocket. | 2 |
| FR-V-011 | The system SHALL enforce a maximum voice input duration of 30 seconds per utterance. | After 30 seconds, recognition auto-stops with message: "Maximum recording time reached." | 1 |

### 3.2 Voice Output (TTS)

| ID | Requirement | Acceptance Criteria | Phase |
|----|------------|---------------------|-------|
| FR-V-020 | The system SHALL speak the assistant's response aloud using SpeechSynthesis when TTS is enabled. | After SSE streaming completes (Complete event received), the full response text is spoken via `speechSynthesis.speak()`. | 1 |
| FR-V-021 | The system SHALL allow the user to toggle TTS on/off via a speaker icon button. | Toggle persisted in localStorage. Default: ON when voice input was used for the triggering question, OFF when question was typed. | 1 |
| FR-V-022 | The system SHALL select an appropriate Italian voice for TTS. | Voice selection priority: (1) Edge "Isabella Online Natural", (2) Chrome "Google italiano", (3) any voice with `lang.startsWith('it')`. Fallback to default voice if no Italian voice found. | 1 |
| FR-V-023 | The system SHALL support barge-in: user can interrupt TTS by tapping the stop button or starting to speak. | `speechSynthesis.cancel()` called on user interaction. 600ms cooldown before new synthesis (Firefox workaround). | 1 |
| FR-V-024 | The system SHALL NOT speak responses longer than 500 characters. | Responses exceeding 500 characters are truncated for TTS with "... read the full response on screen" appended. Full text always shown in chat. | 1 |
| FR-V-025 | The system SHALL strip citations, markdown formatting, and URLs from text before speaking. | TTS receives plain text only. Citation markers like `[p.15]` removed. Markdown headers, bold, links converted to plain text. | 1 |

### 3.3 Quick Ask Mode

| ID | Requirement | Acceptance Criteria | Phase |
|----|------------|---------------------|-------|
| FR-V-030 | The system SHALL provide a `/ask` route with a minimal voice-first interface. | Route renders a centered mic button, game selector, and single response area. No chat history visible. | 1 |
| FR-V-031 | The system SHALL pre-select the user's last-used game in Quick Ask mode. | Game selector defaults to the game from the most recent chat thread. User can change. | 1 |
| FR-V-032 | The system SHALL auto-create a chat thread when a Quick Ask question is sent. | Thread created via existing `api.chat.createThread()`. Thread ID stored for "Continue in Chat" navigation. | 1 |
| FR-V-033 | The system SHALL provide a "Continue in Chat" link after displaying the response. | Link navigates to `/chat/[threadId]` with the full thread context. | 1 |

### 3.4 Voice Settings & Preferences

| ID | Requirement | Acceptance Criteria | Phase |
|----|------------|---------------------|-------|
| FR-V-040 | The system SHALL persist voice preferences in localStorage. | Preferences: `{ ttsEnabled: boolean, autoSend: boolean, voiceLang: string, ttsVoiceURI: string | null }`. Key: `meepleai-voice-prefs`. | 1 |
| FR-V-041 | The system SHALL provide a voice settings panel accessible from the chat input area. | Small gear icon next to mic button opens a popover with TTS toggle, auto-send toggle, language selector. | 1 |

---

## 4. Non-Functional Requirements

| ID | Requirement | Target | Measurement Method |
|----|------------|--------|-------------------|
| NFR-V-001 | Voice recognition latency (speech end to final transcript) | Phase 1: < 2000ms (Chrome). Phase 2: < 800ms (Deepgram streaming). | Performance instrumentation: timestamp at `onspeechend` vs timestamp at final result callback. |
| NFR-V-002 | TTS start latency (response received to first spoken word) | < 500ms | Performance instrumentation: timestamp at SSE Complete event vs `SpeechSynthesisUtterance.onstart`. |
| NFR-V-003 | Total end-to-end latency (speech end to TTS start) | Phase 1: < 8 seconds. Phase 2: < 6 seconds. | Includes STT + RAG pipeline + LLM + TTS init. Measured from `onspeechend` to `utterance.onstart`. |
| NFR-V-004 | Voice feature bundle size | < 15 KB gzipped (Phase 1). < 25 KB gzipped (Phase 2, excluding Deepgram SDK). | Webpack bundle analyzer. |
| NFR-V-005 | Microphone permission grant rate | > 80% of users who tap the mic button | Analytics event: `voice_permission_requested` vs `voice_permission_granted`. |
| NFR-V-006 | Voice input success rate (transcript accepted and sent) | > 70% (Phase 1, quiet). > 85% (Phase 2, any environment). | Analytics: `voice_transcript_sent` / `voice_session_started`. |
| NFR-V-007 | Accessibility: screen reader compatibility | Voice button and all states announced correctly by NVDA, VoiceOver, TalkBack. | Manual testing with each screen reader. |
| NFR-V-008 | Accessibility: keyboard operation | Voice button activable via Enter/Space. Escape cancels listening. | Automated: Playwright keyboard test. |
| NFR-V-009 | Memory usage during voice session | < 50 MB additional memory over baseline chat | Chrome DevTools memory profiling. |
| NFR-V-010 | Battery impact on mobile | Voice session (30 questions) uses < 5% battery on mid-range Android phone | Manual testing with Android battery stats. |
| NFR-V-011 | GDPR compliance (Phase 1) | Consent banner shown before first microphone use explaining that audio is sent to Google servers. | Manual verification. |
| NFR-V-012 | GDPR compliance (Phase 2) | Deepgram processes audio via EU endpoint. No audio persisted beyond transcription. | Infrastructure configuration audit. |
| NFR-V-013 | Concurrent voice sessions | System handles 100 simultaneous WebSocket STT connections per API instance. | Load test with k6/Artillery. |

---

## 5. Architecture & Design

### 5.1 Phase 1: Browser-Native MVP

*(blocco di codice rimosso)*

**Key principle**: Phase 1 adds a new input mechanism (voice-to-text) and a new output mechanism (text-to-speech) around the EXISTING chat flow. The transcript string produced by voice input enters the same `handleSendMessage()` function that typed text uses. The response text that triggers TTS is the same `answer` string from the `onComplete` SSE callback.

### 5.2 Phase 2: Server-Side Production

*(blocco di codice rimosso)*

**WebSocket message protocol**:

*(blocco di codice rimosso)*

**Backend endpoint** (CQRS pattern per project conventions):

*(blocco di codice rimosso)*

### 5.3 Component Interfaces

#### 5.3.1 Provider Abstraction (enables Phase 1 -> Phase 2 swap)

*(blocco di codice rimosso)*

#### 5.3.2 Voice Input Hook

*(blocco di codice rimosso)*

#### 5.3.3 Voice Output Hook

*(blocco di codice rimosso)*

#### 5.3.4 Voice Preferences Store

*(blocco di codice rimosso)*

#### 5.3.5 React Components

*(blocco di codice rimosso)*

### 5.4 Quick Ask Mode (`/ask` route)

*(blocco di codice rimosso)*

**Quick Ask page layout (mobile-first)**:

*(blocco di codice rimosso)*

### 5.5 File Structure

*(blocco di codice rimosso)*

### 5.6 Integration with ChatThreadView.tsx

The voice feature integrates into `ChatThreadView.tsx` with MINIMAL changes to the existing component. The integration points are:

1. **Import hooks**: `useVoiceInput`, `useVoiceOutput`, `useVoicePreferencesStore`
2. **Input area modification**: Add `VoiceMicButton` next to the send button (lines 640-665 of current component)
3. **Transcript overlay**: Render `VoiceTranscriptOverlay` above the input area when `state !== 'idle'`
4. **TTS trigger**: In the existing `onComplete` callback (lines 91-101), add: `if (ttsEnabled && lastMessageWasVoice) speak(answer)`
5. **Message indicator**: Add optional mic icon to user messages that originated from voice

**Lines of code modified in ChatThreadView.tsx**: Approximately 40-60 lines added. Zero existing lines deleted or restructured.

---

## 6. UX Specification

### 6.1 Voice Button States

The `VoiceMicButton` has 5 visual states mapped to `VoiceRecognitionState`:

| State | Icon | Color | Animation | Label (sr-only) | data-testid |
|-------|------|-------|-----------|-----------------|-------------|
| `idle` | Mic outline | `text-muted-foreground` | None | "Start voice input" | `voice-mic-idle` |
| `requesting` | Mic outline | `text-amber-500` | Slow pulse (opacity) | "Requesting microphone access" | `voice-mic-requesting` |
| `listening` | Mic filled | `text-white` on `bg-red-500` | Pulsing ring (scale 1.0->1.3, 1.5s infinite) | "Listening. Tap to stop." | `voice-mic-listening` |
| `processing` | Spinner | `text-amber-500` | Spin animation | "Processing speech" | `voice-mic-processing` |
| `error` | Mic with X | `text-red-500` | Brief shake (200ms) | "Voice error. Tap to retry." | `voice-mic-error` |
| `disabled` | Mic outline | `text-muted-foreground/50` | None | "Voice input not available in this browser" | `voice-mic-disabled` |

**CSS classes for the listening pulse ring**:

*(blocco di codice rimosso)*

### 6.2 Visual Feedback

**Transcript overlay** (appears above input area when listening/processing):

*(blocco di codice rimosso)*

**Transcript overlay styling**:
- Background: `bg-amber-50/80 dark:bg-amber-950/40 backdrop-blur-md`
- Border: `border border-amber-200/50 dark:border-amber-800/50`
- Corner radius: `rounded-xl`
- Padding: `px-4 py-3`
- Position: Absolute, anchored above the input area, max-width matches input area

**TTS speaker button** (on assistant messages):

- Small speaker icon (16x16) at the bottom-right of assistant message bubbles
- Only visible when TTS is supported AND `ttsEnabled` is true
- States: idle (outline), speaking (filled with sound waves animation), unavailable (hidden)

### 6.3 Mobile Considerations

| Concern | Solution |
|---------|----------|
| **Large tap target** | Mic button minimum 48x48px touch target (WCAG 2.5.8). In Quick Ask: 80x80px. |
| **One-hand operation** | Mic button positioned at bottom-right of input area. Quick Ask has centered large button reachable by thumb. |
| **Haptic feedback** | `navigator.vibrate(50)` on mic activation. `navigator.vibrate([30, 50, 30])` on recognition complete. Uses existing `src/lib/haptics.ts`. |
| **Orientation** | Works in both portrait and landscape. Quick Ask is portrait-optimized. |
| **Screen lock** | Recognition stops when screen locks. Warn user if screen lock timeout is < 30 seconds. |
| **Camera capture attr** | Photo upload uses `capture="environment"`. Voice does NOT use file input. Uses `getUserMedia` directly. |
| **iOS Safari** | Phase 1: Show "Use Chrome for voice" message. Phase 2: MediaRecorder path works. SpeechSynthesis works. |
| **PWA** | Voice works in standalone PWA mode. Microphone permission persists. |

### 6.4 Accessibility

| Requirement | Implementation |
|-------------|---------------|
| **Screen reader announcement** | Mic button has `aria-label` that changes with state. State changes announced via `aria-live="polite"` region. |
| **Keyboard operation** | Mic button focusable. Enter/Space activates. Escape cancels listening. Tab moves to next element. |
| **Reduced motion** | Pulse animation disabled when `prefers-reduced-motion: reduce`. Replace with static color change (red background). |
| **High contrast** | Mic button states use sufficient color contrast. Red listening state: WCAG AA contrast ratio > 4.5:1. |
| **ARIA states** | `aria-pressed="true"` when listening. `aria-busy="true"` when processing. `aria-invalid="true"` on error. |
| **Transcript overlay** | `role="status"` with `aria-live="polite"` for interim results. `aria-live="assertive"` for final results. |
| **TTS and screen readers** | When TTS is active AND screen reader detected, prefer screen reader. Set `ttsEnabled: false` automatically. Detect via `navigator.userAgent` heuristics or `matchMedia('(prefers-reduced-motion)')` as proxy. |
| **Focus management** | After voice error, focus returns to mic button. After transcript sent, focus returns to input field. |

---

## 7. Scenarios (Gherkin)

### 7.1 Happy Path: Voice Question in Chat

*(blocco di codice rimosso)*

### 7.2 Error and Edge Cases

*(blocco di codice rimosso)*

### 7.3 Quick Ask Mode

*(blocco di codice rimosso)*

### 7.4 TTS Scenarios

*(blocco di codice rimosso)*

---

## 8. Failure Modes & Graceful Degradation

### 8.1 Failure Mode Catalog

| # | Failure | Detection | Impact | Severity | Recovery Strategy |
|---|---------|-----------|--------|----------|-------------------|
| FM-1 | Browser lacks SpeechRecognition (Firefox, Brave) | `!('webkitSpeechRecognition' in window)` at component mount | Voice input unavailable | Medium | Phase 1: Mic button disabled with tooltip. Phase 2: Auto-switch to server STT. Text input always available. |
| FM-2 | Microphone permission denied | `PermissionError` from `getUserMedia` or `recognition.onerror` with `error: 'not-allowed'` | Voice input blocked | High | Show persistent inline message with link to browser settings. Never auto-request again (browser blocks). |
| FM-3 | Microphone hardware failure | `recognition.onerror` with `error: 'audio-capture'` | Voice input fails | Medium | Show "Microphone not available" error. Suggest checking system audio settings. Fall back to text. |
| FM-4 | Network failure during recognition (Phase 1) | `recognition.onerror` with `error: 'network'` | Transcript not received | Medium | Show "Network error" message. Offer retry. Audio already sent to Google and lost. |
| FM-5 | SpeechRecognition silently stops (60s Chrome bug) | `recognition.onend` fires without preceding `onerror` or valid result | Recognition ends unexpectedly | Low | Auto-restart recognition if `state === 'listening'` and no final result received. Max 3 restarts per session. |
| FM-6 | WebSocket connection failure (Phase 2) | WebSocket `onerror` or `onclose` with abnormal code | Server STT unavailable | High | Circuit breaker: after 3 failures in 60 seconds, fall back to Web Speech API (if available) or text input. Show banner. |
| FM-7 | Deepgram API unavailable (Phase 2) | HTTP 503 or timeout from Deepgram | Server STT unavailable | High | Backend returns error via WebSocket. Frontend falls back to Web Speech API or text input. Circuit breaker in .NET API with 30-second half-open check. |
| FM-8 | TTS voice not available | `speechSynthesis.getVoices()` returns empty or no Italian voice | Response not spoken | Low | Use default voice. If no voices at all, disable TTS silently. Log analytics event. |
| FM-9 | TTS fails to speak | `utterance.onerror` fires | Response not spoken | Low | Log error. Response text is always visible in chat. TTS failure is non-critical. |
| FM-10 | Tab backgrounded during recognition | Chrome stops recognition when tab loses focus | Recognition interrupted | Low | `document.onvisibilitychange` handler: if `hidden`, stop recognition gracefully with message "Voice paused -- tap mic to resume." |
| FM-11 | Multiple rapid mic taps (debounce) | User taps mic button multiple times quickly | State confusion | Low | Debounce: ignore taps within 300ms of last state change. |
| FM-12 | Audio context blocked by browser autoplay policy | `AudioContext` creation fails without user gesture | Voice features broken | Medium | All audio initialization gated behind user gesture (mic button tap). Never auto-start audio context on page load. |

### 8.2 Degradation Hierarchy

*(blocco di codice rimosso)*

**Principle**: Text input and text output are ALWAYS available. Voice is a progressive enhancement. The chat system never depends on voice. A complete failure of all voice features leaves the user with the exact same experience they have today.

### 8.3 Circuit Breaker Configuration (Phase 2)

*(blocco di codice rimosso)*

*(blocco di codice rimosso)*

### 8.4 Timeouts

| Operation | Timeout | On Timeout |
|-----------|---------|------------|
| Microphone permission request | 30 seconds (browser-controlled) | Browser shows "blocked" state |
| No speech detected | 5 seconds | Auto-stop + "No speech detected" message |
| Silence after speech (auto-stop) | 2 seconds | Stop recognition, process transcript |
| Max recording duration | 30 seconds | Auto-stop + "Max time reached" message |
| WebSocket connection (Phase 2) | 5 seconds | Fall back to Web Speech API or text |
| Deepgram transcription (Phase 2) | 10 seconds | Return error via WebSocket |
| TTS utterance start | 3 seconds | Log warning, skip TTS for this response |

---

## 9. Test Strategy

### 9.1 Testing Pyramid

*(blocco di codice rimosso)*

### 9.2 Unit Tests (Automated, CI)

| Component | Test Count | Testing Approach |
|-----------|-----------|------------------|
| `WebSpeechProvider` | 8-10 | Mock `window.webkitSpeechRecognition`. Test state transitions, event dispatching, error handling, timeout enforcement. |
| `text-sanitizer.ts` | 5-7 | Pure function tests. Input markdown/citations -> output plain text. |
| `voice-detection.ts` | 3-4 | Mock `window` properties. Test browser capability detection. |
| `VoiceMicButton` | 5-6 | Render with each state. Verify correct icon, color, animation class, aria attributes. Snapshot tests. |
| `VoiceTranscriptOverlay` | 4-5 | Render with interim/final text. Test edit, send, cancel callbacks. |
| `VoiceSettingsPopover` | 3-4 | Render with preferences. Test toggle callbacks. |
| `TtsSpeakerButton` | 2-3 | Render speaking/idle states. Test toggle callback. |
| `voice/store.ts` | 3-4 | Test Zustand store: preference changes, persistence, defaults. |
| `useVoiceOutput` | 4-5 | Mock `window.speechSynthesis`. Test speak, stop, voice selection, truncation. |

**Mocking pattern for SpeechRecognition** (following project convention with `vi.hoisted`):

*(blocco di codice rimosso)*

### 9.3 Integration Tests (Automated, CI)

| Test | What It Validates |
|------|-------------------|
| `useVoiceInput` + `WebSpeechProvider` | Full hook lifecycle: start -> interim -> final -> auto-send. With mocked `SpeechRecognition`. |
| `useVoiceInput` + error handling | All 8 error codes produce correct state and user message. |
| `useVoiceInput` + `useVoiceOutput` | Barge-in: starting voice input cancels active TTS. |
| `ChatThreadView` + voice integration | Mic button renders, tap triggers listening, transcript flows to `handleSendMessage`. Requires shallow render of `ChatThreadView` with mocked hooks. |
| `VoicePreferencesStore` + localStorage | Preferences persist across store recreations. |
| Quick Ask page | Game selector populated, mic button functional, response card renders after mock response. |

### 9.4 E2E Tests (Manual Trigger, NOT in CI)

Voice E2E tests cannot run in standard CI because:
- `SpeechRecognition` is not available in headless Chrome
- Microphone input cannot be simulated without OS-level audio injection
- TTS output cannot be verified without audio capture

**E2E test approach**:

*(blocco di codice rimosso)*

**CI-safe E2E tests** (test DOM/UI, not audio):
1. Mic button renders on chat page
2. Mic button shows disabled state when SpeechRecognition unavailable (mock `window`)
3. Voice settings popover opens and preferences are toggleable
4. Quick Ask page renders with game selector and mic button
5. Transcript overlay appears when state is simulated to 'listening'

### 9.5 Testing Voice in CI/CD

Since actual voice recognition cannot be tested in CI, the strategy is:

1. **Provider interface testing**: `ISpeechRecognitionProvider` implementations are tested with mocks at the unit level. The interface contract guarantees interchangeability.
2. **Event simulation**: Integration tests fire synthetic events (onresult, onerror, onend) on the mocked SpeechRecognition object to verify the full data flow.
3. **Visual regression**: Screenshot tests for mic button states (idle, listening, processing, error) using Playwright's screenshot comparison.
4. **Contract testing (Phase 2)**: The WebSocket protocol between frontend and backend is tested with a mock WebSocket server that sends predefined `VoiceServerMessage` sequences.

### 9.6 Accessibility Testing

| Test | Method | Tool |
|------|--------|------|
| Screen reader announcement of mic states | Manual | NVDA (Windows), VoiceOver (Mac/iOS) |
| Keyboard operation | Automated | Playwright: Tab to mic, Enter to activate, Escape to cancel |
| Color contrast of all states | Automated | axe-core via `@axe-core/playwright` |
| Focus management after voice operations | Automated | Playwright: verify `document.activeElement` after each state change |
| Reduced motion preference | Automated | Playwright: set `prefers-reduced-motion: reduce`, verify no animation classes |

---

## 10. Migration Path (Phase 1 to Phase 2)

### 10.1 What Changes

| Layer | Phase 1 | Phase 2 | Change Type |
|-------|---------|---------|-------------|
| **STT Provider** | `WebSpeechProvider` | `ServerSttProvider` | **Swap** (same interface) |
| **Provider Factory** | Returns `WebSpeechProvider` | Returns `ServerSttProvider` (prefers) or `WebSpeechProvider` (fallback) | **Modify** |
| **useVoiceInput hook** | Unchanged | Unchanged | **None** |
| **useVoiceOutput hook** | Unchanged | Unchanged | **None** |
| **UI Components** | Unchanged | Unchanged | **None** |
| **Chat integration** | Unchanged | Unchanged | **None** |
| **Backend** | None | New WebSocket endpoint + Deepgram integration | **New** |
| **Infrastructure** | None | Deepgram API key secret | **New** |

### 10.2 Provider Factory Logic (Phase 2)

*(blocco di codice rimosso)*

### 10.3 Migration Checklist

*(blocco di codice rimosso)*

### 10.4 Feature Flag

*(blocco di codice rimosso)*

The `VoiceMicButton` checks `NEXT_PUBLIC_VOICE_ENABLED`. If `false`, the mic button is not rendered at all (not even in disabled state). This allows a clean rollback.

---

## 11. Cost Analysis

### 11.1 Phase 1 Costs

| Item | Cost | Notes |
|------|------|-------|
| Development | ~2-3 weeks (1 developer) | Hooks, components, integration, tests |
| Infrastructure | $0/month | Browser-native APIs, no server costs |
| STT | $0/month | Web Speech API is free (Google pays) |
| TTS | $0/month | Browser SpeechSynthesis is free |
| **Total Phase 1** | **$0/month recurring** | One-time development cost only |

### 11.2 Phase 2 Costs

| Item | Monthly Cost | Calculation |
|------|-------------|-------------|
| Deepgram Nova-3 STT | $18 at 1K sessions | 1000 sessions x 50 questions x 5 sec = 4167 min x $0.0043 |
| Deepgram Nova-3 STT | $180 at 10K sessions | Linear scaling |
| WebSocket infrastructure | $0 (included) | .NET API already runs; WebSocket is a connection upgrade |
| Development | ~4-6 weeks (1 developer) | Server provider, .NET endpoint, Deepgram integration, load testing |
| **Total Phase 2** | **~$18-180/month** | Scales linearly with usage |

### 11.3 Cost Scaling Table

| Monthly Sessions | Voice Questions | Audio Minutes | Deepgram Cost | Per-Session Cost |
|-----------------|----------------|---------------|---------------|------------------|
| 100 | 5,000 | 417 | $1.79 | $0.018 |
| 1,000 | 50,000 | 4,167 | $17.92 | $0.018 |
| 10,000 | 500,000 | 41,667 | $179.17 | $0.018 |
| 100,000 | 5,000,000 | 416,667 | $1,791.67 | $0.018 |

**Break-even analysis**: If voice input increases user retention by even 5%, the $18/month cost at 1K sessions is negligible compared to customer lifetime value. At 100K sessions ($1,792/month), this should be funded by premium tier subscriptions.

### 11.4 Cost Controls

| Control | Implementation |
|---------|---------------|
| Per-user daily limit | Max 100 voice questions per user per day (configurable). After limit: "Voice limit reached. Type your question or try again tomorrow." |
| Audio duration limit | Max 30 seconds per utterance. Prevents accidental long recordings. |
| Rate limiting | Existing 10 req/min rate limiter on chat proxy applies to voice-originated messages too. |
| Free tier restriction | Consider limiting free tier to 20 voice questions/day, unlimited for premium. |
| Monitoring | Dashboard showing daily audio minutes processed, cost, per-user breakdown. Alert at 80% of monthly budget. |

---

## 12. Open Questions & Risks

### 12.1 Open Questions

| # | Question | Impact | Decision Needed By | Proposed Default |
|---|----------|--------|-------------------|------------------|
| OQ-1 | Should voice messages be visually distinguished from typed messages (small mic icon)? | UX polish | Phase 1 development start | Yes, subtle mic icon on voice-originated user messages |
| OQ-2 | Should Quick Ask (`/ask`) be available to non-authenticated users as a demo? | Growth/conversion | Phase 1 development start | No -- requires game + rulebook context which needs auth |
| OQ-3 | Should TTS read the response in the same language the question was asked, or always in the user's locale? | UX edge case | Phase 1 | Match the response language (AI responds in same language as question) |
| OQ-4 | Should we add a "voice" message type to the backend ChatMessage model? | Data model | Before Phase 1 backend work | No -- voice produces text identical to typed. Add `inputMethod: 'voice' \| 'text'` metadata if analytics needed. |
| OQ-5 | Should Phase 2 WebSocket go through the Next.js API proxy or directly to the .NET API? | Architecture | Before Phase 2 | Through Next.js proxy (same CORS/auth pattern as existing SSE) |
| OQ-6 | Should we support voice input in the sidebar game detail panel (`AgentChatPanel`)? | Scope | After Phase 1 | Defer -- chat thread view first, sidebar later |
| OQ-7 | Maximum TTS text length (currently proposed: 500 chars). Is this the right cutoff? | UX | Phase 1 testing | 500 chars. Validate with real rulebook answers during testing. |
| OQ-8 | Should wake word detection ("Hey Meeple") be on the roadmap? | Future feature | Post Phase 2 | Not now -- technically complex in browser, high false positive risk at game table |

### 12.2 Risk Register

| # | Risk | Probability | Impact | Mitigation | Contingency |
|---|------|------------|--------|------------|-------------|
| R-1 | Board game table noise makes Phase 1 voice unusable | High | High | Clear UX guidance ("speak close to phone"). Phase 2 with Deepgram has much better noise handling. | Accept reduced accuracy. "Didn't catch that" retry loop. Always available text fallback. |
| R-2 | Italian game terminology misrecognized | Medium | Medium | Phase 2: Deepgram custom vocabulary/keywords feature. Phase 1: show transcript for user verification. | Auto-send OFF for Italian locale. User reviews and corrects transcript. |
| R-3 | Safari iOS breaks SpeechRecognition mid-session | High | Medium | Phase 1: Do not support Safari. Show "Use Chrome" message. Phase 2: server STT works on all browsers. | Safari users use text input. No degraded voice experience. |
| R-4 | Google deprecates or restricts free SpeechRecognition API | Low | High | Phase 2 eliminates dependency. | Accelerate Phase 2 timeline. |
| R-5 | Deepgram pricing increases significantly | Low | Medium | Contract/pricing tier lock. Budget alerts at 80%. | Switch to AssemblyAI or OpenAI Whisper API (similar pricing, easy swap behind `ITranscriptionService`). |
| R-6 | User privacy concerns about microphone access | Medium | Medium | Clear permission dialog. GDPR consent. No audio persisted. "Voice is optional" messaging. | Respect denial gracefully. Never nag for permission. |
| R-7 | Voice feature distracts from core product quality | Low | High | Ship Phase 1 as MVP behind feature flag. Validate with 10 beta users before general release. | Disable via feature flag if quality issues. |
| R-8 | WebSocket scaling issues under load (Phase 2) | Medium | High | Load test at 2x expected peak. Horizontal scaling of .NET API already in place. | Circuit breaker falls back to Web Speech API. |

### 12.3 Out of Scope (Explicitly Excluded)

- Multi-speaker diarization ("Who asked that question?")
- Voice commands for non-chat actions (dice rolling, timer, scoring)
- Offline voice recognition (Whisper.js) -- deferred to Phase 3+
- Real-time voice translation (ask in Italian, get answer in English)
- Voice-based game state updates ("I just built a city")
- Custom wake word detection
- Audio recording and playback of questions
- Voice output via cloud TTS (e.g., ElevenLabs, OpenAI TTS)

---

## Appendix A: Analytics Events

| Event | Trigger | Properties |
|-------|---------|------------|
| `voice_feature_loaded` | Voice components mount | `{ supported: boolean, phase: 1\|2, browser: string }` |
| `voice_permission_requested` | First mic button tap | `{ browser: string }` |
| `voice_permission_granted` | User grants mic access | `{ browser: string }` |
| `voice_permission_denied` | User denies mic access | `{ browser: string }` |
| `voice_session_started` | Mic button tapped, listening begins | `{ language: string, threadId: string }` |
| `voice_transcript_received` | Final transcript returned | `{ charCount: number, confidence: number, durationMs: number }` |
| `voice_transcript_sent` | Transcript submitted to chat | `{ charCount: number, wasEdited: boolean, autoSend: boolean }` |
| `voice_error` | Any voice error | `{ errorCode: VoiceErrorCode, phase: 1\|2 }` |
| `voice_tts_started` | TTS begins speaking | `{ charCount: number, language: string }` |
| `voice_tts_interrupted` | User interrupts TTS | `{ method: 'button'\|'barge_in', spokenPercentage: number }` |
| `voice_quick_ask_used` | Quick Ask page interaction | `{ gameId: string, continuedInChat: boolean }` |

## Appendix B: Glossary

| Term | Definition |
|------|-----------|
| STT | Speech-to-Text. Converting spoken audio to text transcript. |
| TTS | Text-to-Speech. Converting text to spoken audio. |
| Barge-in | User interrupts the system while it is speaking (TTS). |
| Interim result | Partial transcript that updates in real-time as user speaks. Not final. |
| Final result | Definitive transcript after speech recognition processing completes. |
| Silence detection | System detects that the user has stopped speaking (no audio energy above threshold). |
| Provider | An implementation of `ISpeechRecognitionProvider` that performs STT via a specific technology. |
| Circuit breaker | Pattern that detects repeated failures and temporarily stops attempting the failing operation. |
| Web Speech API | W3C browser API for speech recognition and synthesis. Chrome-only for recognition. |
| Deepgram Nova-3 | Cloud-based STT service with streaming support and noise robustness. |
| Quick Ask | Standalone `/ask` route with minimal UI for one-shot voice questions. |


---



<div style="page-break-before: always;"></div>

## plans/voice-input/web-speech-api-research.md

# Web Speech API Research: Voice-First Board Game Companion

**Research Date**: 2026-03-07
**Confidence Level**: High (85%) -- based on 15+ sources cross-referenced
**Purpose**: Implementation decision support for MeepleAI voice interaction

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [SpeechRecognition API (STT)](#1-speechrecognition-api-stt)
3. [SpeechSynthesis API (TTS)](#2-speechsynthesis-api-tts)
4. [Alternatives to Browser Speech API](#3-alternatives-to-browser-speech-api)
5. [UX Patterns for Voice in Web Apps](#4-ux-patterns-for-voice-in-web-apps)
6. [Real-World Examples](#5-real-world-examples)
7. [Recommendation for MeepleAI](#6-recommendation-for-meepleai)
8. [Sources](#sources)

---

## Executive Summary

The Web Speech API is viable for prototyping but **not production-grade** for a multilingual board game companion. Key findings:

- **Browser support is fragmented**: Only Chrome/Edge have full SpeechRecognition; Firefox has zero support; Safari is partial and unreliable.
- **Audio goes to Google's cloud** by default -- privacy concern. On-device recognition is emerging but requires language pack downloads and is not universally available.
- **Noisy environments** (board game table with multiple talkers) will significantly degrade Web Speech API accuracy. Cloud APIs like Deepgram and AssemblyAI handle this better.
- **Italian support exists** but accuracy is lower than English, and the Web Speech API offers no tuning knobs.
- **Recommended architecture**: Push-to-talk UX + server-side STT (Deepgram or OpenAI gpt-4o-mini-transcribe) + browser SpeechSynthesis for TTS (with cloud TTS fallback for Italian quality).

---

## 1. SpeechRecognition API (STT)

### 1.1 Browser Support Matrix

| Browser          | Desktop | Mobile  | Prefix Required     | Notes                                    |
|------------------|---------|---------|----------------------|------------------------------------------|
| Chrome 33+       | Full    | Full    | `webkitSpeechRecognition` | Best implementation; sends audio to Google servers |
| Edge (Chromium)  | Full    | Full    | `webkitSpeechRecognition` | Same engine as Chrome                    |
| Safari 14.1+     | Partial | Partial | `webkitSpeechRecognition` | Slow responses, transcript duplications, continuous mode broken on iOS |
| Firefox          | None    | None    | N/A                  | No support, no plans to implement        |
| Brave            | None    | None    | N/A                  | Explicitly refuses to implement (privacy) |
| Samsung Internet | None    | None    | N/A                  | Not supported                            |

**Practical impact**: ~70-75% of global browser market is covered (Chrome + Edge). Safari's partial support is unreliable enough to require fallback. Firefox users (~3-6%) are completely excluded.

### 1.2 Continuous vs One-Shot Recognition

| Mode | How It Works | Gotchas |
|------|-------------|---------|
| **One-shot** (`continuous: false`) | Listens for a single utterance, stops after silence detected | Reliable across browsers. Default mode. Good for command-style input. |
| **Continuous** (`continuous: true`) | Keeps listening after each result, accumulates transcript | Broken on iOS Safari (single growing string instead of result list). Chrome creates a forever-growing results array. Can silently stop after ~60 seconds. Must handle `onend` to restart. |

**Key gotcha**: In continuous mode on Chrome, the `results` array grows indefinitely and includes both interim and final results. You must track which results are `isFinal` and manage your own transcript buffer.

**Key gotcha**: Chrome may silently stop continuous recognition after ~60 seconds of inactivity or background tab switching. You need an `onend` handler that auto-restarts.

### 1.3 Language Support (Italian + English)

- **Setting language**: `recognition.lang = 'it-IT'` or `recognition.lang = 'en-US'`
- **Italian is supported** in Chrome's speech recognition backend (Google's cloud service)
- **No runtime language switching**: You must stop and restart recognition to change language
- **No bilingual mode**: Cannot recognize Italian and English simultaneously -- must pick one per session
- **Accuracy**: Italian accuracy is lower than English, particularly for:
  - Technical/gaming terminology
  - Proper nouns (game names, character names)
  - Code-switching (mixing Italian and English in one sentence, common among Italian board gamers)

### 1.4 Accuracy in Noisy Environments

**This is the critical weakness for a board game scenario.**

The Web Speech API provides **zero noise handling configuration**. You cannot:
- Set noise gate thresholds
- Enable noise suppression
- Configure voice activity detection sensitivity
- Perform speaker diarization (separate who is speaking)

In a board game setting (3-6 people talking, dice rolling, card shuffling, ambient music):
- Expect **significant accuracy degradation** (no published benchmarks, but community reports suggest 50-70% accuracy vs 90%+ in quiet environments)
- Cross-talk from other players will be captured as part of the transcript
- No way to isolate the primary speaker

**Contrast with cloud APIs**: Deepgram Nova-3 and AssemblyAI Universal are specifically trained for noisy, multi-speaker environments. AssemblyAI reports 30% diarization improvement in noisy far-field scenarios.

### 1.5 Latency

| Metric | Value | Notes |
|--------|-------|-------|
| Interim results | ~200-500ms | Available with `interimResults: true`; useful for visual feedback |
| Final result after speech ends | ~750ms-1500ms | Chrome waits for silence detection (~750ms threshold) then sends to server |
| Server round-trip | ~300-800ms | Depends on network; audio sent to Google's servers |
| Total end-to-end | ~1-2 seconds | From user stops speaking to final transcript available |
| Safari | ~2-3 seconds | Notably slower than Chrome |

**For a board game Q&A**: 1-2 second latency is acceptable. Users ask a question, wait for the answer. This is not a real-time captioning scenario.

### 1.6 Privacy

**Default behavior (Chrome)**: Audio is streamed to Google's cloud servers for processing. Google's privacy policy applies. Audio may be retained for service improvement.

**On-device recognition (emerging)**:
- Chrome is developing `processLocally` flag for SpeechRecognition
- Requires one-time language pack download per language
- Ensures neither audio nor transcripts leave the device
- Not yet universally available; Italian language pack availability is uncertain
- Spec proposal: https://github.com/WebAudio/web-speech-api/blob/main/explainers/on-device-speech-recognition.md

**GDPR consideration**: Voice is classified as personally identifiable information under GDPR. Sending audio to Google's servers without explicit consent is a compliance risk for EU users (Italy is EU).

### 1.7 Known Limitations and Gotchas Summary

1. **60-second timeout**: Continuous recognition may silently stop after ~60s
2. **No offline support**: Requires internet (audio sent to Google)
3. **No custom vocabulary**: Cannot add board game terms, character names
4. **No confidence tuning**: Cannot set minimum confidence threshold
5. **Safari duplications**: Safari sometimes returns duplicate transcript segments
6. **iOS continuous mode broken**: Results come as single growing string, not array
7. **Tab backgrounding**: Chrome stops recognition when tab loses focus
8. **HTTPS required**: SpeechRecognition only works on HTTPS (or localhost)
9. **Single instance**: Only one SpeechRecognition instance can be active per page
10. **No audio access**: You cannot get the raw audio buffer -- only the transcript

---

## 2. SpeechSynthesis API (TTS)

### 2.1 Browser Support

| Browser | Support | Notes |
|---------|---------|-------|
| Chrome | Full | Good voice selection, system + Google voices |
| Edge | Full | Excellent quality via Microsoft neural voices |
| Safari | Full | Decent quality, macOS/iOS system voices |
| Firefox | Full | System voices only, quality varies by OS |

**TTS has much broader support than STT.** All major browsers support SpeechSynthesis, including Firefox.

### 2.2 Voice Quality

- **System voices** (offline): Vary wildly by OS. Windows SAPI voices are robotic. macOS voices are decent. Android/iOS voices are good.
- **Cloud/neural voices** (Chrome, Edge): Much more natural. Edge's "Microsoft Isabella Online (Natural)" for Italian is high quality.
- **Consistency problem**: Voice availability differs per browser AND per OS. You cannot guarantee a specific voice will be available.

### 2.3 Italian Voice Availability

| Platform | Italian Voice | Quality |
|----------|--------------|---------|
| Chrome Desktop | Google italiano | Decent, slightly robotic |
| Edge Desktop | Microsoft Isabella Online (Natural) | Good, neural quality |
| Safari macOS | Alice (Italian) | Decent |
| iOS Safari | System Italian voices | Good (Apple neural TTS) |
| Android Chrome | Google italiano | Decent |
| Firefox (any OS) | OS system voices only | Varies |

**Best Italian TTS in browser**: Edge with Microsoft Isabella Natural voice.

### 2.4 Interruption Handling (Barge-in)

The `speechSynthesis.cancel()` method stops speech immediately and clears the utterance queue.

**Known issues with barge-in**:
1. **Safari/iOS bug**: Calling `cancel()` triggers the `onerror` event on the utterance being spoken (error type: "canceled"). Your error handler must distinguish cancel from real errors.
2. **Firefox timing bug**: After calling `cancel()`, a new `speak()` call made within ~500ms may be silently swallowed. Workaround: use `setTimeout` of 500-600ms before starting new speech.
3. **Implementing barge-in**: Listen for `SpeechRecognition.onspeechstart` (user started talking) and call `speechSynthesis.cancel()` to stop TTS. Works but requires careful state management to avoid recognition/synthesis fighting each other.

**Recommended pattern**:
*(blocco di codice rimosso)*

---

## 3. Alternatives to Browser Speech API

### 3.1 Whisper.js (Client-Side, via Transformers.js)

| Aspect | Details |
|--------|---------|
| **Library** | `@huggingface/transformers` (Transformers.js) |
| **Model** | whisper-base (73M params, ~200MB download) |
| **Execution** | WebAssembly (CPU) or WebGPU (GPU-accelerated) |
| **Languages** | 100 languages including Italian |
| **Offline** | Yes, fully offline after model download |
| **Privacy** | All processing local, no data leaves device |

**Feasibility assessment**:

| Factor | Rating | Notes |
|--------|--------|-------|
| Accuracy | Good | Comparable to cloud Whisper for clean audio; degrades with noise |
| Italian accuracy | Good | Whisper was trained on multilingual data; Italian well-represented |
| Model download | Concern | 200MB initial download; can cache in IndexedDB |
| Inference speed (CPU) | Slow | 5-15 seconds for a 10-second clip on mid-range laptop |
| Inference speed (WebGPU) | Acceptable | ~1-3 seconds for 10-second clip on GPU-capable device |
| Mobile performance | Poor | Most mobile devices lack WebGPU; CPU inference too slow for real-time |
| Memory usage | High | ~500MB-1GB RAM during inference |
| Browser support for WebGPU | Limited | Chrome 113+, Edge 113+; no Firefox/Safari |

**Verdict**: Whisper.js is viable for desktop-only, tech-savvy users who accept the initial download. Not suitable as the primary STT solution for a general-audience mobile-friendly web app. Could serve as an offline fallback.

### 3.2 Cloud STT APIs Comparison

| Provider | Model | WER (English) | WER (Multilingual) | Latency (Streaming) | Cost per Minute | Italian Support | Noisy Audio |
|----------|-------|---------------|---------------------|---------------------|-----------------|-----------------|-------------|
| **OpenAI** | gpt-4o-mini-transcribe | ~2.5% | ~5% | Not streaming | $0.006 | Yes | Good |
| **OpenAI** | Whisper API | ~6.5% | ~7.4% | Not streaming | $0.006 | Yes | Good |
| **Deepgram** | Nova-3 | ~8.1% | ~6.8% | <300ms | $0.0043 | Yes | Best |
| **AssemblyAI** | Universal | ~5.4% | ~5.8% | ~300ms | ~$0.006 | Yes | Very good |
| **Google Cloud** | STT v2 | ~5-7% | ~6-8% | ~300ms | $0.006-0.012 | Yes | Good |

**Key differentiators for board game use case**:

- **Deepgram Nova-3**: Best for real-time streaming with sub-300ms latency. Best noise robustness. Speaker diarization built-in. Lowest cost. **Best fit for live board game scenario.**
- **OpenAI gpt-4o-mini-transcribe**: Highest accuracy overall. Not streaming (batch only -- send full audio clip). Good for post-recording transcription, not live interaction.
- **AssemblyAI Universal**: Best balance of accuracy and features. 300ms streaming. Good speaker diarization (30% improvement in noisy conditions).

### 3.3 When to Use Client-Side vs Server-Side

| Scenario | Recommendation | Rationale |
|----------|---------------|-----------|
| Quick voice command ("roll dice", "next turn") | Client-side (Web Speech API) | Low latency, simple utterance, no need for high accuracy |
| Rules question during game | Server-side (Deepgram/OpenAI) | Need high accuracy for RAG query, noisy environment |
| Offline mode | Client-side (Whisper.js) | No network available |
| Privacy-critical users | Client-side (Whisper.js) | No audio leaves device |
| Mobile users | Server-side | Client-side ML too heavy for mobile |
| Italian language | Server-side (Deepgram/OpenAI) | Better multilingual models |

---

## 4. UX Patterns for Voice in Web Apps

### 4.1 Activation Mode Comparison

| Pattern | How It Works | Pros | Cons | Board Game Fit |
|---------|-------------|------|------|----------------|
| **Push-to-talk** | User holds/taps button to speak | Clear intent signal; no false activations; battery efficient | Requires hand interaction (hands may be holding cards) | BEST -- avoids cross-talk from other players |
| **Tap-to-toggle** | Tap once to start, tap again to stop | Hands-free during speaking; clear on/off state | Still requires initial tap; may forget to stop | GOOD -- compromise between PTT and always-on |
| **Always-listening** | Continuously captures audio | Fully hands-free | Battery drain; privacy concern; picks up all table conversation; high false activation rate | POOR -- too many false activations at game table |
| **Wake word** ("Hey Meeple") | Listens for keyword, then captures query | Hands-free; clear activation; familiar pattern | Wake word detection in browser is hard (requires always-on processing); cross-talk may trigger it | MODERATE -- appealing but technically complex in browser |

**Recommendation for MeepleAI**: **Tap-to-toggle** as primary, with clear visual state indicator. User taps mic button, asks question, system auto-stops after silence detection (~2 seconds). This avoids:
- Picking up other players' conversation
- Battery drain from always-listening
- Complexity of wake word detection
- Awkwardness of holding a button while speaking

### 4.2 Visual Feedback Patterns

**During listening state**:
- Pulsing microphone icon (CSS animation, no JS overhead)
- Waveform visualization (Web Audio API `AnalyserNode` -- gives real-time audio levels)
- Interim transcript display (shows words as they're recognized)
- Color state: neutral -> active (green/blue pulse) -> processing (amber) -> error (red)

**Recommended minimal implementation**:
*(blocco di codice rimosso)*

### 4.3 Error Handling Patterns

| Error | User Experience | Recovery |
|-------|----------------|----------|
| No speech detected (silence) | "I didn't hear anything. Tap the mic and try again." | Auto-return to idle after 3 seconds |
| Low confidence transcript | Show transcript with "Did you mean: [transcript]?" + confirm/retry buttons | Let user confirm or re-speak |
| Network error | "Can't reach the server. Check your connection." | Offer offline mode if Whisper.js loaded |
| Microphone permission denied | "Microphone access needed. Tap to enable in settings." | Link to browser permission settings |
| Background noise too high | "It's noisy -- try speaking closer to your phone." | Increase noise gate threshold |
| Browser not supported | "Voice not supported in this browser. Try Chrome or Edge." | Fall back to text input |

### 4.4 Mobile Considerations

1. **Microphone permission**: Must be requested via user gesture (tap). Cannot auto-request on page load.
2. **Background behavior**: iOS Safari and Chrome both stop audio capture when app is backgrounded. Recognition will halt.
3. **Screen lock**: Recognition stops when screen locks. Cannot run in background.
4. **Battery impact**: Continuous recognition drains battery. Push-to-talk is significantly more efficient.
5. **Haptic feedback**: Use `navigator.vibrate(50)` on mic activation/deactivation for tactile confirmation.
6. **Proximity**: Mobile users naturally hold phone closer to mouth -- better accuracy than desktop mic.

---

## 5. Real-World Examples

### 5.1 Production Web Apps Using Voice

| App/Platform | Technology | Use Case | Relevance to MeepleAI |
|--------------|-----------|----------|----------------------|
| **Google Search** (voice search) | Web Speech API | One-shot voice queries | Similar: quick question, expect answer |
| **Pipecat** (open-source framework) | Deepgram + various LLMs | Multimodal conversational AI | Architecture reference for voice + AI pipeline |
| **Vapi** (voice AI platform) | Custom STT/TTS | Voice agents for customer service | Shows production patterns for voice + LLM |
| **Speechify** | Multiple STT providers | Reading assistance | Multilingual voice handling patterns |
| **Voiceitt** | Custom models | Accessible speech recognition | Handling non-standard speech patterns |

### 5.2 Voice Pattern for "Quick Question During Board Game"

The ideal interaction flow:

*(blocco di codice rimosso)*

**Total expected latency budget**:
- STT: ~1-2 seconds (Web Speech API) or ~300ms (Deepgram streaming)
- RAG + LLM: ~2-4 seconds (depends on model and retrieval)
- TTS: ~200ms to start speaking (browser SpeechSynthesis)
- **Total: ~3-6 seconds from speech end to audio response start**

This is acceptable for a board game context where players continue their turn while waiting.

---

## 6. Recommendation for MeepleAI

### Architecture Decision

*(blocco di codice rimosso)*

### Recommended Stack

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **Audio Capture** | `MediaRecorder` API | Universal browser support; gives raw audio for server processing |
| **STT (primary)** | Deepgram Nova-3 (server-side) | Best noise handling, lowest latency streaming, Italian support, $0.0043/min |
| **STT (fallback)** | Web Speech API | Zero cost; works for simple commands in quiet environment |
| **STT (offline)** | Whisper.js (optional, desktop only) | Privacy-first users; offline scenarios |
| **TTS (primary)** | Browser SpeechSynthesis | Free; instant; works offline; adequate quality |
| **TTS (Italian quality)** | OpenAI TTS API or Edge neural voices | Better Italian naturalness when quality matters |
| **UX pattern** | Tap-to-toggle with auto-stop | Best fit for board game table scenario |

### Cost Estimate

For a typical board game session (2-3 hours, ~50 voice questions):
- Average question: ~5 seconds of audio
- Total audio: ~250 seconds = ~4.2 minutes
- Deepgram cost: 4.2 * $0.0043 = **$0.018 per session**
- At 1000 sessions/month: **$18/month for STT**

### Implementation Priority

1. **Phase 1 (MVP)**: Tap-to-toggle + Web Speech API (free, Chrome-only, quick to build)
2. **Phase 2 (Production)**: Replace STT with Deepgram streaming via WebSocket through API backend
3. **Phase 3 (Enhancement)**: Add Whisper.js as offline fallback; add speaker diarization for multi-player mode

### Key Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Board game table noise | High | Push-to-talk UX; Deepgram noise-robust models; "speak closer" prompt |
| Italian accuracy | Medium | Deepgram/OpenAI have good Italian; add custom vocabulary for game terms |
| Browser fragmentation | Medium | Always provide text input as fallback; progressive enhancement |
| GDPR compliance | High | Use Deepgram (EU data processing available); get explicit consent for audio |
| Mobile battery drain | Low | Tap-to-toggle (not always-on); short recognition sessions |

---

## Sources

- [MDN Web Speech API](https://developer.mozilla.org/en-US/docs/Web/API/Web_Speech_API)
- [MDN SpeechRecognition](https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition)
- [MDN SpeechRecognition: continuous property](https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition/continuous)
- [MDN SpeechSynthesis.cancel()](https://developer.mozilla.org/en-US/docs/Web/API/SpeechSynthesis/cancel)
- [Can I Use: Speech Recognition](https://caniuse.com/speech-recognition)
- [On-device Web Speech API Explainer](https://github.com/WebAudio/web-speech-api/blob/main/explainers/on-device-speech-recognition.md)
- [W3C TAG On-device Web Speech API Review](https://github.com/w3ctag/design-reviews/issues/1038)
- [Taming the Web Speech API (Andrea Giammarchi)](https://webreflection.medium.com/taming-the-web-speech-api-ef64f5a245e1)
- [Web Speech API: Complete Guide (VocaFuse)](https://vocafuse.com/blog/web-speech-api-vs-cloud-apis/)
- [AssemblyAI: Speech Recognition in the Browser](https://www.assemblyai.com/blog/speech-recognition-javascript-web-speech-api)
- [AssemblyAI: Offline Whisper in Browser](https://www.assemblyai.com/blog/offline-speech-recognition-whisper-browser-node-js)
- [AssemblyAI Benchmarks](https://www.assemblyai.com/benchmarks)
- [AssemblyAI: Speaker Diarization Update](https://www.assemblyai.com/blog/speaker-diarization-update)
- [Deepgram: Speech-to-Text API Pricing 2025](https://deepgram.com/learn/speech-to-text-api-pricing-breakdown-2025)
- [Deepgram: Best Speech-to-Text APIs 2026](https://deepgram.com/learn/best-speech-to-text-apis-2026)
- [Deepgram: Whisper vs Deepgram](https://deepgram.com/learn/whisper-vs-deepgram)
- [Deepgram: Noise-Robust Speech Recognition](https://deepgram.com/learn/noise-robust-speech-recognition-methods-best-practices)
- [Deepgram: Noise Reduction Paradox](https://deepgram.com/learn/the-noise-reduction-paradox-why-it-may-hurt-speech-to-text-accuracy)
- [Whisper WebGPU (Xenova/Hugging Face)](https://github.com/xenova/whisper-web)
- [Whisper WebGPU Tutorial (dev.to)](https://dev.to/proflead/real-time-audio-to-text-in-your-browser-whisper-webgpu-tutorial-j6d)
- [Northflank: Best Open Source STT 2026](https://northflank.com/blog/best-open-source-speech-to-text-stt-model-in-2026-benchmarks)
- [JavaScript Speech Recognition (VideoSDK)](https://www.videosdk.live/developer-hub/stt/javascript-speech-recognition)
- [HadrienGardeur: Web Speech Recommended Voices](https://github.com/HadrienGardeur/web-speech-recommended-voices)
- [Chrome Developers: Voice Driven Web Apps](https://developer.chrome.com/blog/voice-driven-web-apps-introduction-to-the-web-speech-api)
- [Pipecat: Voice AI Framework](https://github.com/pipecat-ai/pipecat)
- [Lollypop: Voice UI Design Best Practices 2025](https://lollypop.design/blog/2025/august/voice-user-interface-design-best-practices/)
- [IxDF: How to Design Voice User Interfaces](https://www.interaction-design.org/literature/article/how-to-design-voice-user-interfaces)
- [Firefox Bug 1522074: cancel() timing issue](https://bugzilla.mozilla.org/show_bug.cgi?id=1522074)
- [Chromium: Web Speech API 60-second limit](https://groups.google.com/a/chromium.org/g/chromium-html5/c/s2XhT-Y5qAc)


---



<div style="page-break-before: always;"></div>

## roadmap/p1-high-execution-plan.md

# P1-High Issues Execution Plan
> Generated: 2026-02-13 | PM Agent | Epic #4071 PDF Status Tracking

## 📊 Overview

**Total Issues**: 10 P1-High
**Total Estimate**: ~20-25 days
**Execution Strategy**: Sequential foundations → Parallel waves → Cleanup
**Workflow**: `/implementa` for each issue

---

## 🔗 Dependency Chain

*(blocco di codice rimosso)*

**Critical Path**: #4208 → #4209 → #4211 → #4210 → #4213 (17-22 days)

---

## 📅 Execution Phases

### **Phase 1: Foundation** (3-4 days) - SEQUENTIAL
**Critical**: State machine must be stable before other work

| Issue | Title | Type | Size | Estimate | Blocking |
|-------|-------|------|------|----------|----------|
| **#4208** | PDF State Machine & Error Handling | Backend | L | 3-4d | #4212, #4209 |

**Deliverables**:
- State transition validation
- Error categorization (Transient/Permanent/Validation)
- Manual retry endpoint
- Automatic retry with exponential backoff
- 15+ unit tests

**Command**: `/implementa 4208`

---

### **Phase 2: Metrics & Streaming** (4-6 days) - PARALLEL

| Issue | Title | Type | Size | Estimate | Blocked By | Can Parallel |
|-------|-------|------|------|----------|------------|--------------|
| **#4212** | Processing Duration Metrics & ETA | Backend | M | 2-3d | #4208 | ✅ with #4209 |
| **#4209** | SSE Progress Stream Public PDFs | Backend | L | 3-4d | #4208 | ✅ with #4212 |

**Parallel Strategy**:
- Both depend only on #4208 (completed in Phase 1)
- No shared code modifications (different files)
- #4212 focuses on metrics DB schema + calculation
- #4209 focuses on SSE streaming infrastructure

**Commands**:
*(blocco di codice rimosso)*

**Merge Order**: #4212 first (ETA needed by #4209), then #4209

---

### **Phase 3: Frontend SSE Hook** (2-3 days) - SEQUENTIAL

| Issue | Title | Type | Size | Estimate | Blocked By |
|-------|-------|------|----------|----------|------------|
| **#4211** | SSE Connection Management & Polling Fallback | Frontend | M | 2-3d | #4209 |

**Why Sequential**: Requires #4209 SSE endpoint to be deployed and tested

**Deliverables**:
- `usePdfProgress` hook with SSE + polling fallback
- Reconnection logic (max 5 attempts, exponential backoff)
- Connection state tracking
- 90%+ test coverage

**Command**: `/implementa 4211`

---

### **Phase 4: UI Components** (4-5 days) - SEQUENTIAL

| Issue | Title | Type | Size | Estimate | Blocked By |
|-------|-------|------|----------|----------|------------|
| **#4210** | Real-time Progress UI Components | Frontend | L | 4-5d | #4211 |

**Why Sequential**: Requires `usePdfProgress` hook from #4211

**Deliverables**:
- 4 components: ProgressModal, ProgressCard, ProgressToast, ProgressBadge
- Storybook documentation (3+ variants each)
- Visual regression tests (Playwright)
- 80%+ component test coverage

**Command**: `/implementa 4210`

---

### **Phase 5: Notifications** (4-5 days) - SEQUENTIAL

| Issue | Title | Type | Size | Estimate | Blocked By |
|-------|-------|------|----------|----------|------------|
| **#4213** | Configurable Notifications System | Fullstack | L | 4-5d | #4210, #4211 |

**Why Sequential**: Integrates UI from #4210 and SSE from #4211

**Deliverables**:
- User preferences UI (toast/email/push toggle)
- Email service integration (async queue)
- PWA push notifications (Service Worker)
- Notification history page (`/notifications`)
- E2E notification delivery tests

**Command**: `/implementa 4213`

---

### **Phase 6: Polish & Quality** (3-5 days) - PARALLEL

| Issue | Title | Type | Estimate | Can Parallel |
|-------|-------|------|----------|--------------|
| **#4180** | Tooltip Accessibility WCAG 2.1 AA | Frontend | 1d | ✅ |
| **#4179** | MeepleCard Permission Integration | Frontend | 1d | ✅ |
| **#4178** | Permission Hooks & Utilities | Frontend | 1-2d | ✅ |
| **#4185** | Integration Testing & Documentation | Docs/Test | 2d | ✅ |

**Parallel Strategy**: All independent, no shared files

**Commands** (4 terminals or sequential):
*(blocco di codice rimosso)*

---

## ⚡ Optimization Opportunities

### Parallel Execution Windows

**Wave 1** (Phase 2): 2 terminals
- T1: `/implementa 4212` (Backend metrics)
- T2: `/implementa 4209` (Backend SSE)
- **Time Saved**: ~2 days (vs sequential)

**Wave 2** (Phase 6): 4 terminals
- T1: `/implementa 4180` (Tooltips)
- T2: `/implementa 4179` (Permissions)
- T3: `/implementa 4178` (Hooks)
- T4: `/implementa 4185` (Testing)
- **Time Saved**: ~3 days (vs sequential)

**Total Parallel Savings**: ~5 days

---

## 📈 Timeline Projection

### Sequential Execution
| Phase | Duration | Cumulative |
|-------|----------|------------|
| Phase 1 | 3-4d | 4d |
| Phase 2 (seq) | 5-7d | 11d |
| Phase 3 | 2-3d | 14d |
| Phase 4 | 4-5d | 19d |
| Phase 5 | 4-5d | 24d |
| Phase 6 (seq) | 5d | 29d |
| **Total** | **~29 days** | |

### Parallel Execution (Recommended)
| Phase | Duration | Cumulative | Parallel? |
|-------|----------|------------|-----------|
| Phase 1 | 3-4d | 4d | No |
| Phase 2 | 3-4d | 8d | ✅ 2 terminals |
| Phase 3 | 2-3d | 11d | No |
| Phase 4 | 4-5d | 16d | No |
| Phase 5 | 4-5d | 21d | No |
| Phase 6 | 2d | 23d | ✅ 4 terminals |
| **Total** | **~23 days** | | **6 days saved** |

---

## 🎯 Resource Allocation

### Per-Issue Resources

| Issue | Backend | Frontend | Fullstack | Tests | Docs |
|-------|---------|----------|-----------|-------|------|
| #4208 | ●●●● | - | - | ●●● | ●● |
| #4212 | ●●● | - | - | ●●● | ● |
| #4209 | ●●●● | - | - | ●●● | ●● |
| #4211 | - | ●●●● | - | ●●●● | ●● |
| #4210 | - | ●●●●● | - | ●●●● | ●●● |
| #4213 | ●● | ●●●● | ●● | ●●●● | ●●● |
| #4180 | - | ●● | - | ●● | ● |
| #4179 | - | ●● | - | ●● | ● |
| #4178 | - | ●●● | - | ●●● | ●● |
| #4185 | - | - | - | ●●●●● | ●●●●● |

**Legend**: ● = 1 day effort

---

## 🔄 PDCA Integration

### Plan Phase (Each Issue Start)
*(blocco di codice rimosso)*

### Do Phase (During Implementation)
*(blocco di codice rimosso)*

### Check Phase (After Implementation)
*(blocco di codice rimosso)*

### Act Phase (Post-Merge)
*(blocco di codice rimosso)*

---

## 🎬 Execution Commands

### Sequential (Safe, Slower)
*(blocco di codice rimosso)*

**Timeline**: ~29 days

### Parallel (Recommended, Faster)
*(blocco di codice rimosso)*

**Timeline**: ~23 days (6 days saved)

---

## 🚨 Risk Mitigation

### High-Risk Issues

| Issue | Risk | Mitigation |
|-------|------|------------|
| #4208 | Complex state machine, race conditions | Extra testing time, code review mandatory |
| #4209 | Multi-client SSE, memory leaks | Load testing, memory profiling |
| #4213 | Email/push integration, external deps | Mock services, graceful degradation |

### Checkpoints

- After #4208: Verify state machine stability before continuing
- After #4211: Test SSE hook thoroughly (E2E with real backend)
- After #4213: Complete integration testing of entire flow

---

## 📝 Session Continuity (Serena Memory)

### Memory Keys

*(blocco di codice rimosso)*

---

## 🎯 Success Criteria

**Phase Completion**:
- All DoD items checked ✅
- Code review passed
- PR merged to main-dev
- Issue closed
- Branch cleaned up

**Overall Completion**:
- 10/10 issues merged
- 0 blocking bugs
- >85% test coverage maintained
- Documentation complete
- PDCA cycles documented for major issues

---

**Status**: PLAN READY
**Next Action**: Start Phase 1 with `/implementa 4208`


---



<div style="page-break-before: always;"></div>

## roadmap/playground-poc-rag-debug-sequence.md

# Implementation Sequence: Playground POC Strategy + RAG Debug Console

**Epics**: #4435 (POC Strategy) + #4436 (RAG Debug)
**Created**: 2026-02-15
**Base Branch**: `main-dev`
**Approach**: Sequential waves, each issue = branch → implement → test → PR → merge → close

---

## Dependency Graph

*(blocco di codice rimosso)*

---

## Execution Sequence

### Wave 1: Foundation (CRITICAL PATH)

#### Step 1 → Issue #4437 - POC Strategy Selector (Default SingleModel)
- **Branch**: `feature/issue-4437-poc-strategy-selector`
- **Parent**: `main-dev`
- **Priority**: P1 Critical
- **Scope**: Backend + Frontend
- **Estimated effort**: Large

**Backend changes**:
1. Add `AgentSearchStrategy? Strategy` to `PlaygroundChatCommand` (default: SingleModel)
2. Add `int? Strategy` to `PlaygroundChatRequest`
3. Update `AgentPlaygroundEndpoints.cs` to map strategy from request
4. Modify `PlaygroundChatCommandHandler`:
   - Branch on strategy (like `AskAgentQuestionCommandHandler` lines 136-190)
   - `RetrievalOnly`: emit Citations + Complete (no Token events, no LLM call)
   - `SingleModel` (default): current behavior (RAG + stream LLM)
   - `MultiModelConsensus`: dual-model calls with consensus
5. Include strategy name in SSE `Complete` event metadata
6. Ensure `AgentDefinition.Create()` defaults to POC strategy
7. Verify `SeedAgentDefinitionsCommandHandler` seeds with POC default

**Frontend changes**:
1. Add `selectedStrategy` to playground store
2. Add strategy radio group in playground page (3 options)
3. Pre-select "SingleModel (POC)" as default
4. Pass strategy in fetch body to SSE endpoint
5. Show strategy description tooltip per option
6. Display used strategy in debug panel from Complete event

**Tests**:
- Backend: 3 strategy paths (RetrievalOnly returns no tokens, SingleModel streams, MultiModel consensus)
- Frontend: Strategy selector renders, default is SingleModel, strategy sent in request

**DoD**: All 3 strategies work via SSE streaming, POC is default, new agents default to POC

---

### Wave 2: Quick Wins (parallel-safe, touch different parts)

#### Step 2 → Issue #4441 - RAG Strategy Info Panel
- **Branch**: `feature/issue-4441-rag-strategy-info`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (small) + Frontend (medium)
- **Depends on**: #4437 (strategy info in SSE events)

**Backend changes**:
1. Extend SSE `Complete` event to include strategy details:
   - Strategy name, type, description
   - Parameters: TopK, MinScore, weights (from AgentStrategy value object)
   - Search type (hybrid/vector/keyword)
2. New SSE event or extend `StateUpdate` with `StrategyInfo` sub-type

**Frontend changes**:
1. New "Strategy Info" collapsible section in `DebugPanel.tsx`
2. Display: strategy name badge, parameter table (TopK, MinScore, weights)
3. Search type icon (hybrid/vector/keyword)
4. Update store with strategy config from SSE

**Tests**: Strategy info renders correctly, parameters match agent config

---

#### Step 3 → Issue #4438 - LLM Provider/Model Override
- **Branch**: `feature/issue-4438-llm-provider-override`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (medium) + Frontend (medium)
- **Depends on**: #4437 (extended handler)

**Backend changes**:
1. Add `string? ModelOverride` and `string? ProviderOverride` to `PlaygroundChatCommand`
2. Add to `PlaygroundChatRequest`
3. In handler: if override provided, use it instead of agent's config
4. Validate model availability (check provider supports the model)
5. Include actual model/provider used in Complete event

**Frontend changes**:
1. Collapsible "Advanced Options" section in playground
2. Model dropdown (list available models per provider)
3. Provider selector (Ollama / OpenRouter)
4. "Reset to Agent Default" button
5. Debug panel shows actual model/provider used vs configured

**Tests**: Override works, fallback to agent default, invalid model rejected

---

### Wave 3: Observability Foundation

#### Step 4 → Issue #4439 - Real Cost Tracking
- **Branch**: `feature/issue-4439-real-cost-tracking`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (medium) + Frontend (small)
- **Depends on**: #4437 (strategy-aware handler)

**Backend changes**:
1. Port cost calculation from `AskAgentQuestionCommandHandler` to playground handler
2. Track per-component: embedding cost + search cost + LLM cost
3. Persist to `LlmCostLog` table (reuse existing `PersistCostLog()` pattern)
4. Extend SSE Complete event with `CostBreakdown`:
   - `embeddingCost`, `searchCost`, `llmCost`, `totalCost`
   - `provider`, `model`, `tokensPerDollar`
5. RetrievalOnly: emit $0.00 cost

**Frontend changes**:
1. Replace estimate `(tokens/1000 * 0.003)` with real cost from backend
2. Show cost breakdown in debug panel (per-component)
3. Color-code: green (free/cheap), yellow (moderate), red (expensive)
4. Cumulative session cost counter

**Tests**: Cost breakdown matches strategy, RetrievalOnly = $0, costs persisted in DB

---

#### Step 5 → Issue #4442 - Per-Step Pipeline Timing Waterfall
- **Branch**: `feature/issue-4442-pipeline-timing`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (medium) + Frontend (large)
- **Depends on**: #4437 (instrumented handler)

**Backend changes**:
1. Instrument each pipeline step with `Stopwatch`:
   - Agent loading (ms)
   - Embedding generation (ms)
   - Vector search (ms)
   - Reranking (ms) — if applicable
   - Context building (ms)
   - LLM generation: first-token-time + total (ms)
   - Post-processing (ms)
2. New SSE event type `PipelineStep` or extend `StateUpdate`:
   *(blocco di codice rimosso)*
3. Include full pipeline timing array in Complete event

**Frontend changes**:
1. New waterfall visualization component in debug panel
2. Horizontal bars: step name, duration, % of total
3. Color-coded by type: retrieval=blue, compute=orange, LLM=purple
4. Hover tooltip: step details (items processed, input/output size)
5. First-token-time annotation on LLM bar

**Tests**: All steps emit timing, waterfall renders, times sum ≈ total latency

---

### Wave 4: Deep Observability (extends Wave 3 instrumentation)

#### Step 6 → Issue #4443 - Cache Observability
- **Branch**: `feature/issue-4443-cache-observability`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (medium) + Frontend (small)
- **Depends on**: #4442 (pipeline instrumentation pattern)

**Backend changes**:
1. Instrument HybridCache calls in handler:
   - Cache lookup: hit/miss
   - Cache key (hashed)
   - TTL remaining (if hit), item age
2. Emit cache status in SSE `StateUpdate` or new `CacheEvent` type
3. Include cache summary in Complete event

**Frontend changes**:
1. "Cache" section in Debug Panel
2. Badge: HIT (green) / MISS (red) / SKIP (gray)
3. Details: key, TTL, age, cache type
4. Session hit rate counter

**Tests**: Cache hit/miss reported correctly, session stats accumulate

---

#### Step 7 → Issue #4444 - API Call Tracing
- **Branch**: `feature/issue-4444-api-call-tracing`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (large) + Frontend (large)
- **Depends on**: #4442 (pipeline instrumentation pattern)

**Backend changes**:
1. Create `ApiTraceCollector` middleware/service
2. Instrument external calls:
   - Embedding API: model, input size, output dimensions, latency
   - Qdrant: collection, query params, results count, latency
   - LLM API: provider, model, prompt size, completion size, status, latency
   - Reranker: input count, output count, latency
3. New SSE event type `ApiTrace`:
   *(blocco di codice rimosso)*
4. Mask sensitive data (API keys, tokens)

**Frontend changes**:
1. New "Network" tab in debug panel (alongside Debug/RAG/Scenarios)
2. Table: timestamp, service, method, status, size, latency
3. Expandable rows: request/response details (payload preview)
4. Filter by service type, sort by latency/timestamp
5. Total calls count + aggregate bandwidth

**Tests**: All external calls traced, no sensitive data leaked, filter/sort works

---

### Wave 5: Consumer Layer

#### Step 8 → Issue #4445 - Developer Console
- **Branch**: `feature/issue-4445-developer-console`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Frontend (large)
- **Depends on**: #4442, #4443, #4444 (needs rich events to display)

**Backend changes** (minimal):
1. Add log-level metadata to SSE events (info/warn/error/debug)
2. Emit debug-level events in playground mode only

**Frontend changes**:
1. New "Console" tab in debug panel
2. Log entries: timestamp, level, source (RAG/LLM/Cache/API), message
3. Filter by level (checkboxes: info/warn/error/debug)
4. Filter by source (dropdown)
5. Text search across logs
6. Auto-scroll with pause on hover
7. Clear + export (JSON/text) buttons
8. Color-coded by level

**Tests**: Console renders all log types, filters work, export produces valid output

---

### Wave 6: Advanced Features (Low Priority)

#### Step 9 → Issue #4440 - Strategy Comparison Mode
- **Branch**: `feature/issue-4440-strategy-comparison`
- **Parent**: `main-dev`
- **Priority**: P3
- **Scope**: Frontend (large)
- **Depends on**: #4437 (all strategies working), #4439 (cost tracking for comparison)

**Backend changes**: None (reuses playground chat endpoint with different strategy params)

**Frontend changes**:
1. "Compare" toggle/button in playground header
2. Sends same question with all 3 strategies (3 parallel SSE connections)
3. Split view: 3 columns (RetrievalOnly | SingleModel | MultiModelConsensus)
4. Per-column: response, tokens, cost, latency, confidence
5. Summary row: comparison table highlighting best/worst per metric
6. Export comparison results

**Tests**: 3 parallel requests fire, results display in columns, metrics compared

---

#### Step 10 → Issue #4446 - TOMAC-RAG Layer Visualization
- **Branch**: `feature/issue-4446-tomac-rag-viz`
- **Parent**: `main-dev`
- **Priority**: P3
- **Scope**: Backend (small) + Frontend (medium)
- **Depends on**: #4442 (timing data for active layers)

**Backend changes**:
1. Include TOMAC layer activation data in Complete event
2. For L3 (Retrieval) and L5 (Generation): per-layer metrics
3. For L1, L2, L4, L6: status "planned"

**Frontend changes**:
1. New TOMAC visualization component
2. Vertical pipeline: 6 layers with arrows
3. Each layer: status badge (Active green / Planned gray / Bypassed orange)
4. Active layers: expand to show metrics
5. Planned layers: description of future capability

**Tests**: All 6 layers render, active layers show metrics

---

## Implementation Commands

Per ogni issue, il flusso `/implement` è:

*(blocco di codice rimosso)*

---

## Summary Table

| Wave | Step | Issue | Title | Depends On | Scope | Priority |
|------|------|-------|-------|-----------|-------|----------|
| 1 | 1 | #4437 | POC Strategy Selector | — | BE+FE Large | P1 |
| 2 | 2 | #4441 | RAG Strategy Info Panel | #4437 | BE+FE Small | P2 |
| 2 | 3 | #4438 | LLM Provider Override | #4437 | BE+FE Medium | P2 |
| 3 | 4 | #4439 | Real Cost Tracking | #4437 | BE+FE Medium | P2 |
| 3 | 5 | #4442 | Per-Step Pipeline Timing | #4437 | BE+FE Large | P2 |
| 4 | 6 | #4443 | Cache Observability | #4442 | BE+FE Medium | P2 |
| 4 | 7 | #4444 | API Call Tracing | #4442 | BE+FE Large | P2 |
| 5 | 8 | #4445 | Developer Console | #4442-44 | FE Large | P2 |
| 6 | 9 | #4440 | Strategy Comparison | #4437,#4439 | FE Large | P3 |
| 6 | 10 | #4446 | TOMAC-RAG Viz | #4442 | BE+FE Medium | P3 |

**Total**: 10 issue, 6 wave, sequenza ottimizzata per dipendenze tecniche e valore di business.


---

