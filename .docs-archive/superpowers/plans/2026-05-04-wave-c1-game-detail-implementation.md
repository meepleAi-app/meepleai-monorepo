# Wave C.1 `/games/[id]` v2 Brownfield Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to execute task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Prerequisites (non-negotiable)**:
> 1. PR #700 (Phase 0.5 contract) MUST be merged into `main-dev` before dispatch
> 2. Read [`docs/frontend/contracts/games-id-hooks.md`](../../frontend/contracts/games-id-hooks.md) entirely — it is the source of truth for FSM cells, hook gating, and component contracts
> 3. Read closed PR #697 commits (5c53ce4e3, 52bcfdb47, 15b63e86c, 30d48a26e) as reference of prior work — branch `feature/issue-581-wave-c1-game-detail-fe-v2` retained for diff
>
> **Goal:** Brownfield big-bang migration di `/games/[id]` route da v1 (hero MeepleCard + 4-link tab nav a subroute) a v2 design (single-page tabs animated underline + 8 v2 components + FSM 5-state) seguendo Phase 0.5 contract.
>
> **Architecture:** Tier L route per spec sez. 1.2 #5 — orchestrator `GameDetailViewV2` con hook gating cumulativo (parent + lazy tab); subroute pages `/games/[id]/{rules,faqs,sessions,strategies,reviews}` PRESERVATE unchanged.
>
> **Tech Stack:** Next.js 16 App Router, TanStack Query v5, Vitest + RTL, Playwright (visual + a11y axe-core).

---

## Tier classification

- **Tier**: L (≥3 hooks, cartesian FSM ≥16 celle teoriche, 9 critical celle covered)
- **Coexistence flag**: NO (decisione contract sez. 7 — app pre-prod, big-bang OK)
- **Bundle budget**: target ~95 KB (margin 25 KB su +120 KB Tier L cap)
- **Test mix ratio**: 50% unit / 35% integration / 15% e2e (~25 + 18 + 5 tests)
- **Effort estimate**: 6-8 giorni (vs 5-7 giorni Wave B Tier S, +Phase 0.5 overhead)

## File structure

### Files to CREATE

```
apps/web/src/lib/games/
  game-detail-state.ts                         # FSM state derivation (cherry-pick PR #697 commit 5c53ce4e3)
  game-detail-visual-test-fixture.ts           # Visual fixture sentinel (cherry-pick PR #697)

apps/web/src/components/v2/game-detail/         # 8 components (cherry-pick PR #697 commit 52bcfdb47)
  GameDetailHero.tsx
  GameDetailTabsAnimated.tsx
  GameDetailKpiCards.tsx
  GameDetailFaqList.tsx
  GameDetailRulesAccordion.tsx
  GameDetailSessionsRail.tsx
  GameDetailAgentsList.tsx
  GameDetailKbDocList.tsx
  index.ts                                      # Barrel exports
  __tests__/                                    # Unit FSM tests per component
    GameDetailHero.test.tsx
    GameDetailTabsAnimated.test.tsx
    GameDetailKpiCards.test.tsx
    GameDetailFaqList.test.tsx
    GameDetailRulesAccordion.test.tsx
    GameDetailSessionsRail.test.tsx
    GameDetailAgentsList.test.tsx
    GameDetailKbDocList.test.tsx

apps/web/src/app/(authenticated)/games/[id]/_components/
  GameDetailViewV2.tsx                          # Orchestrator (REWRITE from PR #697 — fix gating bug)
  __tests__/
    GameDetailViewV2.test.tsx                   # Integration tests via renderHook + MSW

apps/web/e2e/visual-migrated/sp4-game-detail.spec.ts
apps/web/e2e/v2-states/game-detail.spec.ts
apps/web/e2e/a11y/game-detail.spec.ts
apps/web/e2e/smoke-real-backend/game-detail.smoke.spec.ts

apps/web/src/locales/{it,en}.json               # +~50 keys/locale (cherry-pick + verify)
```

### Files to MODIFY

