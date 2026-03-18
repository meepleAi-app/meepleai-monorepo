# Frontend Code Improvements — Design Spec

**Date**: 2026-03-17
**Scope**: Systematic frontend refactoring across 4 phases
**Approach**: Vertical by codebase area (admin → core UI → structure → quality)
**Breaking change strategy**: Big bang per PR (no re-export bridges)
**Estimated effort**: 4-5 weeks

---

## Phase 1: Admin Area

### 1A. Split `adminClient.ts` (3878 lines → 6 sub-clients + factory)

**Current file**: `src/lib/api/clients/adminClient.ts`

Split by admin navigation section:

| New file | Nav section | Estimated lines |
|----------|-------------|-----------------|
| `adminUsersClient.ts` | Users | ~500 |
| `adminContentClient.ts` | Content | ~700 |
| `adminAiClient.ts` | AI | ~900 |
| `adminAnalyticsClient.ts` | Analytics | ~400 |
| `adminSystemClient.ts` | System | ~400 |
| `adminMonitorClient.ts` | Monitor | ~500 |

**Factory pattern** — `adminClient.ts` becomes a lightweight re-export (~50 lines):

```typescript
// Note: must match existing call site signature: createAdminClient({ httpClient })
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
```

Existing consumers (`adminClient.methodName()`) continue to work unchanged. New code can import specific sub-clients for smaller bundles.

**Test split**: Each sub-client gets its own test file mirroring the source split.

### 1B. Split `admin.schemas.ts` (1014 lines → 6 schema files)

**Current file**: `src/lib/api/schemas/admin.schemas.ts`

**Note**: `adminClient.ts` does NOT import from `admin.schemas.ts` directly — it imports from individual schema files (`financial-ledger.schemas`, `mechanic-extractor.schemas`, etc.). The `admin.schemas.ts` file is consumed directly by ~10 page/component files. This split is motivated by **readability** of the 1014-line file, not by client-schema coupling.

Split by domain for readability:
- `admin-users.schemas.ts`
- `admin-content.schemas.ts`
- `admin-ai.schemas.ts`
- `admin-analytics.schemas.ts`
- `admin-system.schemas.ts`
- `admin-monitor.schemas.ts`

Original `admin.schemas.ts` becomes a barrel re-export for backward compatibility with the ~10 existing consumers.

### 1C. Reorganize `rag-dashboard/` root-level large files

**Current state**: `rag-dashboard/` is already partially organized with 164 files across existing subdirectories: `builder/` (with `nodes/` sub-directory, 23 files), `config/`, `metrics/`, `hooks/`, `retrieval-strategies/`, `__tests__/`. However, 6 large files (691-757 lines each) remain at the root level.

**Goal**: Move the 6 root-level large files into appropriate existing or new subdirectories.

**Pre-implementation step**: Audit existing subdirectories to determine correct destinations. Likely mapping:

| Root file | Candidate destination | Rationale |
|-----------|----------------------|-----------|
| `RagConfigurationForm.tsx` (751) | `config/` (existing) | Configuration UI |
| `AgentRoleConfigurator.tsx` (724) | `config/` (existing) | Agent config UI |
| `PerformanceMetricsTable.tsx` (691) | `metrics/` (existing) | Metrics display |
| `TokenFlowVisualizer.tsx` (757) | `metrics/` (existing) | Token flow visualization |
| `ParameterGuide.tsx` (748) | `reference/` (new) | Reference documentation UI |
| `LayerDeepDocs.tsx` (703) | `reference/` (new) | Layer documentation UI |

**Implementation**: Verify at implementation time that existing `config/` and `metrics/` directories are the right fit. Create `reference/` only if no existing directory is appropriate. Update all consumer imports.

**Scope note**: Only the 6 root-level large files are moved. The existing subdirectory structure (`builder/`, `hooks/`, etc.) is left unchanged. PR estimate: ~15-25 files (6 moved + consumer import updates).

---

## Phase 2: Core UI Decomposition

### 2A. `EntityExtraMeepleCard.tsx` (2052 lines → ~8 files)

**Current file**: `src/components/ui/data-display/extra-meeple-card/EntityExtraMeepleCard.tsx`

