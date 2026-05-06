# Wave C.2 `/agents/[id]` v2 Brownfield Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to execute task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Prerequisites (non-negotiable)**:
> 1. PR #706 (Phase 0.5 contract `agents-id-hooks.md`) MUST be merged into `main-dev` before dispatch
> 2. Read [`docs/frontend/contracts/agents-id-hooks.md`](../../frontend/contracts/agents-id-hooks.md) entirely — source of truth for FSM cells (10 cells incl. Cell 10 NEW), hook gating, component contracts, variant matrix
> 3. Read Wave C.1 retry plan [`2026-05-04-wave-c1-game-detail-implementation.md`](2026-05-04-wave-c1-game-detail-implementation.md) — pattern blueprint validated end-to-end via PR #702
> 4. Read Wave C.1 implementation as reference: `git show 96d2ea48e` (squash merge) per i 7 commits originali
>
> **Goal:** Brownfield big-bang migration di `/agents/[id]` route da v1 (server component + AgentCharacterSheet legacy) a v2 design (client component + 7 v2 components + 5-tab single-page navigation + variant matrix active/draft/archived) seguendo Phase 0.5 contract con 2-step dependency chain (`agentId → agent.gameId → KB docs`).
>
> **Architecture:** Tier L route per spec sez. 1.2 #5 — orchestrator `AgentDetailViewV2` con hook gating cumulativo (parent + lazy 4 tabs); KB tab gated by `agent.gameId != null` (2-step chain).
>
> **Tech Stack:** Next.js 16 App Router, TanStack Query v5, Vitest + RTL, Playwright (visual + a11y axe-core).

---

## Tier classification

- **Tier**: L (≥3 hooks, cartesian FSM ≥16 celle teoriche, 10 critical celle covered including Cell 10 NEW)
- **Coexistence flag**: NO (decisione contract sez. 7 — app pre-prod, big-bang OK)
- **Bundle budget**: target ~90 KB (margin 30 KB su +120 KB Tier L cap)
- **Test mix ratio**: 50% unit / 35% integration / 15% e2e (~30 + 20 + 5 tests)
- **Effort estimate**: 7-9 giorni (vs Wave C.1 retry 6-8 gg, +1 gg per variant matrix + 2-step chain complexity)

## Wave C.2 unique vs Wave C.1

| Aspect | Wave C.1 | Wave C.2 |
|--------|----------|----------|
| Hook count (orchestrator) | 1 parent + 1 lazy | 1 parent + **4 lazy** |
| Tabs count | 6 | 5 (Identity/Knowledge/Performance/History/Settings) |
| Sub-resource chain | Direct `gameId` | **2-step** `agentId → agent.gameId` |
| Page component | Already client | **Server → client conversion** |
| Variant matrix | Single | **3-state** (active/draft/archived) |
| FSM cells | 9 | **10** (+Cell 10 NEW for standalone agent) |

## File structure

### Files to CREATE

