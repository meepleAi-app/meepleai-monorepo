# Frontend Code Improvements — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Systematic frontend refactoring to reduce tech debt: split monolithic files, consolidate duplicate directories, standardize naming, improve type safety.

**Architecture:** Vertical-by-area approach — 4 phases (Admin, Core UI, Structure, Quality), each producing independent PRs to `frontend-dev`. Big bang import updates per PR; no re-export bridges.

**Tech Stack:** Next.js 16, React 19, TypeScript 5.9, Zustand, React Query, Vitest, Playwright

**Spec:** `docs/superpowers/specs/2026-03-17-frontend-code-improvements-design.md`

---

## Pre-requisites

- [ ] **Step 1: Create feature branch from frontend-dev**

```bash
cd apps/web
git checkout frontend-dev && git pull
git checkout -b feature/frontend-improvements
git config branch.feature/frontend-improvements.parent frontend-dev
```

- [ ] **Step 2: Verify clean state**

```bash
pnpm typecheck && pnpm lint && pnpm test --run
```

Expected: All pass. If not, fix pre-existing issues first.

---

## Phase 1: Admin Area (PR 1 + PR 2)

### Task 1: Analyze adminClient method grouping

Before splitting, confirm the method-to-subclient mapping by reading the actual file.

**Files:**
- Read: `src/lib/api/clients/adminClient.ts`
- Read: `src/lib/api/schemas/admin.schemas.ts`
- Read: `src/lib/api/index.ts` (line ~403 for admin registration)

- [ ] **Step 1: Read `adminClient.ts` and map each method to its target sub-client**

Use this grouping (125+ methods total, based on exploration):

| Sub-client | Methods (summary) | Est. lines |
|------------|-------------------|------------|
| `adminUsersClient.ts` | User CRUD, suspend/unsuspend, resetPassword, impersonate, sessions, badges, activity, API keys, data export/import | ~600 |
| `adminContentClient.ts` | Prompt templates, versions, audit logs, shared games, game publication, mechanic extractor, entity links, email templates/queue | ~800 |
| `adminAiClient.ts` | AI models, LLM config, tier/strategy, RAG executions, A/B testing, agent typologies, KB/vector/Qdrant ops, PDF ops, embedding, processing queue | ~1000 |
| `adminAnalyticsClient.ts` | Stats, overview stats, analytics, chat/PDF/model analytics, recent activity, usage stats/timeline/costs, free quota, recent requests, top consumers, token balance/consumption/tier, cost tracking, cost scenarios, resource forecasting, ledger | ~700 |
| `adminSystemClient.ts` | Workflow templates, N8n config, infrastructure, cache, database metrics, emergency overrides, batch jobs, queue ops | ~500 |
| `adminMonitorClient.ts` | Service dashboard, container logs, docker containers, metrics time series, accessibility/performance/resource metrics, scheduled reports, OpenRouter status | ~400 |

- [ ] **Step 2: Confirm mapping covers ALL methods — no method left behind**

Run: `grep -c "async " src/lib/api/clients/adminClient.ts`
Expected: Count matches sum of all sub-client methods.

### Task 2: Create admin sub-client files

**Files:**
- Create: `src/lib/api/clients/admin/adminUsersClient.ts`
- Create: `src/lib/api/clients/admin/adminContentClient.ts`
- Create: `src/lib/api/clients/admin/adminAiClient.ts`
- Create: `src/lib/api/clients/admin/adminAnalyticsClient.ts`
- Create: `src/lib/api/clients/admin/adminSystemClient.ts`
- Create: `src/lib/api/clients/admin/adminMonitorClient.ts`
- Create: `src/lib/api/clients/admin/index.ts`

- [ ] **Step 1: Create `src/lib/api/clients/admin/` directory**

- [ ] **Step 2: Create `adminUsersClient.ts`**

Extract ALL user-related methods from `adminClient.ts`. Each sub-client follows this pattern:

```typescript
import type { HttpClient } from '../../core/httpClient';
// import only the schemas this sub-client needs

export function createAdminUsersClient(http: HttpClient) {
  return {
    // paste all user methods here
    getAllUsers: async (params?: {...}) => { ... },
    getUsers: async (params?: {...}) => { ... },
    createUser: async (request: CreateUserRequest) => { ... },
    // ... all user methods
  };
}

export type AdminUsersClient = ReturnType<typeof createAdminUsersClient>;
```

- [ ] **Step 3: Create `adminContentClient.ts`** — same pattern, content methods
- [ ] **Step 4: Create `adminAiClient.ts`** — same pattern, AI/KB methods
- [ ] **Step 5: Create `adminAnalyticsClient.ts`** — same pattern, analytics/usage methods
- [ ] **Step 6: Create `adminSystemClient.ts`** — same pattern, system/infra methods
- [ ] **Step 7: Create `adminMonitorClient.ts`** — same pattern, monitoring methods

- [ ] **Step 8: Create barrel `admin/index.ts`**