**Actual code structure**: The component is organized by **entity type** (Game, Player, Collection, Agent, Chat, Kb), NOT by visual variant. Each entity type is a self-contained exported component (120-488 lines). Shared infrastructure (ENTITY_COLORS, EntityHeader, EntityLoadingState, EntityErrorState) spans lines 1-126.

**New structure**:
```
extra-meeple-card/
├── EntityExtraMeepleCard.tsx       — re-exports all entity components (~50 lines)
├── shared.tsx                      (~130) — ENTITY_COLORS, EntityHeader, loading/error states
├── entities/
│   ├── GameExtraMeepleCard.tsx     (~322) — game detail with KB docs, agent preview
│   ├── PlayerExtraMeepleCard.tsx   (~158) — player profile, stats, history
│   ├── CollectionExtraMeepleCard.tsx (~119) — collection contents
│   ├── AgentExtraMeepleCard.tsx    (~379) — model info, KB docs, threads
│   ├── ChatExtraMeepleCard.tsx     (~346) — messages, citations, ChatBubble
│   └── KbExtraMeepleCard.tsx       (~488) — metadata, timeline, processing stats
└── index.ts                        — public re-export (already exists)
```

**Public API unchanged** — consumers import individual entity components (`GameExtraMeepleCard`, `AgentExtraMeepleCard`, etc.) which are already the named exports.

### 2B. `ChatThreadView.tsx` (852 lines → ~3 files)

**Current file**: `src/components/chat-unified/ChatThreadView.tsx`

**New structure**:
```
chat-unified/
├── ChatThreadView.tsx              (~300) — thread container, scroll logic
├── ChatMessageList.tsx             (~300) — message list rendering
└── ChatInputArea.tsx               (~250) — input, attachments, send
```

### 2C. `game-carousel.tsx` (913 lines → ~3 files)

**Current file**: `src/components/ui/data-display/game-carousel.tsx`

**New structure** (granular decomposition matching actual internal structure):
```
data-display/
├── game-carousel/
│   ├── GameCarousel.tsx            (~350) — main component (React.memo)
│   ├── GameCarouselSkeleton.tsx    (~50) — loading skeleton
│   ├── types.ts                    — CarouselGame, GameCarouselProps, sort types
│   ├── constants.ts                — CAROUSEL_SORT_OPTIONS
│   ├── math.ts                     — CardPosition, calculateCardPositions
│   ├── hooks/
│   │   ├── useSwipe.ts             — touch gesture handling
│   │   └── useKeyboardNavigation.ts — arrow key navigation
│   ├── components/
│   │   ├── NavButton.tsx           — prev/next chevron buttons
│   │   ├── DotsIndicator.tsx       — smart dots with ellipsis
│   │   ├── AutoPlayButton.tsx      — play/pause toggle
│   │   └── SortDropdown.tsx        — sort menu with click-outside
│   └── index.ts
```

### 2D. Files NOT decomposed (monitored only)

| File | Lines | Reason |
|------|-------|--------|
| `component-registry.ts` (3826) | Declarative config | Split would harm navigability |
| `block-definitions.ts` (1354) | Declarative config | RAG pipeline block definitions |
| `DebugPanel.tsx` (1034) | Dev-only | Low impact, used only in development |
| `httpClient.ts` (997) | Infrastructure | Cohesive responsibility, splitting would increase coupling |

---

## Phase 3: Structure & Naming

### 3A. Directory consolidation (3 pairs + 1 keep-separate)

**Pre-implementation step**: For each pair, verify actual file counts and usage at implementation time. The counts below are from code review analysis.