```
apps/web/src/app/(authenticated)/games/[id]/page.tsx
  REPLACE: legacy hero + tab nav → thin shell rendering <GameDetailViewV2 id={normalizedGameId} />

apps/web/src/app/(authenticated)/games/[id]/__tests__/GameDetailPage.test.tsx
  DELETE: legacy v1 surface tests no longer applicable (mirror PR #697 decision)

docs/frontend/v2-migration-matrix.md
  UPDATE: 8 game-detail rows status pending → done, PR ref → #N (this PR)
```

### Files to PRESERVE unchanged

```
apps/web/src/app/(authenticated)/games/[id]/{rules,faqs,sessions,strategies,reviews}/
  Subroute pages remain v1 — NOT in scope for Wave C.1 (decision per contract sez. 4.4)
  Empty-state CTAs in v2 components link to these subroutes
```

---

## Task decomposition (5-commit TDD pattern)

Each commit is independently testable. After each commit, all existing tests must remain green. **Branch from `main-dev` after PR #700 merge**.

### Task 1: Foundation — i18n + visual-test fixture + FSM state derivation

**Files:**
- Create: `apps/web/src/lib/games/game-detail-state.ts`
- Create: `apps/web/src/lib/games/game-detail-visual-test-fixture.ts`
- Modify: `apps/web/src/locales/it.json` (+50 keys)
- Modify: `apps/web/src/locales/en.json` (+50 keys)
- Test: `apps/web/src/lib/games/__tests__/game-detail-state.test.ts`
- Test: `apps/web/src/lib/games/__tests__/game-detail-visual-test-fixture.test.ts`

**Reference**: cherry-pick `git show 5c53ce4e3 -- apps/web/src/lib/games/` from closed PR #697 — files are likely correct, verify against contract sez. 3.2 state derivation.

**Critical contract enforcement**:

```ts
// apps/web/src/lib/games/game-detail-state.ts
export type GameDetailUiState = 'loading' | 'error' | 'not-found' | 'default';

export interface DeriveGameDetailUiStateInput {
  gameId: string | null;       // ⚠️ must be string|null, NEVER undefined
  isLoading: boolean;
  isError: boolean;
  hasData: boolean;
}

export function deriveGameDetailUiState(input: DeriveGameDetailUiStateInput): GameDetailUiState {
  // ⚠️ CRITICAL: gameId === null short-circuits FIRST (Cell 1 contract)
  if (input.gameId == null) return 'not-found';
  if (input.isLoading) return 'loading';
  if (input.isError) return 'error';
  if (!input.hasData) return 'not-found';  // Cell 4: success(null)
  return 'default';
}
```

**Test plan (~15 unit tests)**:
- [ ] **Step 1.1**: Property test — `gameId === null` returns `'not-found'` for ALL combinations of isLoading/isError/hasData (8 cases)
- [ ] **Step 1.2**: `gameId='valid' & isLoading=true` returns `'loading'`
- [ ] **Step 1.3**: `gameId='valid' & isLoading=false & isError=true` returns `'error'`
- [ ] **Step 1.4**: `gameId='valid' & isLoading=false & isError=false & hasData=false` returns `'not-found'` (Cell 4)
- [ ] **Step 1.5**: `gameId='valid' & isLoading=false & isError=false & hasData=true` returns `'default'`
- [ ] **Step 1.6**: Visual fixture `tryLoadVisualTestFixture('default')` returns Wingspan-shaped `LibraryGameDetail` when `IS_VISUAL_TEST_BUILD === true`, else returns `null`
- [ ] **Step 1.7**: Visual fixture `tryLoadVisualTestFixture('not-found')` returns `null` always
- [ ] **Step 1.8**: i18n keys present in both locales (at minimum: `pages.gameDetail.{hero,tabs,sections}.*`)
- [ ] **Step 1.9**: i18n keys parity check (it/en have same keys)
- [ ] **Step 1.10**: Run `pnpm typecheck` → expect green
- [ ] **Step 1.11**: Run `pnpm test src/lib/games` → expect 15+ tests passing