```
apps/web/src/lib/agents/
  agent-detail-state.ts                          # FSM state derivation (mirror games/game-detail-state.ts)
  agent-detail-variant.ts                        # active/draft/archived derivation
  agent-detail-visual-test-fixture.ts            # Visual fixture sentinel

apps/web/src/components/v2/agent-detail/         # 7 components — IMPLEMENT existing stubs
  AgentHero.tsx                                   # NEW (was AgentCharacterSheet stub — replaced)
  AgentTabs.tsx                                   # NEW (or reuse GameDetailTabsAnimated as primitive)
  PersonaCard.tsx                                 # implement existing stub
  SystemPromptViewer.tsx                          # implement existing stub
  KbDocList.tsx                                   # implement existing stub (5-state union)
  ChatHistoryTimeline.tsx                         # implement existing stub
  AgentSettingsForm.tsx                           # implement existing stub (variant-aware)
  AgentDangerZone.tsx                             # implement existing stub (active variant only)
  index.ts                                        # Barrel exports
  __tests__/                                      # Unit tests per component
    AgentHero.test.tsx
    AgentTabs.test.tsx (or reuse pattern)
    PersonaCard.test.tsx
    SystemPromptViewer.test.tsx
    KbDocList.test.tsx (5-state coverage)
    ChatHistoryTimeline.test.tsx
    AgentSettingsForm.test.tsx (variant matrix)
    AgentDangerZone.test.tsx

apps/web/src/app/(authenticated)/agents/[id]/_components/
  AgentDetailViewV2.tsx                           # Orchestrator
  __tests__/
    AgentDetailViewV2.test.tsx                    # Integration tests (~20 tests, all 10 FSM cells)

apps/web/src/hooks/queries/  (if needed — verify existing)
  useAgent.ts                                     # NEW if not exists; wraps api.agents.getById
  useAgentPerformance.ts                          # NEW (TBD endpoint or derived from agent.invocationCount)
  # Existing: useAgentKbDocs, useAgentThreads, useAgentConfig (verified in contract sez. 2)

apps/web/e2e/visual-migrated/sp4-agent-detail.spec.ts
apps/web/e2e/v2-states/agent-detail.spec.ts
apps/web/e2e/a11y/agent-detail.spec.ts
apps/web/e2e/smoke-real-backend/agent-detail.smoke.spec.ts

apps/web/src/locales/{it,en}.json                  # +~60 keys/locale (more than C.1 due to variant + 5 tabs)
```

### Files to MODIFY

```
apps/web/src/app/(authenticated)/agents/[id]/page.tsx
  CONVERT: server component → client component
  REPLACE: notFound() server-side + AgentCharacterSheet legacy
  → thin shell: useParams + AgentDetailViewV2

apps/web/src/app/(authenticated)/agents/[id]/error.tsx
  KEEP: Next.js error boundary (no change)

docs/frontend/v2-migration-matrix.md
  UPDATE: 7 agent-detail rows status pending → done, PR ref → #N
```

### Files to PRESERVE unchanged

```
apps/web/src/app/(authenticated)/agents/page.tsx — Wave B.2 already migrated (PR #637)
apps/web/src/components/agent/AgentCharacterSheet.tsx — legacy v1, kept for backward compat (deprecated)
```

---

## Task decomposition (5-commit TDD pattern, mirror Wave C.1)

Each commit independently testable. After each commit, all existing tests must remain green. **Branch from `main-dev` after PR #706 merge**.

### Task 1: Foundation — i18n + visual fixture + FSM + variant derivation

**Files:**
- Create: `apps/web/src/lib/agents/agent-detail-state.ts`
- Create: `apps/web/src/lib/agents/agent-detail-variant.ts`
- Create: `apps/web/src/lib/agents/agent-detail-visual-test-fixture.ts`
- Modify: `apps/web/src/locales/it.json` (+60 keys)
- Modify: `apps/web/src/locales/en.json` (+60 keys)
- Test: `apps/web/src/lib/agents/__tests__/agent-detail-state.test.ts`
- Test: `apps/web/src/lib/agents/__tests__/agent-detail-variant.test.ts`
- Test: `apps/web/src/lib/agents/__tests__/agent-detail-visual-test-fixture.test.ts`

**Critical contract enforcement** (mirror Wave C.1 + Cell 10):

```ts
// agent-detail-state.ts — REQUIRED
export type AgentDetailUiState = 'loading' | 'error' | 'not-found' | 'default';

export interface DeriveAgentDetailUiStateInput {
  agentId: string | null;  // ⚠️ MUST be string|null, NEVER undefined
  isLoading: boolean;
  isError: boolean;
  hasData: boolean;
}

export function deriveAgentDetailUiState(input: DeriveAgentDetailUiStateInput): AgentDetailUiState {
  if (input.agentId == null) return 'not-found';   // Cell 1 (CRITICAL FIRST CHECK)
  if (input.isLoading) return 'loading';            // Cell 2
  if (input.isError) return 'error';                // Cell 3
  if (!input.hasData) return 'not-found';           // Cell 4
  return 'default';                                  // Cells 5-10
}

// agent-detail-variant.ts — Wave C.2 NEW
export type AgentVariant = 'active' | 'draft' | 'archived';

export function deriveAgentVariant(agent: AgentDto): AgentVariant {
  if (agent.archivedAt != null) return 'archived';
  if (agent.systemPrompt == null) return 'draft';
  return 'active';
}
```