```typescript
export { createAdminUsersClient, type AdminUsersClient } from './adminUsersClient';
export { createAdminContentClient, type AdminContentClient } from './adminContentClient';
export { createAdminAiClient, type AdminAiClient } from './adminAiClient';
export { createAdminAnalyticsClient, type AdminAnalyticsClient } from './adminAnalyticsClient';
export { createAdminSystemClient, type AdminSystemClient } from './adminSystemClient';
export { createAdminMonitorClient, type AdminMonitorClient } from './adminMonitorClient';
```

- [ ] **Step 9: Run typecheck to verify all imports resolve**

```bash
pnpm typecheck
```

### Task 3: Replace adminClient.ts with factory

**Files:**
- Modify: `src/lib/api/clients/adminClient.ts` (replace 3878 lines with ~50)

- [ ] **Step 1: Replace `adminClient.ts` with factory composition**

```typescript
import type { HttpClient } from '../core/httpClient';
import {
  createAdminUsersClient,
  createAdminContentClient,
  createAdminAiClient,
  createAdminAnalyticsClient,
  createAdminSystemClient,
  createAdminMonitorClient,
} from './admin';

export function createAdminClient({ httpClient }: { httpClient: HttpClient }) {
  const http = httpClient;
  return {
    ...createAdminUsersClient(http),
    ...createAdminContentClient(http),
    ...createAdminAiClient(http),
    ...createAdminAnalyticsClient(http),
    ...createAdminSystemClient(http),
    ...createAdminMonitorClient(http),
  };
}

export type AdminClient = ReturnType<typeof createAdminClient>;
```

Note: Match the exact signature expected by `src/lib/api/index.ts` line ~403: `admin: createAdminClient({ httpClient })`. The factory takes `{ httpClient }` destructured.

- [ ] **Step 2: Verify NO consumer imports changed** — all consumers use `api.admin.methodName()` which still works.

```bash
pnpm typecheck
```

- [ ] **Step 3: Run tests**

```bash
pnpm test --run -- adminClient
```

Expected: All existing admin tests pass (they test through the factory).

- [ ] **Step 4: Commit**

```bash
git add src/lib/api/clients/admin/ src/lib/api/clients/adminClient.ts
git commit -m "refactor(admin): split adminClient.ts into 6 domain sub-clients"
```

### Task 4: Split admin.schemas.ts

**Files:**
- Read: `src/lib/api/schemas/admin.schemas.ts` (1014 lines)
- Create: `src/lib/api/schemas/admin/admin-users.schemas.ts`
- Create: `src/lib/api/schemas/admin/admin-content.schemas.ts`
- Create: `src/lib/api/schemas/admin/admin-ai.schemas.ts`
- Create: `src/lib/api/schemas/admin/admin-analytics.schemas.ts`
- Create: `src/lib/api/schemas/admin/admin-system.schemas.ts`
- Create: `src/lib/api/schemas/admin/admin-monitor.schemas.ts`
- Create: `src/lib/api/schemas/admin/index.ts`
- Modify: `src/lib/api/schemas/admin.schemas.ts` (becomes barrel re-export)

- [ ] **Step 1: Read `admin.schemas.ts` and group exports by domain**

Use same grouping as sub-clients. Each schema file exports the Zod schemas + inferred types for its domain.

- [ ] **Step 2: Create `src/lib/api/schemas/admin/` directory with 6 schema files**

Each file contains only the schemas relevant to its domain. Shared/base schemas (if any) stay in a `admin-shared.schemas.ts`.

- [ ] **Step 3: Replace `admin.schemas.ts` with barrel re-export**

```typescript
// Barrel re-export for backward compatibility
export * from './admin/admin-users.schemas';
export * from './admin/admin-content.schemas';
export * from './admin/admin-ai.schemas';
export * from './admin/admin-analytics.schemas';
export * from './admin/admin-system.schemas';
export * from './admin/admin-monitor.schemas';
```

- [ ] **Step 4: Verify no consumer changes needed**

```bash
pnpm typecheck
```

The ~10 page/component consumers import from `admin.schemas` which now re-exports everything.

- [ ] **Step 5: Commit**

```bash
git add src/lib/api/schemas/admin/ src/lib/api/schemas/admin.schemas.ts
git commit -m "refactor(admin): split admin.schemas.ts into 6 domain schema files"
```

### Task 5: Split admin test files

**Files:**
- Read: `src/lib/api/clients/__tests__/adminClient.test.ts` (715 lines)
- Create: `src/lib/api/clients/__tests__/admin/adminUsersClient.test.ts`
- Create: `src/lib/api/clients/__tests__/admin/adminContentClient.test.ts`
- Create: `src/lib/api/clients/__tests__/admin/adminAiClient.test.ts`
- Create: `src/lib/api/clients/__tests__/admin/adminAnalyticsClient.test.ts`
- Create: `src/lib/api/clients/__tests__/admin/adminSystemClient.test.ts`
- Create: `src/lib/api/clients/__tests__/admin/adminMonitorClient.test.ts`

- [ ] **Step 1: Read existing test file and understand test structure**