**Commit message**:
```
feat(game-detail): foundation — FSM derivation + visual fixture + i18n (Wave C.1)

Phase 0.5 contract enforced: deriveGameDetailUiState short-circuits on
gameId === null FIRST before any other check (Cell 1 contract). All FSM
cells documented in docs/frontend/contracts/games-id-hooks.md sez. 3.

Cherry-picked from PR #697 commit 5c53ce4e3 with state derivation hardening
(adds gameId null check that was missing in original).

Refs #581 (Wave C umbrella), #573 (matrix).
```

---

### Task 2: 8 v2 components implementation

**Files:**
- Create: `apps/web/src/components/v2/game-detail/{GameDetailHero,GameDetailTabsAnimated,GameDetailKpiCards,GameDetailFaqList,GameDetailRulesAccordion,GameDetailSessionsRail,GameDetailAgentsList,GameDetailKbDocList}.tsx`
- Create: `apps/web/src/components/v2/game-detail/index.ts` (barrel)
- Create: `apps/web/src/components/v2/game-detail/__tests__/*.test.tsx` (8 unit test files)

**Reference**: cherry-pick `git show 52bcfdb47 -- apps/web/src/components/v2/game-detail/` from PR #697.

**Component contracts** (per Phase 0.5 sez. 4):

#### 2.1 `GameDetailHero` (variant own/community)
- Props: `variant`, `title`, `subtitle`, `imageUrl`, `meta`, `manaPips`, `ctas`, `labels`
- Tests: 4 — variant='own' shows Edit+Play CTAs; variant='community' shows AddToLibrary CTA; meta optional fields render conditionally; reduced-motion gates animation

