# Codebase Dead Code Cleanup Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove dead/orphan frontend components, hooks, stores, tests, and stale files identified by spec-panel analysis.

**Architecture:** Pure deletion + barrel export updates. No new code. Each task is an independent group of related dead files. Verify with typecheck + test suite after each group.

**Tech Stack:** Next.js 16, Tailwind 4, Vitest

**Branch:** `chore/dead-code-cleanup` from `main-dev`

**Parent branch:** `main-dev`

---

## File Structure — Files to DELETE

### Group A: Dead Layout Components (PR #431 leftovers)
| Delete | Reason |
|--------|--------|
| `apps/web/src/components/layout/ActionPill/ActionPill.tsx` | 0 imports |
| `apps/web/src/components/layout/ActionPill/index.ts` | Barrel for dead component |
| `apps/web/src/components/layout/FloatingActionPill.tsx` | 0 imports outside barrel |
| `apps/web/src/components/layout/HandRail/HandRail.tsx` | 0 imports |
| `apps/web/src/components/layout/HandRail/HandRailCard.tsx` | Only used by dead HandRail |
| `apps/web/src/components/layout/HandRail/index.ts` | Barrel for dead component |
| `apps/web/src/components/layout/mobile/HandDrawer.tsx` | 0 imports |
| `apps/web/src/components/layout/mobile/ActionBar.tsx` | 0 imports |

### Group B: Dead Hooks
| Delete | Reason |
|--------|--------|
| `apps/web/src/hooks/useDrawCard.ts` | Only used by dead components |
| `apps/web/src/hooks/useCollectionStats.ts` | 0 imports |
| `apps/web/src/hooks/useDashboardContext.ts` | 0 imports |
| `apps/web/src/hooks/useSwipeGesture.ts` | 0 imports |

### Group C: Dead Stores
| Delete | Reason |
|--------|--------|
| `apps/web/src/lib/stores/card-hand-store.ts` | Only used by dead components/hooks |
| `apps/web/src/stores/ragConfigStore.ts` | 0 imports |

### Group D: Dead Tests (for deleted components)
| Delete | Reason |
|--------|--------|
| `apps/web/src/__tests__/components/layout/ActionPill.test.tsx` | Tests dead ActionPill |
| `apps/web/src/__tests__/components/layout/FloatingActionPill.test.tsx` | Tests dead FloatingActionPill |
| `apps/web/src/__tests__/components/layout/HandRail.test.tsx` | Tests dead HandRail |
| `apps/web/src/__tests__/components/layout/mobile/HandDrawer.test.tsx` | Tests dead HandDrawer |
| `apps/web/src/__tests__/components/layout/mobile/ActionBar.test.tsx` | Tests dead ActionBar |
| `apps/web/src/__tests__/hooks/useDrawCard.test.ts` | Tests dead useDrawCard |
| `apps/web/src/__tests__/hooks/useDashboardContext.test.ts` | Tests dead useDashboardContext (if exists) |
| `apps/web/src/__tests__/hooks/useSwipeGesture.test.ts` | Tests dead useSwipeGesture (if exists) |
| `apps/web/src/__tests__/stores/card-hand-store.test.ts` | Tests dead card-hand-store |

### Group E: Stale Files
| Delete | Reason |
|--------|--------|
| `apps/api/tests/Api.Tests/Integration/KnowledgeBase/Agents/ArbitroAgentIntegrationTests.cs.old` | Backup file |
| `docs/development/agent-stats-display-decision.md` | Decision for deleted component |
| `docs/development/orphan-components-execution-plan.md` | Completed plan from months ago |

## Files to MODIFY

| Modify | Change |
|--------|--------|
| `apps/web/src/components/layout/index.ts` | Remove exports for ActionPill, FloatingActionPill, HandRail, ActionBar, HandDrawer |

---

### Task 1: Delete dead layout components + their tests