| Source | Destination | Action |
|--------|-------------|--------|
| `components/dashboard/` (~26 files, includes `zones/` subsystem, `DashboardEngine`) | `components/dashboard-v2/` (~28 files) | Audit which `dashboard/` files have equivalents in `dashboard-v2/`. Migrate unique files (esp. `zones/`, `DashboardEngine`, `DashboardEngineProvider`). Delete files superseded by v2 equivalents. |
| `components/game-night/` (~34 files, includes `steps/` subdirectory) | `components/game-nights/` (~16 files) | Merge into `game-night/` (richer one). **Name conflict**: both `game-night/SessionHeader.tsx` and `session/SessionHeader.tsx` exist with different interfaces — disambiguate as `GameNightSessionHeader` vs keep `SessionHeader` in `session/`. |
| `components/sessions/` (~21 files: 4 root + `live/` subdirectory with 11 source files + tests) | `components/session/` (~53 files) | Merge into `session/` (richer one). **Name conflict risk**: `sessions/live/InviteModal.tsx` may conflict with existing files in `session/` — audit `sessions/live/` for name collisions before merging and disambiguate with renames where needed. |

**Keep separate** (not consolidated):
| Directory pair | Reason |
|----------------|--------|
| `components/layout/` (~69 files: UnifiedShell, QuickView, CardRack, navigation system) vs `components/layouts/` (~12 files: AuthLayout, PublicLayout, PublicFooter) | These serve fundamentally different purposes: `layout/` is the authenticated app shell infrastructure, `layouts/` is page-level public/auth wrappers. Merging would conflate two distinct architectural layers. Keep both, ensure naming is clear. |

**Process per pair**:
1. Verify actual file counts and identify files with no imports (dead code)
2. Check for name conflicts between source and destination
3. Move used files to destination, resolving conflicts with renames
4. Update all imports (big bang)
5. Delete source directory
6. Run tests to validate

### 3B. Rename hooks kebab-case → camelCase (25 files)

All files in `src/hooks/` matching `use-*.ts` pattern:

```
use-app-mode.ts             → useAppMode.ts
use-bottom-nav-actions.ts   → useBottomNavActions.ts
use-bulk-collection-actions.ts → useBulkCollectionActions.ts
use-collection-actions.ts   → useCollectionActions.ts
use-contextual-actions.ts   → useContextualActions.ts
use-entity-actions.ts       → useEntityActions.ts
use-media-query.ts          → useMediaQuery.ts
use-toast.ts                → useToast.ts
... (17 others — full list determined at implementation time)
```

**High blast-radius rename**: `use-toast.ts` has ~37 importers across the codebase and implements the `console.warn('[Toast]')` stub pattern used by Playwright E2E tests (`page.on('console')` for toast detection). After renaming:
- Verify all E2E tests that capture console output for toast assertions still work
- Confirm no external documentation hardcodes the `@/hooks/use-toast` path

**Duplicate name conflict**: `use-media-query.ts` will rename to `useMediaQuery.ts`, but `src/lib/hooks/useMediaQuery.ts` (→ `src/lib/domain-hooks/useMediaQuery.ts` after 3C) already exists. These are two different hooks with different consumers:
- `src/hooks/use-media-query.ts` → used by `meeple-card-mobile-tags.tsx`, `HoverPreview.tsx`
- `src/lib/hooks/useMediaQuery.ts` → used by `FlipCard.tsx`

**Resolution**: At implementation time, determine if these can be consolidated into one (update 3 importers). If not, rename the `src/hooks/` one to `useBreakpointQuery.ts` to disambiguate.

**Process**:
1. `git mv use-kebab.ts useCamel.ts` for each file
2. Update all imports referencing old path
3. Update corresponding test files
4. Single PR for all 25 renames

### 3C. Rename `src/lib/hooks/` → `src/lib/domain-hooks/`

This directory contains genuine React hooks (`useGameDetail`, `useMediaQuery`, `useGameToolkit`, `useSessionSync`, `useTurnOrder`, `useDiceRoller`, etc. — 21 files using `useQuery`, `useMutation`, `useCallback`, `useEffect`). The rename distinguishes these domain-specific hooks from the application-level hooks in `src/hooks/`.

**Rationale**: `src/hooks/` = app-wide hooks (navigation, toast, actions). `src/lib/hooks/` = domain-specific hooks (game detail, session sync, dice roller). Renaming to `domain-hooks/` makes this distinction explicit.

1. `git mv src/lib/hooks/ src/lib/domain-hooks/`
2. Update all ~32 consumer imports
3. Verify no automated linting rules depend on hooks-in-hooks-directory patterns

---

## Phase 4: Quality Sweep