Tests likely call `createAdminClient()` and test method responses. Split tests to match sub-client grouping.

- [ ] **Step 2: Create sub-client test files**

Each test file imports its specific `createAdmin*Client` function and tests only those methods. Also move the existing test files:
- `src/lib/api/clients/__tests__/adminClient.reports.test.ts` → analytics sub-client tests
- `src/lib/api/clients/__tests__/adminClient.entity-links.test.ts` → content sub-client tests
- `__tests__/admin/knowledge-base/adminClient.kb-methods.test.ts` (NOTE: this file is at project root `__tests__/`, NOT under `src/`) → AI sub-client tests

- [ ] **Step 3: Delete or empty old test files** (keep if some tests test the factory composition itself)

- [ ] **Step 4: Run all admin tests**

```bash
pnpm test --run -- admin
```

Expected: Same test count passes.

- [ ] **Step 5: Commit**

```bash
git add src/lib/api/clients/__tests__/
git commit -m "refactor(admin): split admin test files to match sub-client structure"
```

### Task 6: Reorganize rag-dashboard root files (PR 2)

**Files:**
- Read: `src/components/rag-dashboard/` (list all root files and subdirs)
- Read: `src/components/rag-dashboard/config/index.ts`
- Read: `src/components/rag-dashboard/metrics/index.ts`
- Move: 6 root-level large files to appropriate subdirs

- [ ] **Step 1: Audit existing subdirectory contents**

```bash
ls src/components/rag-dashboard/config/
ls src/components/rag-dashboard/metrics/
```

Verify that `config/` and `metrics/` are appropriate destinations for the files in the spec.

- [ ] **Step 2: Move configuration files to `config/`**

```bash
git mv src/components/rag-dashboard/RagConfigurationForm.tsx src/components/rag-dashboard/config/
git mv src/components/rag-dashboard/AgentRoleConfigurator.tsx src/components/rag-dashboard/config/
```

- [ ] **Step 3: Move metrics files to `metrics/`**

```bash
git mv src/components/rag-dashboard/PerformanceMetricsTable.tsx src/components/rag-dashboard/metrics/
git mv src/components/rag-dashboard/TokenFlowVisualizer.tsx src/components/rag-dashboard/metrics/
```

- [ ] **Step 4: Create `reference/` and move reference files**

```bash
mkdir src/components/rag-dashboard/reference
git mv src/components/rag-dashboard/ParameterGuide.tsx src/components/rag-dashboard/reference/
git mv src/components/rag-dashboard/LayerDeepDocs.tsx src/components/rag-dashboard/reference/
```

Create `src/components/rag-dashboard/reference/index.ts`:
```typescript
export { ParameterGuide } from './ParameterGuide';
export { LayerDeepDocs } from './LayerDeepDocs';
```

- [ ] **Step 5: Update barrel exports in `config/index.ts` and `metrics/index.ts`**

Add the new files to existing barrel exports.

- [ ] **Step 6: Find and update ALL consumer imports**

```bash
grep -rn "from.*rag-dashboard/RagConfigurationForm" src/
grep -rn "from.*rag-dashboard/AgentRoleConfigurator" src/
grep -rn "from.*rag-dashboard/PerformanceMetricsTable" src/
grep -rn "from.*rag-dashboard/TokenFlowVisualizer" src/
grep -rn "from.*rag-dashboard/ParameterGuide" src/
grep -rn "from.*rag-dashboard/LayerDeepDocs" src/
```

Update each import to new path (e.g., `rag-dashboard/config/RagConfigurationForm`).

- [ ] **Step 7: Verify**

```bash
pnpm typecheck && pnpm test --run -- rag-dashboard
```

- [ ] **Step 8: Commit and create PR 2**

```bash
git add src/components/rag-dashboard/
git commit -m "refactor(rag-dashboard): reorganize root files into config/, metrics/, reference/"
```

### Task 7: Create PR 1 (admin split) and PR 2 (rag-dashboard)

- [ ] **Step 1: Create PR 1 for admin changes**