**Files to delete:**
- `apps/web/src/components/layout/ActionPill/ActionPill.tsx`
- `apps/web/src/components/layout/ActionPill/index.ts`
- `apps/web/src/components/layout/FloatingActionPill.tsx`
- `apps/web/src/components/layout/HandRail/HandRail.tsx`
- `apps/web/src/components/layout/HandRail/HandRailCard.tsx`
- `apps/web/src/components/layout/HandRail/index.ts`
- `apps/web/src/components/layout/mobile/HandDrawer.tsx`
- `apps/web/src/components/layout/mobile/ActionBar.tsx`
- `apps/web/src/__tests__/components/layout/ActionPill.test.tsx`
- `apps/web/src/__tests__/components/layout/FloatingActionPill.test.tsx`
- `apps/web/src/__tests__/components/layout/HandRail.test.tsx`
- `apps/web/src/__tests__/components/layout/mobile/HandDrawer.test.tsx`
- `apps/web/src/__tests__/components/layout/mobile/ActionBar.test.tsx`

**Files to modify:**
- `apps/web/src/components/layout/index.ts` — remove all exports referencing deleted files

- [ ] **Step 1: Delete all component files**
- [ ] **Step 2: Delete all test files**
- [ ] **Step 3: Update layout barrel export** — remove lines exporting ActionPill, FloatingActionPill, HandRail, ActionBar, HandDrawer
- [ ] **Step 4: Grep for remaining imports** — `grep -r "ActionPill\|FloatingActionPill\|HandRail\|HandDrawer\|ActionBar" apps/web/src/ --include="*.ts" --include="*.tsx" -l` and fix any broken imports
- [ ] **Step 5: Run typecheck** — `cd apps/web && pnpm typecheck`
- [ ] **Step 6: Commit** — `git commit -m "chore(cleanup): remove dead layout components (ActionPill, HandRail, HandDrawer, ActionBar, FloatingActionPill)"`

---

### Task 2: Delete dead hooks + stores + their tests

**Files to delete:**
- `apps/web/src/hooks/useDrawCard.ts`
- `apps/web/src/hooks/useCollectionStats.ts`
- `apps/web/src/hooks/useDashboardContext.ts`
- `apps/web/src/hooks/useSwipeGesture.ts`
- `apps/web/src/lib/stores/card-hand-store.ts`
- `apps/web/src/stores/ragConfigStore.ts`
- `apps/web/src/__tests__/hooks/useDrawCard.test.ts`
- `apps/web/src/__tests__/stores/card-hand-store.test.ts`
- `apps/web/src/hooks/__tests__/useDashboardContext.test.ts` (if exists)
- `apps/web/src/hooks/__tests__/useSwipeGesture.test.ts` (if exists)

- [ ] **Step 1: Delete all hook/store files and their tests**
- [ ] **Step 2: Grep for remaining imports** — fix any broken references
- [ ] **Step 3: Run typecheck** — `cd apps/web && pnpm typecheck`
- [ ] **Step 4: Commit** — `git commit -m "chore(cleanup): remove dead hooks and stores (useDrawCard, card-hand-store, ragConfigStore, etc.)"`

---

### Task 3: Delete stale files + orphan docs

**Files to delete:**
- `apps/api/tests/Api.Tests/Integration/KnowledgeBase/Agents/ArbitroAgentIntegrationTests.cs.old`
- `docs/development/agent-stats-display-decision.md`
- `docs/development/orphan-components-execution-plan.md`

- [ ] **Step 1: Delete files**
- [ ] **Step 2: Commit** — `git commit -m "chore(cleanup): remove stale backup files and orphan docs"`

---

### Task 4: Fix remaining broken references

After Tasks 1-3, some config/type files may still reference deleted code.

- [ ] **Step 1: Full grep scan** — search for all deleted identifiers across codebase
- [ ] **Step 2: Fix each broken reference** — remove imports, config entries, type references
- [ ] **Step 3: Run typecheck** — `cd apps/web && pnpm typecheck`
- [ ] **Step 4: Run full test suite** — `cd apps/web && pnpm test`
- [ ] **Step 5: Commit** — `git commit -m "chore(cleanup): fix broken references to deleted code"`

---

### Task 5: Final validation

- [ ] **Step 1: Run typecheck** — `cd apps/web && pnpm typecheck`
- [ ] **Step 2: Run lint** — `cd apps/web && pnpm lint`
- [ ] **Step 3: Run full test suite** — `cd apps/web && pnpm test`
- [ ] **Step 4: Verify deletion count** — confirm all targeted files are gone