**Test plan (~25 unit tests)**:

- [ ] **Step 1.1**: Property test — `agentId === null` returns `'not-found'` for ALL 8 combinations of isLoading/isError/hasData
- [ ] **Step 1.2-1.5**: Cell 2/3/4/5 derivation tests (4 tests)
- [ ] **Step 1.6-1.9**: Variant matrix — active/draft/archived/edge cases (4 tests)
- [ ] **Step 1.10**: Visual fixture loaders (default + standalone Cell 10 + null) (3 tests)
- [ ] **Step 1.11**: i18n key parity it/en + namespace `pages.agentDetail.*`
- [ ] **Step 1.12**: Run `pnpm typecheck` → expect green
- [ ] **Step 1.13**: Run `pnpm test src/lib/agents` → expect 25+ tests passing

**Commit message**:
```
feat(agent-detail): foundation — FSM derivation + variant + visual fixture + i18n (Wave C.2)

Phase 0.5 contract enforced:
- deriveAgentDetailUiState: agentId === null short-circuit FIRST (Cell 1)
- deriveAgentVariant: active/draft/archived from agent.archivedAt + agent.systemPrompt
- Visual fixture: 'default' (active variant) + 'standalone' (Cell 10 — gameId=null) + 'not-found'
- i18n: ~60 keys/locale for 5 tabs + variant banners + tab content

All FSM cells documented in docs/frontend/contracts/agents-id-hooks.md sez. 3.

Refs #581 (Wave C umbrella).
```

---

### Task 2: 7 v2 components implementation

**Files:**
- Implement existing stubs in `apps/web/src/components/v2/agent-detail/{AgentHero,AgentTabs,PersonaCard,SystemPromptViewer,KbDocList,ChatHistoryTimeline,AgentSettingsForm,AgentDangerZone}.tsx`
- Create: `apps/web/src/components/v2/agent-detail/index.ts`
- Create: 8 unit test files

**Component contracts** (per Phase 0.5 sez. 4):

#### 2.1 `AgentHero` (variant matrix)

```ts
type AgentHeroVariant = 'active' | 'draft' | 'archived';

interface AgentHeroProps {
  variant: AgentHeroVariant;
  name: string;
  avatar: string;  // emoji or imageUrl
  persona: string | null;
  meta: AgentHeroMeta;  // type/model/createdAt/lastUsed/invocations
  ctaPlay?: () => void;       // active variant
  ctaSetup?: () => void;       // draft variant
  ctaUnarchive?: () => void;   // archived variant
  ctaShare?: () => void;
  labels: AgentHeroLabels;
}
```

⚠️ **A11y CTA contrast pre-emption** (Wave C.1 lesson): use 700-shade Tailwind for white text:
- `bg-emerald-700` (active CTA Play)
- `bg-amber-700` (draft CTA Setup)
- `bg-violet-700` (archived CTA Unarchive — entity agent purple)

#### 2.2 `AgentTabs` (5 tabs, animated underline)

Either reuse Wave C.1 `GameDetailTabsAnimated` or create equivalent. Decision in implementation: prefer reuse if the abstraction can absorb 5-vs-6 tab variation. Same a11y contract (`role="tablist"`, `role="tab"`, etc.).

#### 2.3 `KbDocList` (5-state discriminated union — CRITICAL Cell 10 distinct from Cell 8)

```ts
type KbDocsState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'empty' }                // Cell 8: gameId valid, no docs
  | { kind: 'standalone' }            // Cell 10: agent.gameId === null
  | { kind: 'success'; docs: KbDocEntry[] };

interface KbDocListProps {
  state: KbDocsState;
  labels: KbDocListLabels;
}
```

#### 2.4 Other components

