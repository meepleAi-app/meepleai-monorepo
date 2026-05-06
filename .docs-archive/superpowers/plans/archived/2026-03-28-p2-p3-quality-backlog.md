# P2/P3 Quality & Accessibility Backlog

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Address quality, DDD compliance, frontend accessibility, and infrastructure refinements identified by the comprehensive analysis. Prerequisite: Plans A (P0) and B (P1) must be merged first.

**Estimated total effort:** ~80h across all tasks.

---

## P2 Tasks

### Task 1: Decouple KnowledgeBase Domain from Other Bounded Contexts (~16h)

**Problem:** `KnowledgeBase` Domain layer imports from 5 other BCs: GameManagement (domain + application), Authentication (domain), SystemConfiguration (domain + application), DocumentProcessing (domain).

**Strategy:** Replace direct BC imports with shared kernel value objects and domain events. Read data via application-layer queries, not direct domain references.

**Key principle:** Domain layer should only depend on SharedKernel. Cross-BC reads go through Application queries.

- [ ] Identify all cross-BC imports in `KnowledgeBase/Domain/`:
  ```bash
  grep -rn "using Api.BoundedContexts\." apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/
  ```
- [ ] For each import, classify: (a) value type that can be moved to SharedKernel, (b) entity reference that should become a GUID, (c) service call that should become an application query
- [ ] Move `ProcessingState` enum (used cross-BC) to `SharedKernel/Domain/Enums/` if not already there
- [ ] Replace `GameManagement.Domain.Models.Game` references in KB domain with `GameId` (Guid) — domain should not hold cross-BC entity references
- [ ] Remove `SystemConfiguration.Application.Services` import from KB domain layer (Application from Domain is an inverted dependency) — use domain event or pass values via command
- [ ] Build and fix all compilation errors

### Task 2: Fix Remaining CQRS Violations — Repository/Service Direct Injection (~8h)

**Problem:** Beyond IFeatureFlagService (fixed in P1), these endpoint files directly inject repositories or services:
- `Routing/AlertEndpoints.cs` — `IAlertingService`
- `Routing/BggEndpoints.cs` — `IBggApiService` (zero mediator usage)
- `Routing/N8nWebhookEndpoints.cs` — `INotificationRepository`
- `Routing/BatchJobLogsEndpoints.cs` — `IBatchJobRepository`

**Strategy:** Create query/command handlers for each service operation. `BggEndpoints.cs` is the worst offender — every handler needs a corresponding Command or Query.

- [ ] Create `TriggerBggImportCommand` + handler wrapping `IBggApiService.TriggerImportAsync`
- [ ] Create `GetBggGameQuery` + handler wrapping `IBggApiService.GetGameAsync`
- [ ] Create `SendAlertCommand` + handler for `IAlertingService`
- [ ] Create `GetBatchJobLogsQuery` + handler
- [ ] Update each endpoint to use `IMediator.Send()` only

### Task 3: Split Oversized Frontend Components (~8h)

**Problem:** 8 components exceed 300 lines, worst is `admin/agents/config/page.tsx` at 1,512 lines.

- [ ] Split `apps/web/src/app/admin/(dashboard)/agents/config/page.tsx` into:
  - `AgentConfigTabs.tsx` — tab container
  - `AgentGeneralConfigForm.tsx` — general settings form
  - `AgentModelConfigForm.tsx` — model selection form
  - `AgentPromptConfigForm.tsx` — prompt templates form
- [ ] Split `apps/web/src/components/playground/DebugPanel.tsx` (1,034 lines) into section components
- [ ] Split `apps/web/src/components/collection/CollectionDashboard.tsx` (835 lines)

### Task 4: Add Keyboard Navigation and ARIA to Interactive Components (~8h)

**Problem:** `CardDeck.tsx`, `GameTableLayout.tsx`, `DeckStack.tsx` have `<div onClick>` without `role`, `tabIndex`, or keyboard handlers.

For each affected component:

- [ ] Add `role="button"` to clickable divs
- [ ] Add `tabIndex={0}`
- [ ] Add `onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleClick(); } }}`
- [ ] Apply same pattern from `AccessibleFormInput.tsx` to all non-input form fields
- [ ] Add missing `<label htmlFor=...>` associations to all form inputs (auth forms, admin config forms)
- [ ] Add root `apps/web/src/app/error.tsx` for unhandled error fallback

### Task 5: Migrate Raw `<img>` to `next/image` (~4h)

**Problem:** 18 files use raw `<img>` tags, missing lazy loading, format optimization, and LCP improvements.