```bash
git push -u origin feature/frontend-improvements
gh pr create --base frontend-dev --title "refactor(admin): split adminClient and schemas into domain sub-modules" --body "$(cat <<'EOF'
## Summary
- Split `adminClient.ts` (3878 lines) into 6 domain sub-clients (~400-1000 lines each)
- Split `admin.schemas.ts` (1014 lines) into 6 domain schema files
- Factory composition in `adminClient.ts` preserves backward compatibility
- All 46+ consumer files unchanged — same `api.admin.method()` API

## Test plan
- [ ] `pnpm typecheck` passes
- [ ] `pnpm test --run -- admin` — all admin tests pass
- [ ] Spot-check 3+ admin pages in browser

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Note: PR 2 for rag-dashboard can be a separate branch or sequential commit on same branch — decide at implementation time based on PR review cadence.

---

## Phase 2: Core UI Decomposition (PR 3 + PR 4)

### Task 8: Decompose EntityExtraMeepleCard by entity type

**Critical discovery**: The component is organized by **entity type** (Game, Player, Collection, Agent, Chat, Kb), NOT by visual variant. Each entity type is a self-contained component (120-488 lines). The decomposition extracts each entity variant to its own file.

**Files:**
- Read: `src/components/ui/data-display/extra-meeple-card/EntityExtraMeepleCard.tsx` (2052 lines)
- Read: `src/components/ui/data-display/extra-meeple-card/index.ts`
- Create: `src/components/ui/data-display/extra-meeple-card/entities/GameExtraMeepleCard.tsx`
- Create: `src/components/ui/data-display/extra-meeple-card/entities/PlayerExtraMeepleCard.tsx`
- Create: `src/components/ui/data-display/extra-meeple-card/entities/CollectionExtraMeepleCard.tsx`
- Create: `src/components/ui/data-display/extra-meeple-card/entities/AgentExtraMeepleCard.tsx`
- Create: `src/components/ui/data-display/extra-meeple-card/entities/ChatExtraMeepleCard.tsx`
- Create: `src/components/ui/data-display/extra-meeple-card/entities/KbExtraMeepleCard.tsx`
- Create: `src/components/ui/data-display/extra-meeple-card/shared.tsx`
- Modify: `src/components/ui/data-display/extra-meeple-card/EntityExtraMeepleCard.tsx` (becomes ~100 line router)

- [ ] **Step 1: Read EntityExtraMeepleCard.tsx fully to understand structure**

Key sections from exploration:
- Lines 1–126: Shared infrastructure (ENTITY_COLORS, EntityHeader, EntityLoadingState, EntityErrorState)
- Lines 157–478: GameExtraMeepleCard (322 lines)
- Lines 489–646: PlayerExtraMeepleCard (158 lines)
- Lines 657–775: CollectionExtraMeepleCard (119 lines)
- Lines 786–1164: AgentExtraMeepleCard (379 lines)
- Lines 1175–1520: ChatExtraMeepleCard (346 lines)
- Lines 1565–2052: KbExtraMeepleCard (488 lines)

- [ ] **Step 2: Create `shared.tsx`** with common components

Extract lines 1–126:
```typescript
// ENTITY_COLORS constant
// EntityHeader component
// EntityLoadingState component
// EntityErrorState component
```

- [ ] **Step 3: Create `entities/` directory and extract each entity variant**

For each entity (Game, Player, Collection, Agent, Chat, Kb):
1. Copy the component function to its own file
2. Import shared components from `../shared`
3. Import types from `../types`
4. Export the component

- [ ] **Step 4: Reduce `EntityExtraMeepleCard.tsx` to a thin router**

```typescript
export { GameExtraMeepleCard } from './entities/GameExtraMeepleCard';
export { PlayerExtraMeepleCard } from './entities/PlayerExtraMeepleCard';
export { CollectionExtraMeepleCard } from './entities/CollectionExtraMeepleCard';
export { AgentExtraMeepleCard } from './entities/AgentExtraMeepleCard';
export { ChatExtraMeepleCard } from './entities/ChatExtraMeepleCard';
export { KbExtraMeepleCard } from './entities/KbExtraMeepleCard';
```

Or if the current file has a single default export that routes based on entity type, keep that routing logic (~100 lines).

- [ ] **Step 5: Update `index.ts` barrel if needed**

- [ ] **Step 6: Verify**

```bash
pnpm typecheck
pnpm test --run -- extra-meeple-card
```

- [ ] **Step 7: Commit**

```bash
git add src/components/ui/data-display/extra-meeple-card/
git commit -m "refactor(ui): decompose EntityExtraMeepleCard into entity-specific files"
```

### Task 9: Decompose ChatThreadView

**Files:**
- Read: `src/components/chat-unified/ChatThreadView.tsx` (852 lines)
- Create: `src/components/chat-unified/ChatMessageList.tsx`
- Create: `src/components/chat-unified/ChatInputArea.tsx`
- Create: `src/components/chat-unified/ChatInfoPanel.tsx`
- Create: `src/components/chat-unified/AgentSwitchDialog.tsx`
- Modify: `src/components/chat-unified/ChatThreadView.tsx` (~300 lines remaining)

Key structure from exploration:
- Lines 46–72: Types (ChatMessage, ThreadData, ChatThreadViewProps)
- Lines 85–396: State + hooks + handlers
- Lines 425–438: Header
- Lines 441–488: Agent selector + switch dialog + error banner
- Lines 491–795: Left panel — messages + input area
- Lines 813–848: Right panel — info panel (desktop only)

- [ ] **Step 1: Read ChatThreadView.tsx fully**

- [ ] **Step 2: Extract `ChatMessageList.tsx`**

Extract the messages rendering section (lines ~540–704):
- User message bubbles (amber)
- Assistant message bubbles (white/70 backdrop-blur)
- TtsSpeakerButton, RuleSourceCard, ResponseMetaBadge, TechnicalDetailsPanel inline
- Streaming response bubble

Props: `messages`, `streamState`, `isEditor`, `onSpeakMessage`

- [ ] **Step 3: Extract `ChatInputArea.tsx`**

Extract the input section (lines ~723–794):
- RagEnhancementsBadge
- Textarea + VoiceMicButton + VoiceSettingsPopover + Send button

Props: `inputValue`, `onInputChange`, `onSend`, `onKeyDown`, `isSending`, `isLoading`, `voiceState`, `onVoiceTap`

- [ ] **Step 4: Extract `ChatInfoPanel.tsx`**

Extract right panel (lines ~813–848):
- Game info section
- Citations display
- Suggested questions

Props: `game`, `citations`, `suggestedQuestions`, `onQuestionClick`

- [ ] **Step 5: Extract `AgentSwitchDialog.tsx`**

Extract agent switch confirmation dialog (lines ~451–478).

Props: `open`, `onConfirm`, `onCancel`, `pendingAgentName`

- [ ] **Step 6: Update ChatThreadView.tsx to import extracted components**

Keep state management, hooks, and handlers in ChatThreadView. Render extracted components with props.

- [ ] **Step 7: Verify**

```bash
pnpm typecheck
pnpm test --run -- chat
```

- [ ] **Step 8: Commit**

```bash
git add src/components/chat-unified/
git commit -m "refactor(chat): decompose ChatThreadView into focused sub-components"
```

### Task 10: Decompose game-carousel

**Files:**
- Create: `src/components/ui/data-display/game-carousel/` directory
- Create: `src/components/ui/data-display/game-carousel/GameCarousel.tsx`
- Create: `src/components/ui/data-display/game-carousel/GameCarouselSkeleton.tsx`
- Create: `src/components/ui/data-display/game-carousel/types.ts`
- Create: `src/components/ui/data-display/game-carousel/constants.ts`
- Create: `src/components/ui/data-display/game-carousel/math.ts`
- Create: `src/components/ui/data-display/game-carousel/hooks/useSwipe.ts`
- Create: `src/components/ui/data-display/game-carousel/hooks/useKeyboardNavigation.ts`
- Create: `src/components/ui/data-display/game-carousel/components/NavButton.tsx`
- Create: `src/components/ui/data-display/game-carousel/components/DotsIndicator.tsx`
- Create: `src/components/ui/data-display/game-carousel/components/AutoPlayButton.tsx`
- Create: `src/components/ui/data-display/game-carousel/components/SortDropdown.tsx`
- Create: `src/components/ui/data-display/game-carousel/index.ts`
- Delete: `src/components/ui/data-display/game-carousel.tsx` (old single file)

Key structure from exploration:
- Lines 50–127: Types + constants
- Lines 133–186: useSwipe + useKeyboardNavigation hooks
- Lines 192–465: NavButton, DotsIndicator, AutoPlayButton, SortDropdown sub-components
- Lines 471–525: CardPosition interface + calculateCardPositions utility
- Lines 531–866: GameCarousel main (React.memo)
- Lines 872–912: GameCarouselSkeleton

- [ ] **Step 1: Create `game-carousel/` directory**

- [ ] **Step 2: Extract `types.ts`** — CarouselGame, CarouselSortValue, CarouselSortOption, GameCarouselProps (lines 50–127)

- [ ] **Step 3: Extract `constants.ts`** — CAROUSEL_SORT_OPTIONS (lines 82–87)

- [ ] **Step 4: Extract `math.ts`** — CardPosition interface + calculateCardPositions (lines 471–525)

- [ ] **Step 5: Extract `hooks/useSwipe.ts`** (lines 136–160) and `hooks/useKeyboardNavigation.ts` (lines 165–186)

- [ ] **Step 6: Extract sub-components** to `components/`:
- `NavButton.tsx` (lines 195–231)
- `DotsIndicator.tsx` (lines 236–305)
- `AutoPlayButton.tsx` (lines 310–335)
- `SortDropdown.tsx` (lines 341–465)

- [ ] **Step 7: Create `GameCarousel.tsx`** with main component (lines 531–866), importing from extracted files

- [ ] **Step 8: Create `GameCarouselSkeleton.tsx`** (lines 872–912)

- [ ] **Step 9: Create `index.ts` barrel**

```typescript
export { GameCarousel } from './GameCarousel';
export { GameCarouselSkeleton } from './GameCarouselSkeleton';
export type { GameCarouselProps, CarouselGame, CarouselSortValue, CarouselSortOption } from './types';
export { CAROUSEL_SORT_OPTIONS } from './constants';
```

- [ ] **Step 10: Update ALL imports of `game-carousel`**

```bash
grep -rn "from.*data-display/game-carousel" src/
```

Change from `@/components/ui/data-display/game-carousel` to `@/components/ui/data-display/game-carousel` (same path if index.ts resolves) or update to explicit `/index`.

- [ ] **Step 11: Delete old `game-carousel.tsx` single file**

- [ ] **Step 12: Update showcase story if it references the old path**

Check: `src/components/showcase/stories/game-carousel*`

- [ ] **Step 13: Verify**

```bash
pnpm typecheck
pnpm test --run -- game-carousel
```

- [ ] **Step 14: Commit**

```bash
git add src/components/ui/data-display/game-carousel/
git rm src/components/ui/data-display/game-carousel.tsx
git commit -m "refactor(ui): decompose game-carousel into modular directory structure"
```

### Task 11: Create PR 3 + PR 4

- [ ] **Step 1: Push and create PR 3 (EntityExtraMeepleCard)**
- [ ] **Step 2: Create PR 4 (ChatThreadView + game-carousel)**

Both target `frontend-dev`.

---

## Phase 3: Structure & Naming (PR 5 + PR 6)

### Task 12: Consolidate dashboard directories

**Files:**
- Read: `src/components/dashboard/` (all files)
- Read: `src/components/dashboard-v2/` (all files)
- Modify: consumers of dashboard/ components

- [ ] **Step 1: List all files in both directories and check imports**

```bash
grep -rn "from.*components/dashboard/" src/ --include="*.tsx" --include="*.ts" | grep -v dashboard-v2 | grep -v __tests__
```

- [ ] **Step 2: Identify dashboard/ files that have NO equivalent in dashboard-v2/**

Key unique files from exploration: `DashboardEngine.ts`, `DashboardEngineProvider.tsx`, `DashboardRenderer.tsx`, `SessionPanel.tsx`, `SessionPanelCollapsed.tsx`, `zones/` subdirectory. These are actively used by UnifiedShell.

- [ ] **Step 3: Move unique files from `dashboard/` to `dashboard-v2/`**

```bash
git mv src/components/dashboard/DashboardEngine.ts src/components/dashboard-v2/
git mv src/components/dashboard/DashboardEngineProvider.tsx src/components/dashboard-v2/
# ... etc for each unique file
git mv src/components/dashboard/zones/ src/components/dashboard-v2/zones/
```

- [ ] **Step 4: For files that exist in BOTH, keep dashboard-v2/ version (newer)**

- [ ] **Step 5: Update all imports from `components/dashboard/` to `components/dashboard-v2/`**

- [ ] **Step 6: Delete `components/dashboard/`**

- [ ] **Step 7: Verify**

```bash
pnpm typecheck && pnpm test --run
```

- [ ] **Step 8: Commit**

```bash
git commit -m "refactor: consolidate dashboard/ into dashboard-v2/"
```

### Task 13: Consolidate game-night directories

**Files:**
- Read: `src/components/game-night/` (~34 files)
- Read: `src/components/game-nights/` (~16 files)

**Note**: game-night/ = runtime session, game-nights/ = planning. These serve different purposes but the spec says to consolidate.

- [ ] **Step 1: Audit for name conflicts between directories**

From exploration: No direct name conflicts. `game-night/SessionHeader.tsx` conflicts with `session/SessionHeader.tsx` (different module) — rename to `GameNightSessionHeader.tsx` during this step.

- [ ] **Step 2: Move all files from `game-nights/` into `game-night/planning/`**

```bash
mkdir src/components/game-night/planning
git mv src/components/game-nights/*.tsx src/components/game-night/planning/
git mv src/components/game-nights/*.ts src/components/game-night/planning/
```

This preserves semantic separation within a single directory.

- [ ] **Step 3: Update all imports from `components/game-nights/` to `components/game-night/planning/`**

- [ ] **Step 4: Delete `components/game-nights/`**

- [ ] **Step 5: Verify and commit**

```bash
pnpm typecheck && pnpm test --run
git commit -m "refactor: consolidate game-nights/ into game-night/planning/"
```

### Task 14: Consolidate sessions directories

**Files:**
- Read: `src/components/sessions/` (~21 files incl. `live/` subdir)
- Read: `src/components/session/` (~53 files)

- [ ] **Step 1: Audit for name conflicts**

Known conflicts from exploration:
- `session/Scoreboard.tsx` vs `sessions/live/ScoreBoard.tsx` (casing difference)
- `session/InviteSession.tsx` vs `sessions/live/InviteModal.tsx` (different names, similar purpose)

- [ ] **Step 2: Determine if conflicting files are duplicates or distinct**

Read both files. If duplicates, keep the one with more features. If distinct, rename one.

- [ ] **Step 3: Move files from `sessions/` to `session/`**

```bash
# Root files
git mv src/components/sessions/MeepleSessionCard.tsx src/components/session/
git mv src/components/sessions/SessionQuotaBar.tsx src/components/session/

# Live subdirectory
git mv src/components/sessions/live/ src/components/session/live/
```

Rename conflicting files during move if needed.

- [ ] **Step 4: Update all imports**

- [ ] **Step 5: Delete `components/sessions/`**

- [ ] **Step 6: Verify and commit**

```bash
pnpm typecheck && pnpm test --run
git commit -m "refactor: consolidate sessions/ into session/"
```

### Task 15: Rename kebab-case hooks (PR 6)

**Files:**
- Rename: 8 files in `src/hooks/` (not 25 — actual count from exploration)

Actual kebab-case files:
```
use-app-mode.ts
use-bottom-nav-actions.ts
use-bulk-collection-actions.ts
use-collection-actions.ts
use-contextual-actions.ts
use-entity-actions.ts
use-media-query.ts
use-toast.ts
```

- [ ] **Step 1: Rename each file**

```bash
cd src/hooks
git mv use-app-mode.ts useAppMode.ts
git mv use-bottom-nav-actions.ts useBottomNavActions.ts
git mv use-bulk-collection-actions.ts useBulkCollectionActions.ts
git mv use-collection-actions.ts useCollectionActions.ts
git mv use-contextual-actions.ts useContextualActions.ts
git mv use-entity-actions.ts useEntityActions.ts
git mv use-media-query.ts useMediaQuery.ts
git mv use-toast.ts useToast.ts
```

- [ ] **Step 1b: Also rename kebab-case hooks in subdirectories**

```bash
git mv src/hooks/admin/use-agent-definitions.ts src/hooks/admin/useAgentDefinitions.ts
git mv src/hooks/agent/use-orchestrator.ts src/hooks/agent/useOrchestrator.ts
```

- [ ] **Step 2: Update ALL imports across codebase**

For each renamed file, find and update imports:
```bash
grep -rn "@/hooks/use-toast" src/
grep -rn "@/hooks/use-media-query" src/
# ... etc for all 8
```

**Special attention**: `use-toast.ts` has ~37 importers. `use-media-query.ts` creates a duplicate name with `src/lib/hooks/useMediaQuery.ts` — see Task 16.

- [ ] **Step 3: Update corresponding test files**

Check for test files that import the old paths.

- [ ] **Step 4: Verify E2E tests still work with renamed toast hook**

```bash
pnpm test --run -- toast
pnpm test:e2e -- --grep toast
```

- [ ] **Step 5: Commit**

```bash
git add src/hooks/
git commit -m "refactor(hooks): rename kebab-case hooks to camelCase convention"
```

### Task 16: Consolidate useMediaQuery + rename lib/hooks

**Files:**
- Read: `src/hooks/useMediaQuery.ts` (renamed in Task 15, 65 lines, SSR-safe)
- Read: `src/lib/hooks/useMediaQuery.ts` (32 lines, NOT SSR-safe)
- Delete: `src/lib/hooks/useMediaQuery.ts`
- Modify: consumers of `src/lib/hooks/useMediaQuery` (~3 files: FlipCard.tsx, etc.)
- Rename: `src/lib/hooks/` → `src/lib/domain-hooks/`

- [ ] **Step 1: Delete inferior `src/lib/hooks/useMediaQuery.ts`**

The `src/hooks/useMediaQuery.ts` version is superior (SSR-safe, legacy browser support, 'use client' directive).

- [ ] **Step 2: Update the ~3 consumers of `src/lib/hooks/useMediaQuery`**

Change their imports to `@/hooks/useMediaQuery`.

- [ ] **Step 3: Rename `src/lib/hooks/` → `src/lib/domain-hooks/`**

```bash
git mv src/lib/hooks/ src/lib/domain-hooks/
```

- [ ] **Step 4: Update all ~32 consumers of `src/lib/hooks/`**

```bash
grep -rn "@/lib/hooks/" src/ | grep -v node_modules
```

Change all `@/lib/hooks/` → `@/lib/domain-hooks/`.

- [ ] **Step 5: Also rename kebab-case files inside domain-hooks** (5 files found by exploration)

```bash
cd src/lib/domain-hooks
git mv use-bgg-rate-limit.ts useBggRateLimit.ts
git mv use-game-search.ts useGameSearch.ts
git mv use-play-records.ts usePlayRecords.ts
git mv use-session-scores.ts useSessionScores.ts
git mv use-signalr-session.ts useSignalRSession.ts
```

Update their imports too.

- [ ] **Step 6: Verify**

```bash
pnpm typecheck && pnpm test --run
```

- [ ] **Step 7: Commit**

```bash
git commit -m "refactor(hooks): consolidate useMediaQuery, rename lib/hooks to domain-hooks"
```

### Task 17: Create PR 5 + PR 6

- [ ] **Step 1: Create PR 5 for directory consolidation (Tasks 12-14)**
- [ ] **Step 2: Create PR 6 for hook renames (Tasks 15-16)**

---

## Phase 4: Quality Sweep (PR 7 + PR 8)

### Task 18: Fix `any` types in production code

**Files:**
- All non-test `.ts`/`.tsx` files under `src/`

**Note**: Exploration found ~18 explicit `: any` in production code (much less than the initial 1,299 estimate which included test files and other patterns). The actual work is smaller than spec estimated.

- [ ] **Step 1: Find all `any` in production code**

```bash
grep -rn ": any" src/ --include="*.ts" --include="*.tsx" | grep -v __tests__ | grep -v ".test." | grep -v ".spec." | grep -v node_modules
```

Also check for `as any`:
```bash
grep -rn "as any" src/ --include="*.ts" --include="*.tsx" | grep -v __tests__ | grep -v ".test." | grep -v ".spec." | grep -v node_modules
```

- [ ] **Step 2: Categorize each occurrence and apply the fix pattern from the spec**

| Pattern | Fix |
|---------|-----|
| `param: any` | Type with correct interface |
| `catch(e: any)` | `catch(e: unknown)` + type guard |
| `as any` | Remove or use proper type assertion |
| `Record<string, any>` | `Record<string, unknown>` |

- [ ] **Step 3: Fix each occurrence, one file at a time**

- [ ] **Step 4: Verify after each batch of fixes**

```bash
pnpm typecheck
```

- [ ] **Step 5: Commit**

```bash
git commit -m "refactor(types): replace any with proper types in production code"
```

### Task 19: Replace console.* with logger

**Files:**
- Read: `src/lib/logger.ts` (already exists with full API)
- Modify: ~327 files with console.* usage

Logger API (from exploration):
```typescript
import { logger } from '@/lib/logger';
logger.debug(message, context?);
logger.info(message, context?);
logger.warn(message, context?);
logger.error(message, error?, context?);
```

- [ ] **Step 1: Find all console.* in production code**

```bash
grep -rn "console\.\(log\|warn\|error\|info\|debug\)" src/ --include="*.ts" --include="*.tsx" | grep -v __tests__ | grep -v ".test." | grep -v ".spec." | grep -v node_modules | grep -v "logger.ts"
```

- [ ] **Step 2: Categorize exclusions**

DO NOT replace:
- `console.warn('[Toast]')` in `use-toast.ts` / `useToast.ts` — documented toast stub pattern
- Any console.* that is part of the logger implementation itself
- Console capture patterns used by Playwright E2E

- [ ] **Step 3: Replace console.log with appropriate logger level**

For each file:
1. Add `import { logger } from '@/lib/logger';` (adjust path)
2. Replace `console.log(...)` → `logger.debug(message)` — use single string argument only
3. Replace `console.warn(...)` → `logger.warn(message)` — single string argument only
4. Replace `console.error(...)` → `logger.error(message, error)` — accepts optional Error as 2nd param

**Important**: The logger's `debug`/`info`/`warn` methods accept an optional `ErrorContext` as 2nd parameter (not a plain object). Do NOT pass arbitrary objects as context. If the original console call includes data, interpolate it into the message string: `logger.debug(\`User ${userId} loaded\`)` instead of `logger.debug('User loaded', { userId })`.

Work in batches by directory (admin, chat, library, etc.) to keep changes reviewable.

- [ ] **Step 4: Verify after each batch**

```bash
pnpm typecheck
```

- [ ] **Step 5: Commit in batches**

```bash
git commit -m "refactor(logging): replace console.* with structured logger in admin components"
git commit -m "refactor(logging): replace console.* with structured logger in chat components"
# etc.
```

### Task 20: Tighten ESLint config

**Files:**
- Modify: `eslint.config.mjs`

- [ ] **Step 1: Count remaining warnings**

```bash
pnpm lint 2>&1 | tail -5
```

Note the current warning count.

- [ ] **Step 2: Reduce max-warnings**

Edit `eslint.config.mjs` and set max-warnings to current count (ratchet down):

```javascript
// In the config, find maxWarnings and reduce
// From 410 → actual current count after our fixes
```

- [ ] **Step 3: Consider enabling no-explicit-any as warning**

If the `any` count is now near zero in production, enable the rule:
```javascript
'@typescript-eslint/no-explicit-any': 'warn',
```

Only for production code — add override for test files to keep `off`.

- [ ] **Step 4: Verify**

```bash
pnpm lint
```

- [ ] **Step 5: Commit**

```bash
git commit -m "chore(lint): tighten ESLint max-warnings and enable no-explicit-any"
```

### Task 21: Create PR 7 + PR 8

- [ ] **Step 1: Create PR 7 for `any` type fixes (Task 18)**
- [ ] **Step 2: Create PR 8 for console.log replacement + ESLint (Tasks 19-20)**

---

## Validation Checklist (after ALL PRs merged)

- [ ] `pnpm typecheck` — zero errors
- [ ] `pnpm lint` — zero errors, warnings under new threshold
- [ ] `pnpm test --run` — all unit tests pass
- [ ] `pnpm test:e2e` — all E2E tests pass (especially toast-related)
- [ ] Manual check: Navigate admin dashboard, all 6 sections load
- [ ] Manual check: Chat thread loads and sends messages
- [ ] Manual check: Game carousel renders and navigates
- [ ] `adminClient.ts` is now ~50 lines (factory only)
- [ ] `EntityExtraMeepleCard.tsx` is now ~100-400 lines
- [ ] `ChatThreadView.tsx` is now ~300 lines
- [ ] No `components/dashboard/` directory exists (merged into dashboard-v2/)
- [ ] No `components/game-nights/` directory exists (merged into game-night/)
- [ ] No `components/sessions/` directory exists (merged into session/)
- [ ] No kebab-case hook files in `src/hooks/`
- [ ] `src/lib/domain-hooks/` exists (renamed from `src/lib/hooks/`)