#### 2.2 `GameDetailTabsAnimated` (a11y critical)
- Props: `tabs`, `activeKey`, `onChange`, `ariaLabel`
- A11y: `role="tablist"`, `role="tab" aria-selected aria-controls`, `role="tabpanel" aria-labelledby id`
- Reuses `useTablistKeyboardNav` hook (PR #623)
- Tests: 5 — render shape, aria attrs, keyboard nav (delegated to hook), animated underline collapses under reduced-motion, badge count display

#### 2.3 `GameDetailKpiCards` (pure display)
- Props: `manaPips?: ManaPip[]`
- Tests: 3 — empty array renders empty grid, populated renders correct count, accent color tokens applied

#### 2.4 `GameDetailFaqList` (legacy CTA bridge)
- Props: `state: 'empty' | 'has-items'`, `viewAllHref`, `labels`
- Tests: 2 — empty state CTA, has-items list rendering

#### 2.5 `GameDetailRulesAccordion`
- Props: `sections: RuleSection[]`, `viewAllHref`, `labels`, animated chevron
- Tests: 3 — empty state, expand/collapse, motion-reduce chevron rotation gated

#### 2.6 `GameDetailSessionsRail`
- Props: `sessions: SessionEntry[]`, `labels`, win/loss outcome pip
- Tests: 3 — horizontal scroll rail, win pip, empty state

#### 2.7 `GameDetailAgentsList` (discriminated union)
- Props: `state: AgentsState`, `labels`
- ⚠️ Critical contract: discriminated union prevents `data + loading` co-occurrence
- Tests: 4 — render per kind ('loading' | 'error' | 'empty' | 'success')

#### 2.8 `GameDetailKbDocList`
- Props: `state: KbState`, `labels`
- Tests: 3 — render per state, empty CTA, doc count

**Common contract**:
- All components are PURE (no hooks called inside) — orchestrator passes resolved data
- All accept `labels` (i18n strings injected upfront)
- All have `data-slot` attribute for E2E selector targeting

**Test plan (~28 unit tests across 8 files)**:
- [ ] **Step 2.1**: Cherry-pick components from PR #697, verify contract conformance
- [ ] **Step 2.2**: Add/update unit tests covering AC sez. 4 contracts
- [ ] **Step 2.3**: Run `pnpm test src/components/v2/game-detail` → expect 28+ tests green
- [ ] **Step 2.4**: Run `pnpm typecheck` → expect green

**Commit message**:
```
feat(game-detail): 8 v2 components implementation (Wave C.1)

Pure presentation components per Phase 0.5 contract sez. 4:
- GameDetailHero (variant own/community CTA matrix)
- GameDetailTabsAnimated (a11y tablist + useTablistKeyboardNav reuse, PR #623)
- GameDetailKpiCards (pure display)
- GameDetailFaqList/RulesAccordion/SessionsRail/KbDocList (legacy subroute CTA bridges)
- GameDetailAgentsList (discriminated union AgentsState — no data+loading co-occurrence)

All components PURE — orchestrator passes resolved data. All have data-slot for E2E.

Cherry-picked from PR #697 commit 52bcfdb47, contract-aligned.

Refs #581.
```

---

### Task 3: Orchestrator + page wiring (REWRITE — fix PR #697 gating bug)

**Files:**
- Create: `apps/web/src/app/(authenticated)/games/[id]/_components/GameDetailViewV2.tsx`
- Create: `apps/web/src/app/(authenticated)/games/[id]/_components/__tests__/GameDetailViewV2.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/games/[id]/page.tsx` (thin shell)
- Delete: `apps/web/src/app/(authenticated)/games/[id]/__tests__/GameDetailPage.test.tsx` (v1 surface tests)

**Reference**: PR #697 commit 15b63e86c had the orchestrator bug. **DO NOT cherry-pick blindly** — rewrite with Phase 0.5 contract enforcement.

**Critical fixes vs PR #697**:

```diff
- // ❌ PR #697 anti-pattern
- const id = params?.id ?? '';
- const agentsQuery = useQuery({
-   queryKey: ['game-detail', 'agents', id],
-   queryFn: async () => api.agents.getUserAgentsForGame(id),
-   enabled: !!id && (fixture == null ? detailQuery.data != null : true),
- });

+ // ✅ Phase 0.5 contract (sez. 2.1 + 2.2)
+ const rawId = params?.id;
+ const gameId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;
+
+ const detailQuery = useLibraryGameDetail(gameId);  // hook accepts string|null
+
+ const agentsQuery = useGameAgents({
+   gameId,
+   enabled: !!gameId && detailQuery.isSuccess && tab === 'agents',
+ });
```

**Page shell**:
```tsx
// apps/web/src/app/(authenticated)/games/[id]/page.tsx
'use client';

import { useParams } from 'next/navigation';
import { GameDetailViewV2 } from './_components/GameDetailViewV2';

export default function GameDetailPage() {
  const params = useParams<{ id: string }>();
  const rawId = params?.id;
  const gameId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

  return <GameDetailViewV2 gameId={gameId} />;
}
```

**Orchestrator skeleton** (illustrative):
```tsx
// apps/web/src/app/(authenticated)/games/[id]/_components/GameDetailViewV2.tsx
'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'next/navigation';

import * as v2 from '@/components/v2/game-detail';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useGameAgents } from '@/hooks/queries/useGameAgents';
import { useTranslation } from '@/hooks/useTranslation';
import { deriveGameDetailUiState } from '@/lib/games/game-detail-state';
import {
  IS_VISUAL_TEST_BUILD,
  tryLoadVisualTestFixture,
} from '@/lib/games/game-detail-visual-test-fixture';

const VALID_OVERRIDES = ['loading', 'empty', 'not-found', 'error'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

type TabKey = 'info' | 'rules' | 'faqs' | 'sessions' | 'agents' | 'documents';

export interface GameDetailViewV2Props {
  readonly gameId: string | null;  // ⚠️ string|null, NEVER undefined
}

export function GameDetailViewV2({ gameId }: GameDetailViewV2Props) {
  const { t } = useTranslation();
  const searchParams = useSearchParams();
  const [tab, setTab] = useState<TabKey>('info');

  const stateOverride = parseStateOverride(searchParams.get('state'));

  // Visual fixture short-circuit (CI prod build)
  const fixture = useMemo(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    if (stateOverride === 'not-found' || stateOverride === 'empty') return null;
    return tryLoadVisualTestFixture('default');
  }, [stateOverride]);

  // ⚠️ Parent hook — gated by gameId valid
  const detailQuery = useLibraryGameDetail(gameId);

  // ⚠️ Sub-hook — gated by parent success + tab active (Phase 0.5 sez. 2.2)
  const agentsQuery = useGameAgents({
    gameId,
    enabled: !!gameId && detailQuery.isSuccess && tab === 'agents',
  });

  const detail = fixture ?? detailQuery.data ?? null;

  const realKind = useMemo(() => {
    if (fixture != null) return 'default';
    return deriveGameDetailUiState({
      gameId,
      isLoading: detailQuery.isLoading,
      isError: detailQuery.isError,
      hasData: detailQuery.data != null,
    });
  }, [gameId, detailQuery.isLoading, detailQuery.isError, detailQuery.data, fixture]);

  const effectiveKind = stateOverride ?? realKind;

  // Render shells per FSM cell matrix sez. 3
  if (effectiveKind === 'loading') return <LoadingShell />;
  if (effectiveKind === 'error') return <ErrorShell onRetry={() => detailQuery.refetch()} />;
  if (effectiveKind === 'not-found') return <NotFoundShell />;

  // Default render — detail !== null guaranteed
  return (
    <div data-slot="game-detail-view">
      <v2.GameDetailHero {...heroProps(detail!)} />
      <v2.GameDetailTabsAnimated
        tabs={tabsConfig}
        activeKey={tab}
        onChange={setTab}
        ariaLabel={t('pages.gameDetail.tabs.ariaLabel')}
      />
      {/* Tab panels per FSM cells 5-9 */}
      {tab === 'info' && <v2.GameDetailKpiCards manaPips={detail!.manaPips} />}
      {tab === 'agents' && <v2.GameDetailAgentsList state={agentsState(agentsQuery)} labels={...} />}
      {/* ... other tabs */}
    </div>
  );
}
```

**Test plan (18 integration tests)**:

Cover all 9 FSM cells from Phase 0.5 contract sez. 3:

- [ ] **Step 3.1** Cell 1: render with `gameId={null}` → not-found shell, NO sub-hook fetch (assert MSW handler called 0 times)
- [ ] **Step 3.2** Cell 2: detail loading → loading shell rendered, agents query NOT enabled (assert via `agentsQuery.fetchStatus === 'idle'`)
- [ ] **Step 3.3** Cell 3: detail error → error shell rendered, retry CTA wired to `detailQuery.refetch`
- [ ] **Step 3.4** Cell 4: detail success(null) → not-found shell (distinct from Cell 1)
- [ ] **Step 3.5** Cell 5: detail success + tab='info' → default render, agents query NOT mounted
- [ ] **Step 3.6** Cell 5→6 transition: tab change info→agents → agents fetch fired ON tab change (lazy, NOT eager)
- [ ] **Step 3.7** Cell 6: agents loading → inline skeleton in tab content (not shell skeleton)
- [ ] **Step 3.8** Cell 7: agents error → inline error banner, NO shell error (entire page still renders)
- [ ] **Step 3.9** Cell 8: agents success([]) → empty state CTA in tab
- [ ] **Step 3.10** Cell 9: agents success([...]) → grid populated
- [ ] **Step 3.11** `?state=loading` URL override → loading shell (only in dev/visual-test build)
- [ ] **Step 3.12** `?state=error` URL override → error shell
- [ ] **Step 3.13** `?state=not-found` URL override → not-found shell
- [ ] **Step 3.14** `?state=empty` URL override → not-found shell (alias)
- [ ] **Step 3.15** Invalid `?state=xyz` → falls through to real state
- [ ] **Step 3.16** Visual fixture short-circuit when `IS_VISUAL_TEST_BUILD === true`
- [ ] **Step 3.17** ⚠️ **MSW critical assertion**: agents handler NEVER receives `'undefined'` or empty string for gameId param (across ALL cells)
- [ ] **Step 3.18** Tab state preserved across re-renders (no reset on detail refetch)

**Commit message**:
```
feat(game-detail): orchestrator + page wiring (Wave C.1)

GameDetailViewV2 implements Phase 0.5 contract:
- gameId normalized to string|null at page boundary (NEVER undefined)
- useLibraryGameDetail (parent) gated by !!gameId
- useGameAgents (lazy) gated by !!gameId && detailQuery.isSuccess && tab === 'agents'
- 5-state FSM via deriveGameDetailUiState (gameId null check FIRST)
- Visual fixture short-circuit for CI prod build
- ?state= URL override (dev + visual-test only)

REWRITE from PR #697 commit 15b63e86c — fixes /api/v1/agents/undefined cascade
by enforcing contract sez. 2.1 (gameId normalization) + sez. 2.2 (sub-hook gating).

18 integration tests cover 9 FSM cells from contract sez. 3, with critical
MSW assertion: agents handler MUST never receive 'undefined' or empty string.

Removes legacy GameDetailPage.test.tsx (v1 surface no longer applicable —
mirror Wave B.3 LibraryHubV2 brownfield pattern).

Refs #581.
```

---

### Task 4: E2E specs (visual-migrated + v2-states + a11y)

**Files:**
- Create: `apps/web/e2e/visual-migrated/sp4-game-detail.spec.ts`
- Create: `apps/web/e2e/v2-states/game-detail.spec.ts`
- Create: `apps/web/e2e/a11y/game-detail.spec.ts`
- Create: `apps/web/e2e/smoke-real-backend/game-detail.smoke.spec.ts`

**Reference**: PR #697 commit 30d48a26e — cherry-pick + extend for FSM cells.

**Test plan**:
- [ ] **Step 4.1** Visual-migrated spec: 1280×720 + 375×812 baselines via `IS_VISUAL_TEST_BUILD` fixture (2 PNG canonical Linux x86-64)
- [ ] **Step 4.2** v2-states spec: 4 states via `?state=loading,error,not-found,empty` URL override (8 PNG = 4 states × 2 viewports)
- [ ] **Step 4.3** a11y spec: axe-core WCAG 2.1 AA scan + reduced-motion contract test
- [ ] **Step 4.4** Smoke real-backend spec: deterministic UUID `00000000-0000-4000-8000-000000000581` for not-found path; if `SMOKE_GAME_DETAIL_ID` env set, navigate to seeded id and assert hero shell
- [ ] **Step 4.5** Run `pnpm test:e2e --project=v2-states` → expect green
- [ ] **Step 4.6** Run `pnpm test:e2e --project=a11y` → expect green
- [ ] **Step 4.7** Run `pnpm test:e2e --project=visual-migrated` → expect baselines bootstrap (will fail on first run, generate snapshots, commit in Task 5)

**Critical anti-patterns to avoid (from Wave B lessons)**:
- ❌ `waitUntil: 'networkidle'` (flaky in CI with SSR + TanStack Query)
- ✅ `waitUntil: 'domcontentloaded'` + explicit `waitForSelector('[data-slot="game-detail-view"]')`
- ❌ shared-component a11y exclusion via inline override
- ✅ if axe finds shared-component issue: add `data-slot="..."` semantic to component, then `.exclude(...)` chain

**Commit message**:
```
test(game-detail): e2e visual-migrated + v2-states + a11y + smoke (Wave C.1)

- visual-migrated/sp4-game-detail.spec.ts: 1280+375 baselines via fixture
- v2-states/game-detail.spec.ts: 4 states × 2 viewports = 8 PNG
- a11y/game-detail.spec.ts: axe-core WCAG 2.1 AA + reduced-motion
- smoke-real-backend/game-detail.smoke.spec.ts: deterministic UUID + optional seeded id

Covers all 9 FSM cells from Phase 0.5 contract (4 via URL override, 5 via real fixture).

Cherry-picked + extended from PR #697 commit 30d48a26e.

Refs #581.
```

---

### Task 5: Visual baselines bootstrap + matrix update

**Files:**
- Add: `apps/web/e2e/visual-migrated/__snapshots__/sp4-game-detail/*.png` (2 PNG)
- Add: `apps/web/e2e/v2-states/__snapshots__/game-detail/*.png` (8 PNG)
- Modify: `docs/frontend/v2-migration-matrix.md` (8 game-detail rows status pending → done, PR ref → #N)

**Test plan**:
- [ ] **Step 5.1** Trigger workflow `visual-regression-migrated.yml` mode=bootstrap
- [ ] **Step 5.2** Download generated baselines from CI artifact
- [ ] **Step 5.3** Commit 10 PNG canonical Linux x86-64
- [ ] **Step 5.4** Update matrix rows in `docs/frontend/v2-migration-matrix.md`
- [ ] **Step 5.5** Run full E2E suite locally → expect all green (with baselines)

**Commit message**:
```
chore(game-detail): bootstrap visual baselines + matrix update (Wave C.1)

- 10 PNG canonical Linux x86-64 from workflow visual-regression-migrated.yml
  bootstrap run: 2 visual-migrated + 8 v2-states (4 stati × 2 viewport)
- Migration matrix: 8 game-detail rows pending → done, PR ref #N

Refs #581.
```

---

## CI gates (per spec sez. 4.1)

Tutti **OBBLIGATORI** verdi prima di merge:

- [ ] Typecheck (`pnpm typecheck`)
- [ ] Lint (`pnpm lint` — incl. no-hardcoded-hex rule)
- [ ] Vitest unit (target ≥85%, ratio Tier L 50% = ~25 unit tests)
- [ ] Vitest integration (~18 tests via renderHook + MSW)
- [ ] Playwright E2E happy path
- [ ] Playwright visual-migrated regression
- [ ] Playwright v2-states (4 stati)
- [ ] Playwright a11y (axe-core WCAG 2.1 AA, zero violations)
- [ ] Bundle size delta < +120 KB (Tier L cap, target ~95 KB)
- [ ] GitGuardian (no secret pattern)
- [ ] Smoke E2E real-backend (manual gate `workflow_dispatch`, post-merge nightly)

## Definition of Done (page-level, per spec sez. 4.1 amended)

- [ ] Page legacy completamente rimossa (file `__tests__/GameDetailPage.test.tsx` deleted; `page.tsx` becomes thin shell)
- [ ] Page v2 implementata 1:1 con mockup `sp4-game-detail.jsx`
- [ ] **Phase 0.5 contract validated post-implementation**: tutte 9 FSM cells coperte da integration tests
- [ ] Snapshot visual baseline updated + reviewed (10 PNG)
- [ ] E2E happy path passa
- [ ] **Test mix ratio Tier L rispettato**: ~25 unit / ~18 integration / ~5 e2e
- [ ] Bundle delta < +120 KB
- [ ] Matrice migrazione aggiornata (8 rows status `done`)
- [ ] WCAG AA: focus visibile keyboard, role/aria su tablist+tabpanel, axe-core clean
- [ ] **Critical assertion green**: MSW handler in integration tests NEVER receives `'undefined'` or empty string for gameId

---

## Subagent dispatch prompt (for review)

When PR #700 (Phase 0.5 contract) is merged, dispatch implementation subagent with this prompt template:

```
You are implementing Wave C.1 v2 brownfield migration of /games/[id] route.

CRITICAL PREREQUISITES (READ BEFORE WRITING ANY CODE):
1. Read docs/frontend/contracts/games-id-hooks.md ENTIRELY — this is the
   source of truth for hook gating, FSM cells, and component contracts.
2. Read docs/superpowers/plans/2026-05-04-wave-c1-game-detail-implementation.md
   for the 5-task TDD decomposition.
3. The closed PR #697 (branch feature/issue-581-wave-c1-game-detail-fe-v2)
   contains valuable cherry-pick material BUT had a critical gating bug.
   Reference it for foundation/components but REWRITE the orchestrator.

YOUR JOB:
- Execute the 5 tasks in order
- TDD pattern: red → green per task
- Commit after each task with the exact commit message provided
- Branch: feature/issue-581-wave-c1-game-detail-fe-v2-retry from main-dev
- Parent: main-dev

CRITICAL CONTRACT (gating bug from PR #697 — DO NOT REPRODUCE):
- gameId MUST be normalized to string|null at page boundary
- Sub-hooks (useGameAgents) MUST gate cumulatively:
  enabled: !!gameId && detailQuery.isSuccess && tab === 'agents'
- MSW integration tests MUST assert handler never receives 'undefined' string

FSM CELLS CHECKLIST (all 9 cells must have integration test coverage):
[ ] Cell 1: gameId=null → not-found shell, NO sub-hook fetch
[ ] Cell 2: detail loading → loading shell, NO sub-hook fetch
[ ] Cell 3: detail error → error shell, NO sub-hook fetch
[ ] Cell 4: detail success(null) → not-found shell
[ ] Cell 5: detail success(data) + tab≠agents → default render
[ ] Cell 6: detail success + agents loading → inline skeleton
[ ] Cell 7: detail success + agents error → inline banner (NOT shell error)
[ ] Cell 8: detail success + agents empty → empty CTA
[ ] Cell 9: detail success + agents success → grid populated

REPORT after each task:
- Status: DONE | DONE_WITH_CONCERNS | NEEDS_CONTEXT | BLOCKED
- Tests passing count
- Issues encountered + resolution

If BLOCKED on contract ambiguity, STOP and request human review.
DO NOT improvise contract details — every gating decision must be in
the Phase 0.5 contract document.
```

---

## Self-review checklist

After writing this plan, against the spec:

- [x] **Spec coverage**: 5-task decomposition covers all 8 v2 components + orchestrator + e2e + matrix update
- [x] **No placeholders**: every step has explicit file paths, code snippets, commands
- [x] **Type consistency**: `gameId: string | null` enforced uniformly across hook gates, derivation function, component props, integration tests
- [x] **Phase 0.5 alignment**: 9 FSM cells from contract sez. 3 mapped to 18 integration tests in Task 3
- [x] **Anti-pattern documented**: PR #697 gating bug shown explicitly with diff format
- [x] **Tier L test mix**: 50/35/15 ratio ≈ 25/18/5 tests breakdown documented
- [x] **Bundle budget**: ~95 KB target with ~25 KB margin documented
- [x] **CI gates**: all 11 gates listed per spec sez. 4.1 amended

## Execution Handoff

**REQUIRED SUB-SKILL**: superpowers:subagent-driven-development with single-shot dispatch per task (NOT parallel — Wave B.2 race condition lesson per `feedback_subagent-serial-only.md`).

**Pre-execution gate**:
- [ ] PR #700 (Phase 0.5 contract) MERGED into main-dev
- [ ] User explicit authorization to dispatch
- [ ] Branch `feature/issue-581-wave-c1-game-detail-fe-v2-retry` created from `main-dev`

**Post-execution**:
- [ ] All 5 tasks committed to branch
- [ ] PR opened to `main-dev`
- [ ] CI gates green
- [ ] Code review approved
- [ ] Squash merge with `--delete-branch`
- [ ] Memory updated (`session_2026-05-XX_wave-c1-game-detail-merged.md`)

---

**Status**: DRAFT — pending user review of plan before subagent dispatch.

**Approval required for**:
1. Cherry-pick strategy (foundation + components from PR #697 vs full rewrite)
2. Bundle budget target ~95 KB acceptable
3. NO coexistence flag decision (per contract sez. 7)
4. 5-task decomposition vs alternative split