- `PersonaCard`, `SystemPromptViewer`: pure display
- `ChatHistoryTimeline`: discriminated union (loading/error/empty/success)
- `AgentSettingsForm`: variant-aware (editable for active, read-only for archived)
- `AgentDangerZone`: only renders when variant === 'active'

**Test plan (~35 unit tests, mirror Wave C.1's 57)**:

- [ ] **Step 2.1**: AgentHero — 6 tests (variant matrix × CTA presence)
- [ ] **Step 2.2**: AgentTabs — 5 tests (a11y, keyboard nav)
- [ ] **Step 2.3**: PersonaCard — 3 tests
- [ ] **Step 2.4**: SystemPromptViewer — 3 tests
- [ ] **Step 2.5**: KbDocList — **6 tests** (5 kinds + render shape)
- [ ] **Step 2.6**: ChatHistoryTimeline — 4 tests
- [ ] **Step 2.7**: AgentSettingsForm — 6 tests (variant editable/read-only)
- [ ] **Step 2.8**: AgentDangerZone — 2 tests (active variant only)
- [ ] **Step 2.9**: Run `pnpm test src/components/v2/agent-detail` → expect 35+ tests green

**Commit message**:
```
feat(agent-detail): 7 v2 components implementation (Wave C.2)

Pure presentation components per Phase 0.5 contract sez. 4:
- AgentHero (variant active/draft/archived CTA matrix; 700-shade Tailwind for white text)
- AgentTabs (a11y tablist + useTablistKeyboardNav reuse, PR #623)
- PersonaCard, SystemPromptViewer (pure display)
- KbDocList (5-state discriminated union INCLUDING standalone Cell 10)
- ChatHistoryTimeline (4-state discriminated union)
- AgentSettingsForm (variant-aware editable/read-only)
- AgentDangerZone (active variant only)

All components PURE — orchestrator passes resolved data. All have data-slot for E2E.
A11y CTA contrast pre-empted (Wave C.1 hotfix lesson — 600-shades fail WCAG AA 4.5:1).

Refs #581.
```

---

### Task 3: Orchestrator + page wiring (server → client conversion CRITICAL)

**Files:**
- Create: `apps/web/src/app/(authenticated)/agents/[id]/_components/AgentDetailViewV2.tsx`
- Create: `apps/web/src/app/(authenticated)/agents/[id]/_components/__tests__/AgentDetailViewV2.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/agents/[id]/page.tsx` (server → client conversion)
- Delete: any legacy v1 surface tests if present

**CRITICAL: Server → Client conversion** (page.tsx)

```tsx
// BEFORE (legacy server component)
export default async function AgentPage({ params }: { params: { id: string } }) {
  let agent;
  try {
    agent = await api.agents.getById(params.id);
  } catch { notFound(); }
  if (!agent) notFound();
  // ... render AgentCharacterSheet
}

// AFTER (Wave C.2 client thin shell)
'use client';
import { useParams } from 'next/navigation';
import { AgentDetailViewV2 } from './_components/AgentDetailViewV2';

export default function AgentPage() {
  const params = useParams<{ id: string }>();
  const rawId = params?.id;
  const agentId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

  return <AgentDetailViewV2 agentId={agentId} />;
}
```

**Critical orchestrator gating** (full sub-hook chain per contract sez. 2.2):

```tsx
const agentQuery = useAgent(agentId);  // PARENT

// Knowledge tab — 2-STEP CHAIN (Cell 10 awareness)
const kbDocsQuery = useAgentKbDocs({
  gameId: agentQuery.data?.gameId ?? null,
  enabled:
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null &&         // Cell 4 guard
    agentQuery.data.gameId != null &&  // Cell 10 guard (2-step chain)
    tab === 'knowledge',
});

// History tab
const threadsQuery = useAgentThreads({
  agentId,
  enabled:
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null &&
    tab === 'history',
});

// Settings tab
const configQuery = useAgentConfig({
  agentId,
  enabled:
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null &&
    tab === 'settings',
});

// KbDocList state mapper
function mapKbDocsState(
  query: typeof kbDocsQuery,
  agentGameId: string | null
): KbDocsState {
  if (agentGameId == null) return { kind: 'standalone' };  // Cell 10
  if (query.isLoading) return { kind: 'loading' };
  if (query.isError) return { kind: 'error', retry: () => query.refetch() };
  if (query.data?.length === 0) return { kind: 'empty' };
  return { kind: 'success', docs: query.data ?? [] };
}
```

**Variant rendering** (3-state matrix):

```tsx
const variant = useMemo(
  () => (agentQuery.data ? deriveAgentVariant(agentQuery.data) : 'active'),
  [agentQuery.data]
);

// Tab availability per variant:
// - active: all 5 tabs enabled
// - draft: identity + knowledge + settings; performance/history LOCKED
// - archived: all read-only, danger zone hidden
```

**Test plan (20 integration tests covering all 10 FSM cells)**:

- [ ] **Step 3.1**: Cell 1 — `agentId={null}` → not-found shell, NO sub-hook calls
- [ ] **Step 3.2**: Cell 2 — agent loading → loading shell, sub-hooks NOT enabled
- [ ] **Step 3.3**: Cell 3 — agent error → error shell, retry CTA wired
- [ ] **Step 3.4**: Cell 4 — agent success(null) → not-found shell (distinct from Cell 1)
- [ ] **Step 3.5**: Cell 5 — tab='identity' → default render, NO sub-hook calls
- [ ] **Step 3.6**: Cell 5→6 transition — tab change identity→knowledge fires KB fetch
- [ ] **Step 3.7**: Cell 6 — knowledge tab loading → inline skeleton
- [ ] **Step 3.8**: Cell 7 — knowledge tab error → inline banner (NO shell error)
- [ ] **Step 3.9**: Cell 8 — knowledge tab empty → empty CTA
- [ ] **Step 3.10**: Cell 9 — knowledge tab success → grid populated
- [ ] **Step 3.11**: **Cell 10 NEW** — agent.gameId=null + tab=knowledge → standalone empty state, KB fetch NOT fired
- [ ] **Step 3.12-3.14**: Same matrix for History tab (Cells 6-9 history flavor) — 3 tests
- [ ] **Step 3.15-3.17**: Same matrix for Settings tab — 3 tests
- [ ] **Step 3.18**: Variant=archived → settings tab read-only, danger zone hidden
- [ ] **Step 3.19**: Variant=draft → performance/history tabs LOCKED with banner
- [ ] **Step 3.20**: ⚠️ **CRITICAL MSW assertion**: kb-docs handler NEVER receives `'undefined'`, `'null'`, empty string for gameId across ALL cells

**Critical assertion helper** (mirror Wave C.1):

```ts
function assertKbDocsNeverCalledWithBadGameId() {
  const enabledCalls = kbDocsHandlerSpy.mock.calls.filter(([req]) => {
    const url = new URL(req.url);
    return url.searchParams.get('gameId') != null;
  });
  for (const [req] of enabledCalls) {
    const url = new URL(req.url);
    const gameId = url.searchParams.get('gameId') ?? '';
    expect(gameId).not.toBe('undefined');
    expect(gameId).not.toBe('null');
    expect(gameId).not.toBe('');
    expect(gameId).toMatch(/^[a-f0-9-]{36}$/);
  }
}
```

**Commit message**:
```
feat(agent-detail): orchestrator + page wiring server→client (Wave C.2)

AgentDetailViewV2 implements Phase 0.5 contract:
- Page.tsx CONVERTED server → client component (mirror Wave C.1 page.tsx pattern)
- agentId normalized to string|null at page boundary
- useAgent (parent) gated by !!agentId
- 4 lazy sub-hooks gated cumulatively: !!agentId && agentQuery.isSuccess && data != null && tab === 'X'
- Knowledge tab 2-STEP CHAIN: also gated by data.gameId != null (Cell 10 guard)
- 5-state FSM via deriveAgentDetailUiState
- Variant matrix active/draft/archived → tab availability + read-only behavior
- Visual fixture short-circuit for CI prod build (default + standalone fixtures)
- ?state= URL override (dev + visual-test only)

20 integration tests cover 10 FSM cells (incl. Cell 10 NEW), with critical
MSW assertion: kb-docs handler MUST never receive 'undefined' or empty string.

Removes legacy AgentCharacterSheet rendering (kept as deprecated component
for backward compat, no longer used by /agents/[id] route).

Refs #581.
```

---

### Task 4: E2E specs (visual + v2-states + a11y + smoke)

**Files:**
- Create: `apps/web/e2e/visual-migrated/sp4-agent-detail.spec.ts` — 4 PNG (active variant + draft variant × 2 viewports)
- Create: `apps/web/e2e/v2-states/agent-detail.spec.ts` — 5 stati × 2 viewports = 10 PNG (4 base + Cell 10 standalone)
- Create: `apps/web/e2e/a11y/agent-detail.spec.ts` — axe-core WCAG 2.1 AA + reduced-motion
- Create: `apps/web/e2e/smoke-real-backend/agent-detail.smoke.spec.ts` — deterministic UUID

**Critical anti-patterns to avoid (Wave C.1 lessons)**:
- ❌ `waitUntil: 'networkidle'` → use `'domcontentloaded'` + explicit `waitForSelector('[data-slot="agent-detail-view"]')`
- ❌ Tailwind `bg-emerald-600`/`bg-amber-600`/`bg-violet-600` with white text → use 700 shades (PRE-EMPTED in Task 2)
- ❌ Skipping next.config.js redirect audit → checked in pre-implementation gate (audit checklist §8)

**Test plan**:

- [ ] **Step 4.1**: visual-migrated spec with `IS_VISUAL_TEST_BUILD` fixture: active variant baseline + draft variant baseline (2 PNG × 2 viewports = 4)
- [ ] **Step 4.2**: v2-states spec with `?state=` URL override: 5 states (default-active/loading/error/not-found/empty=standalone) × 2 viewports = 10 PNG
- [ ] **Step 4.3**: a11y spec: axe scan default + axe scan archived variant + reduced-motion contract
- [ ] **Step 4.4**: smoke spec: deterministic UUID + optional SMOKE_AGENT_ID env path
- [ ] **Step 4.5**: Run `pnpm test:e2e --project=v2-states e2e/v2-states/agent-detail.spec.ts` → expect 10 PNG bootstrap
- [ ] **Step 4.6**: Run `pnpm test:e2e --project=a11y e2e/a11y/agent-detail.spec.ts` → expect 0 violations

**Commit message**:
```
test(agent-detail): e2e visual-migrated + v2-states + a11y + smoke (Wave C.2)

- visual-migrated/sp4-agent-detail.spec.ts: 4 PNG (active + draft × 2 viewports)
- v2-states/agent-detail.spec.ts: 5 stati × 2 viewport = 10 PNG (incl. Cell 10 standalone)
- a11y/agent-detail.spec.ts: axe-core WCAG 2.1 AA + reduced-motion
- smoke-real-backend/agent-detail.smoke.spec.ts: deterministic UUID

Covers all 10 FSM cells from Phase 0.5 contract (4 via URL override, 6 via real fixture).

Refs #581.
```

---

### Task 5: Visual baselines bootstrap + matrix update + PR

**Files:**
- Add: `apps/web/e2e/visual-migrated/__snapshots__/sp4-agent-detail/*.png` (4 PNG)
- Add: `apps/web/e2e/v2-states/__snapshots__/agent-detail/*.png` (10 PNG)
- Modify: `docs/frontend/v2-migration-matrix.md` (7 agent-detail rows pending → done, PR ref → #N)

**Test plan**:

- [ ] **Step 5.1**: ⚠️ **PRE-FLIGHT AUDIT (Wave C.1 lesson saved ~30min)**:
  - `grep -n "/agents/:id\|/agents/.*destination" apps/web/next.config.js` — verify no redirects
  - `grep -n "agents" apps/web/src/proxy.ts` — verify no rewrites
- [ ] **Step 5.2**: Trigger workflow `visual-regression-migrated.yml` mode=bootstrap
- [ ] **Step 5.3**: Download generated baselines from CI artifact
- [ ] **Step 5.4**: Commit 14 PNG canonical Linux x86-64 (4 visual-migrated + 10 v2-states)
- [ ] **Step 5.5**: Update matrix rows in `docs/frontend/v2-migration-matrix.md`
- [ ] **Step 5.6**: Run full E2E suite locally → expect all green

**Commit message**:
```
chore(agent-detail): bootstrap visual baselines + matrix update (Wave C.2)

- 14 PNG canonical Linux x86-64 from workflow visual-regression-migrated.yml
  bootstrap run: 4 visual-migrated (active+draft × 2 viewports) + 10 v2-states
  (5 stati × 2 viewports incl. Cell 10 standalone)
- Migration matrix: 7 agent-detail rows pending → done, PR ref #N

Refs #581.
```

---

## CI gates (per spec sez. 4.1)

Tutti **OBBLIGATORI** verdi prima di merge:

- [ ] Typecheck (`pnpm typecheck`)
- [ ] Lint (`pnpm lint` — incl. no-hardcoded-hex rule)
- [ ] Vitest unit (target ≥85%, ratio Tier L 50% = ~30 unit tests)
- [ ] Vitest integration (~20 tests via renderHook + MSW)
- [ ] Playwright E2E happy path
- [ ] Playwright visual-migrated regression (4 PNG)
- [ ] Playwright v2-states (5 stati × 2 viewport = 10 PNG)
- [ ] Playwright a11y (axe-core WCAG 2.1 AA, zero violations)
- [ ] Bundle size delta < +120 KB (Tier L cap, target ~90 KB)
- [ ] GitGuardian (no secret pattern)
- [ ] Smoke E2E real-backend (manual gate `workflow_dispatch`, post-merge nightly)

## Definition of Done (page-level, per spec sez. 4.1 amended + Wave C.1 retry validation)

- [ ] Page legacy server component rimossa, sostituita da thin client shell
- [ ] AgentCharacterSheet legacy NOT used by `/agents/[id]` (kept as deprecated)
- [ ] Page v2 implementata 1:1 con mockup `sp4-agent-detail.jsx`
- [ ] **Phase 0.5 contract validated post-implementation**: tutte 10 FSM cells coperte (incl. Cell 10 standalone)
- [ ] **Variant matrix coverage**: active/draft/archived tested in integration + visual
- [ ] Snapshot visual baseline updated + reviewed (14 PNG)
- [ ] E2E happy path passa
- [ ] **Test mix ratio Tier L rispettato**: ~30 unit / ~20 integration / ~5 e2e
- [ ] Bundle delta < +120 KB
- [ ] Matrice migrazione aggiornata (7 rows `done`)
- [ ] WCAG AA: focus visibile keyboard, role/aria su tablist+tabpanel, axe-core clean
- [ ] **Critical assertion green**: MSW handler in integration tests NEVER receives `'undefined'`/`'null'`/empty string for gameId
- [ ] **Pre-flight audit pass**: no redirects/rewrites intercept `/agents/[id]` (Wave C.1 lesson)

---

## Subagent dispatch prompt template (for review)

When PR #706 is merged, dispatch implementation subagent with this template:

```
You are implementing Wave C.2 v2 brownfield migration of /agents/[id] route.

CRITICAL PREREQUISITES (READ BEFORE WRITING ANY CODE):
1. Read docs/frontend/contracts/agents-id-hooks.md ENTIRELY — source of truth
   for hook gating, FSM cells (10 cells incl. Cell 10 NEW), component contracts,
   variant matrix.
2. Read docs/superpowers/plans/2026-05-05-wave-c2-agent-detail-implementation.md
   for the 5-task TDD decomposition.
3. Reference Wave C.1 retry merged PR #702 (squash 96d2ea48e) as battle-tested
   pattern. The contract template `agents-id-hooks.md` mirrors Wave C.1's
   `games-id-hooks.md` with 2-step chain extension (Cell 10).

YOUR JOB:
- Execute the 5 tasks in order
- TDD pattern: red → green per task
- Commit after each task with the exact commit message provided
- Branch: feature/issue-581-wave-c2-agent-detail-fe-v2 from main-dev
- Parent: main-dev

CRITICAL CONTRACT (extends Wave C.1 lessons):
- agentId MUST be normalized to string|null at page boundary
- ⚠️ Page.tsx is currently SERVER COMPONENT. Convert to CLIENT thin shell.
- Sub-hooks (useAgentKbDocs, useAgentThreads, useAgentConfig) MUST gate
  cumulatively: !!agentId && agentQuery.isSuccess && data != null && tab === 'X'
- Knowledge tab additional 2-STEP guard: also data.gameId != null
- Tailwind CTA white-text: use 700-shades (NOT 600)
- MSW integration tests MUST assert handlers never receive 'undefined' string
- Pre-implementation audit: redirect cleanup + proxy.ts rewrite check (Wave C.1
  saved ~30min by adding this checklist)

FSM CELLS CHECKLIST (all 10 cells must have integration test coverage):
[ ] Cell 1: agentId=null → not-found shell, NO sub-hook fetch
[ ] Cell 2: agent loading → loading shell, NO sub-hook fetch
[ ] Cell 3: agent error → error shell, NO sub-hook fetch
[ ] Cell 4: agent success(null) → not-found shell
[ ] Cell 5: agent success(data) + tab='identity' → default render
[ ] Cell 6: agent success + Knowledge/History/Settings tab loading → inline skeleton
[ ] Cell 7: agent success + active tab error → inline banner (NOT shell error)
[ ] Cell 8: agent success + active tab empty → empty CTA
[ ] Cell 9: agent success + active tab success → populated
[ ] Cell 10 NEW: agent.gameId=null + tab=knowledge → standalone empty state
              (Knowledge tab fetch NEVER fired)

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

After writing the complete plan:

**1. Spec coverage**: ✅ All 10 FSM cells (incl. Cell 10) mapped to integration tests in Task 3
**2. Placeholder scan**: No "TBD"/"TODO" markers in critical implementation paths
**3. Type consistency**: `agentId: string | null` enforced uniformly across hooks, derivation, props, tests
**4. Phase 0.5 alignment**: Sub-hook gating contracts directly quoted from contract sez. 2.2
**5. Wave C.1 lessons applied**: Audit checklist (§8 contract) + pre-emptive a11y CTA fix + redirect cleanup gotcha referenced
**6. Tier L test mix**: 50/35/15 ratio ≈ 30/20/5 tests breakdown documented
**7. Bundle budget**: ~90 KB target with ~30 KB margin documented
**8. CI gates**: all 11 gates listed per spec sez. 4.1 amended

## Execution Handoff

**REQUIRED SUB-SKILL**: superpowers:subagent-driven-development with single-shot dispatch per task (NOT parallel — Wave B.2 race condition lesson).

**Pre-execution gate**:
- [ ] PR #706 (Phase 0.5 contract `agents-id-hooks.md`) MERGED into main-dev
- [ ] User explicit authorization to dispatch
- [ ] Branch `feature/issue-581-wave-c2-agent-detail-fe-v2` created from `main-dev`
- [ ] Pre-flight audit completed (redirect cleanup + proxy rewrite check)

**Post-execution**:
- [ ] All 5 tasks committed to branch
- [ ] PR opened to `main-dev`
- [ ] CI gates green (use admin override if pre-existing E2E DB flake recurs)
- [ ] Code review approved
- [ ] Squash merge with `--delete-branch`
- [ ] Memory updated (`session_2026-05-XX_wave-c2-agent-detail-merged.md`)

---

**Status**: DRAFT — pending user review of plan + contract before subagent dispatch.

**Approval required for**:
1. Cherry-pick strategy (NEW from scratch since /agents/[id] page is server component, OR partial reuse from existing AgentCharacterSheet/v1 components)
2. Bundle budget target ~90 KB acceptable
3. NO coexistence flag decision (per contract sez. 7)
4. 5-task decomposition vs alternative split
5. Subagent dispatch prompt template OK to use as-is