Priority order (highest traffic):
- [ ] `components/dashboard/AddToLibraryModal.tsx`
- [ ] `components/dashboard/recent-games-section.tsx`
- [ ] `components/session/LiveSessionContextBar.tsx`
- [ ] `components/badges/BadgeGrid.tsx`
- [ ] `components/badges/BadgeDetailSheet.tsx`
- [ ] `components/ui/data-display/meeple-card/CartaEstesa.tsx`
- [ ] `components/ui/data-display/deck-stack/DeckStack.tsx`
- [ ] Remaining 11 files

For each: `import Image from 'next/image'` → replace `<img src=... alt=...>` with `<Image src=... alt=... width=... height=... />`. Add `fill` prop where dimensions are unknown.

### Task 6: Fix Public Setters on Domain Models (~4h)

**Problem:** `AgentMemory/Domain/Models/PlayerGameStats.cs`, `MemoryNote.cs`, `HouseRule.cs`, `SessionTracking/Domain/Entities/SessionParticipant` have all-public setters.

- [ ] For each class: make all non-Id properties `private set`
- [ ] Add factory method `Create(...)` accepting the required fields
- [ ] Update all construction sites to use the factory method (search with grep)
- [ ] Build and fix errors

### Task 7: Consolidate `store/` vs `stores/` Frontend Directories (~2h)

**Problem:** `apps/web/src/store/` and `apps/web/src/stores/` both exist with different patterns.

- [ ] Move all files from `src/store/*/` into `src/stores/` following flat-file naming
- [ ] Update all imports (`@/store/...` → `@/stores/...`)
- [ ] Delete empty `src/store/` directory
- [ ] Run `pnpm typecheck` to verify no broken imports

---

## P3 Tasks (Lower Priority)

### Task 8: Remove Overly Broad Global Suppressions (~8h)

**Files:** `apps/api/src/Api/GlobalSuppressions.cs`

- [ ] Remove `CA1031` (catch general exception) suppression globally — fix each catch site individually to catch specific exception types
- [ ] Remove `CA2016` (forward CancellationToken) suppression — find async methods missing CT propagation and add
- [ ] Remove `S1172` (unused parameters) from NoWarn in Api.csproj — fix or justify each

### Task 9: Optimize Redis Configuration (~1h)

**File:** `infra/redis/redis.conf`

Add optimizations:
```
tcp-keepalive 300
hz 100
lazyfree-lazy-eviction yes
lazyfree-lazy-expire yes
lazyfree-lazy-server-del yes
save ""
```

Remove default `appendonly yes` since AOF is explicitly enabled. Disable RDB snapshots to reduce I/O.

### Task 10: Fix HybridCache Compression Flag (~1h)

**File:** `apps/api/src/Api/Services/HybridCacheService.cs`

Remove `HybridCacheEntryFlags.DisableCompression` from the global default. Add it only to specific entries for AI-generated content (which is already text-compressed by the LLM context).

### Task 11: Add torch.no_grad() and Model Warmup to Embedding Service (~1h)

**File:** `apps/embedding-service/main.py`

```python
import torch

# Wrap inference in no_grad context:
with torch.no_grad():
    embeddings = await loop.run_in_executor(None, encode_fn)
```

Add warmup endpoint call on startup (same pattern as reranker-service).

### Task 12: Fix CSP connect-src for Production (~1h)

**File:** `apps/web/next.config.js` line 543

Replace hardcoded `http://localhost:8080` with an environment variable:
```javascript
const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:8080';
// In CSP string:
`connect-src 'self' ${apiBaseUrl}`
```

Ensure `NEXT_PUBLIC_API_BASE_URL` is set in staging/production Docker environment.

---

## Priority Order Summary

| Task | Effort | Impact |
|------|--------|--------|
| 1 — KnowledgeBase decoupling | 16h | Architecture health |
| 2 — CQRS violations (repos) | 8h | Architecture compliance |
| 3 — Split oversized components | 8h | Maintainability |
| 4 — Keyboard/ARIA accessibility | 8h | Accessibility compliance |
| 5 — next/image migration | 4h | Frontend performance |
| 6 — Public setters fix | 4h | DDD compliance |
| 7 — Store consolidation | 2h | Developer experience |
| 8 — Remove global suppressions | 8h | Code quality |
| 9 — Redis optimization | 1h | Infrastructure perf |
| 10 — HybridCache compression | 1h | Cache efficiency |
| 11 — torch.no_grad + warmup | 1h | AI service perf |
| 12 — CSP production fix | 1h | Security config |