### 4A. Fix `any` types in production code (~400-500 instances)

**Scope**: Non-test files under `src/` (excluding `__tests__/`, `*.test.*`, `*.spec.*`)

**Fix strategy by pattern**:

| Pattern | Fix | Example |
|---------|-----|---------|
| Component props `any` | Type with interface | `props: any` → `props: GameCardProps` |
| API response `any` | Use Zod-inferred type | `data: any` → `data: z.infer<typeof gameSchema>` |
| Event handler `any` | Specific event type | `(e: any)` → `(e: React.ChangeEvent<HTMLInputElement>)` |
| Catch clause `any` | `unknown` + type guard | `catch(e: any)` → `catch(e: unknown)` |
| Generic fallback `any` | `Record<string, unknown>` or specific type | `obj: any` → `obj: Record<string, unknown>` |

**Excluded**:
- Test files (partial mocks require legitimate `any`)
- Type assertions in test setup (`as any`)
- External libraries without types (add `// eslint-disable-next-line` with comment)

### 4B. Replace `console.*` → structured logger (327 instances)

**Prerequisite**: Verify existing logger in `src/lib/`. If absent, create minimal one:

```typescript
// src/lib/logger.ts
const logger = {
  debug: (...args: unknown[]) => {
    if (process.env.NODE_ENV === 'development') console.debug('[DEBUG]', ...args);
  },
  info: (...args: unknown[]) => console.info('[INFO]', ...args),
  warn: (...args: unknown[]) => console.warn('[WARN]', ...args),
  error: (...args: unknown[]) => console.error('[ERROR]', ...args),
};
export default logger;
```

**Replacement mapping**:

| Original | Replacement |
|----------|-------------|
| `console.log` (debug intent) | `logger.debug` |
| `console.log` (info intent) | `logger.info` |
| `console.warn` | `logger.warn` |
| `console.error` | `logger.error` |

**Excluded**:
- `console.*` inside test files
- `console.warn('[Toast]')` — documented toast stub pattern
- Playwright/E2E console capture patterns

### 4C. ESLint enforcement

After fixes, update `eslint.config.mjs`:
- Reduce `max-warnings` from 410 toward target <100
- Enable `@typescript-eslint/no-explicit-any` as warning for production code

---

## PR Strategy

Each phase = 1 PR to `frontend-dev` (all changes are frontend-only under `apps/web/`).

| PR | Phase | Scope | Est. files changed |
|----|-------|-------|--------------------|
| PR 1 | 1A+1B | adminClient + schemas split | ~30-40 |
| PR 2 | 1C | rag-dashboard root file reorganization | ~15-25 |
| PR 3 | 2A | EntityExtraMeepleCard decomposition | ~15-20 |
| PR 4 | 2B+2C | ChatThreadView + game-carousel decomposition | ~15-20 |
| PR 5 | 3A | Directory consolidation (3 pairs) | ~50-70 |
| PR 6 | 3B+3C | Hook renames + lib/domain-hooks rename | ~40-60 |
| PR 7 | 4A | Fix `any` types production | ~80-100 |
| PR 8 | 4B+4C | console.log replacement + ESLint tighten | ~60-80 |

**Total**: 8 PRs, ~300-400 files touched across all phases.

**Validation per PR**: `pnpm typecheck && pnpm lint && pnpm test` must pass before merge.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Import breakage after renames | Full test suite run per PR; `pnpm typecheck` catches missing imports |
| UI regression after component decomposition | Visual comparison of affected pages; existing Playwright E2E coverage |
| Merge conflicts between phases | Vertical approach minimizes overlap; merge each PR before starting next |
| `any` removal reveals hidden type bugs | Fix incrementally; if a type is too complex, use `unknown` as stepping stone |
| Large PR review burden | Each PR focused on single concern; self-review via code-review agent |

---

## Out of Scope

- Refactoring component internal logic (only structural decomposition)
- Adding new features or functionality
- Backend changes
- New test creation (only updating existing tests for moved/renamed files)
- Performance optimization (separate initiative)
- `component-registry.ts`, `block-definitions.ts`, `DebugPanel.tsx`, `httpClient.ts` decomposition
