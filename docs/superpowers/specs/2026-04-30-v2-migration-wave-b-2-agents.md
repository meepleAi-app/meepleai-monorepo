# V2 Migration · Wave B.2 — `/agents` Brownfield Spec

**Issue**: #634 (parent: #580 Wave B umbrella · #578 Phase 1)
**Branch**: `feature/issue-634-agents-fe-v2` (parent: `main-dev`)
**Mockup**: `admin-mockups/design_files/sp4-agents-index.jsx` (908 LOC)
**Pilot reference**: `2026-04-29-v2-migration-wave-b-1-games.md` (PR #635 MERGED) — brownfield grid pattern + visual-test fixture sentinel + 5-commit TDD decomposition
**Companion exec plan**: `2026-04-27-v2-migration-phase1-execution.md` §4 (Wave B)
**Wave B.1 prerequisites** (✅ all merged): a11y CI gate (#601 → PR #602), brand-orange contrast fix (#587 → PR #602), `useTablistKeyboardNav` hook (PR #623 + orientation #626), per-route bundle-size budget (#629 → PR #631), V2 migration matrix (#573 → PR #632), Wave B.1 `/games?tab=library` (#633 → PR #635 — pattern parent)
**Status**: REVIEWED 2026-04-30 — spec-panel ultrathink applied 3 BLOCKER (resolved via `b-1/b-2/b-3 si`) + 4 NEW BLOCKERs discovered during reconnaissance (backend route gap + DTO field gaps) + short/long-term findings

---

## 1. Goals

1. **Migrare la route `/agents`** (`apps/web/src/app/(authenticated)/agents/page.tsx`) al pattern v2 `sp4-agents-index` — secondo brownfield migration di Wave B (mirror B.1 pattern, B.1 era `/games?tab=library`).
2. **Riusare workflow** validato in Wave A/B.1: visual-migrated + v2-states + bootstrap baselines via CI, hybrid masking, cookie-consent suppression via `addInitScript` (lesson learned A.4 + B.1), `domcontentloaded` waitUntil (B.1 Iter 1 lesson), `data-slot` exclusion per pre-existing EntityBadge debt #636 (B.1 Iter 2 lesson).
3. **Estrarre 4 componenti v2** in `apps/web/src/components/v2/agents/` (tracking matrix `docs/frontend/v2-migration-matrix.md`):
   - `AgentsHero` (mockup `AgentsHero`) — header con gradient agent + count agenti + CTA "Crea agente"
   - `AgentFilters` (mockup `AgentFilters`, **rinominato da `AgentsFiltersStrip` per allineamento mockup**) — sticky filter bar con search + status segmented tablist + sort dropdown
   - `AgentsResultsGrid` (mockup `AgentCardGrid` consumer) — grid che riusa `MeepleCard` v1 senza fork
   - `EmptyAgents` (mockup `EmptyAgents` + `ErrorState`) — discriminated `kind: 'empty' | 'filtered-empty' | 'error' | 'loading'`
4. **Riusare `MeepleCard` v1** con `entity="agent"` e `variant="grid"` per le card — ZERO fork, mandate da CLAUDE.md "Card Components" (mirror B.1 AC-3).
5. **Riusare `useTablistKeyboardNav`** (PR #623) per il status segmented tablist (orientation `horizontal`).
6. **Drop `AdvancedFiltersDrawer`** (BLOCKER #2 risolto via decisione utente `b-2 si`): rimosso TOTALMENTE da scope Wave B (B.2 + umbrella DoD #580). Non più stub placeholder. Scope reduction definitiva.
7. **i18n bilingue IT+EN** (~35 keys nel namespace `pages.agents`) — nessuna stringa hard-coded.
8. **Bundle budget gate** (`frontend-bundle-size`): target Δ ≤ 35 KB First Load JS per `/agents`, hard limit +50 KB (parità B.1).

## 2. Non-goals

- **`AgentsSidebarList` + `AgentDetailPanel`**: mockup `sp4-agents-index` NON definisce master-detail layout — è grid pattern. Rimossi da scope `/agents` per `b-1 si`. Restano stub disk per orphan cleanup separato post-Wave B (matrix update già committato in `5d75ca4b5`).
- **`AdvancedFiltersDrawer`**: dropped totalmente dallo scope Wave B per `b-2 si`. Mockup non lo definisce, no follow-up issue.
- **`AgentCreationSheet` migration**: il modal "Crea Agente" esistente in `apps/web/src/app/(authenticated)/agents/page.tsx:218-227` resta v1. Hero CTA "+ Crea agente" v2 invoca lo stesso modal v1 (no UI change). Modal v2 redesign è candidate Wave B.4+.
- **Agent slot indicator** (`/api/v1/user/agent-slots` per Issue #417): resta nel page header v1 oggi a `apps/web/src/app/(authenticated)/agents/page.tsx:233-235`. Nello scope B.2 viene **portato dentro `AgentsHero` come stat counter** se `useUserAgentSlots()` è disponibile (vedi §3.4 verification). Se non praticabile, Hero mostra solo `count` (parità mockup).
- **Backend changes**: nessuna modifica a domain/handler. Tutta la logica filter+sort+derivation status è client-side (vedi §3.4 BLOCKERs).
- **Backend route registration `GET /api/v1/agents`**: vedi §3.4 R3 — recommended Option B (frontend ship with visual-test fixture, file follow-up backend issue). Backend extension è out-of-scope Wave B.2.
- **`MeepleCard` extension**: niente prop nuove. Se serve customization (top accent bar agent gradient, entity badge per agent), si fa via wrapper o si defere a v2 spec dedicato `MeepleCard`.
- **`useAgents` hook refactor**: si riusa as-is (`apps/web/src/hooks/queries/useAgents.ts`, 16 LOC). Eventuali estensioni vanno in B.4+ catalog.
- **Server Component conversion**: la pagina rimane `'use client'` come oggi (existing brownfield, già client). SSR seed via TanStack Query è out-of-scope.
- **Progressive reveal/infinite scroll**: l'esistente `useInfiniteScroll` con `PAGE_SIZE = 12` resta per backward-compat. UI v2 mockup non lo richiede esplicitamente — verifica se renderizzare tutti gli agenti (count attesto < 50 per user) o mantenere progressive reveal. Decisione: **mantenere progressive reveal** se >12 agenti, altrimenti rendering full grid (parità mockup).

## 3. Architecture

### 3.1 File map

| Tipo | Path | Status |
|------|------|--------|
| Page (existing) | `apps/web/src/app/(authenticated)/agents/page.tsx` | **Edit** (split body in `<AgentsLibraryView>` orchestrator, mantenere modal `AgentCreationSheet`) |
| New view | `apps/web/src/app/(authenticated)/agents/_components/AgentsLibraryView.tsx` | **Create** (orchestrator: state FSM + filtered data) |
| View test | `apps/web/src/app/(authenticated)/agents/_components/__tests__/AgentsLibraryView.test.tsx` | **Create** |
| Component | `apps/web/src/components/v2/agents/AgentsHero.tsx` | **Edit** (stub → impl) |
| Component | `apps/web/src/components/v2/agents/AgentsFiltersStrip.tsx` | **Rename → `AgentFilters.tsx`** + impl. Path discipline: stub esistente → rinominato mantenendo path `apps/web/src/components/v2/agents/`. Nota: NO impatto su altri consumer (stub returns null, zero usage). |
| Component | `apps/web/src/components/v2/agents/AgentsResultsGrid.tsx` | **Create** (file nuovo, no stub esistente) |
| Component | `apps/web/src/components/v2/agents/EmptyAgents.tsx` | **Edit** (stub → impl) |
| Component | `apps/web/src/components/v2/agents/AgentsSidebarList.tsx` | **Skip** (orphan stub, cleanup post-Wave B) |
| Component | `apps/web/src/components/v2/agents/AgentDetailPanel.tsx` | **Skip** (orphan stub, cleanup post-Wave B) |
| Component index | `apps/web/src/components/v2/agents/index.ts` | **Create** (barrel: hero, filters, results, empty) |
| Component tests | `apps/web/src/components/v2/agents/__tests__/{AgentsHero,AgentFilters,AgentsResultsGrid,EmptyAgents}.test.tsx` | **Create** (4 test files) |
| Filter helper | `apps/web/src/lib/agents/library-filters.ts` | **Create** (pure helpers: `filterByStatus`, `filterByQuery`, `sortAgents`) |
| Status derivation | `apps/web/src/lib/agents/derive-status.ts` | **Create** (pure: `(agent: AgentDto) => 'attivo' \| 'in-setup' \| 'archiviato'`, vedi §3.4 BLOCKER B-NEW-2) |
| Helper tests | `apps/web/src/lib/agents/__tests__/{library-filters,derive-status}.test.ts` | **Create** (pure unit, fast) |
| Visual test fixture | `apps/web/src/lib/agents/visual-test-fixture.ts` | **Create** (sentinel pattern A.4/B.1: `IS_VISUAL_TEST_BUILD` env-gated SSR-safe agents mock) |
| i18n IT | `apps/web/src/locales/it.json` § `pages.agents` | **Add** (~35 keys) |
| i18n EN | `apps/web/src/locales/en.json` § `pages.agents` | **Add** (~35 keys) |
| Visual test | `apps/web/e2e/visual-migrated/sp4-agents-index.spec.ts` | **Create** (1 desktop + 1 mobile = 2 PNG) |
| State test | `apps/web/e2e/v2-states/agents-index.spec.ts` | **Create** (5 stati × 2 viewport — adattato — vedi §4.1) |
| A11y test | `apps/web/e2e/a11y/agents-index.spec.ts` | **Create** (axe-core scan default + filtered-empty + reduced-motion) |
| Baselines | `apps/web/e2e/visual-migrated/sp4-agents-index.spec.ts-snapshots/*.png` | **Create** (2 PNG via CI bootstrap) |
| Baselines | `apps/web/e2e/v2-states/agents-index.spec.ts-snapshots/*.png` | **Create** (≤9 PNG via CI bootstrap) |
| Matrix update | `docs/frontend/v2-migration-matrix.md` | **Edit** (rows AgentsHero/AgentFilters/AgentsResultsGrid/EmptyAgents: `Status: pending → done`, `PR: TBD → #<n>`. Matrix già allineato 2026-04-30 in commit `5d75ca4b5`) |

### 3.2 Component API

#### `AgentsLibraryView` (orchestrator, internal `_components/`)

```ts
import type { ReactElement } from 'react';

export interface AgentsLibraryViewProps {
  readonly initialState?: 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';
  // initialState non-undefined ONLY in dev/test via ?state= URL param; production path = undefined
}

export function AgentsLibraryView(props: AgentsLibraryViewProps): ReactElement;
```

**Stato interno** (5-state FSM derivata):
- `loading` ← `useAgents()` `isLoading === true`
- `error` ← `useAgents()` `error !== null`
- `empty` ← `data.length === 0` (nessun agente creato)
- `filtered-empty` ← `data.length > 0` && `filtered.length === 0` (filtri attivi non matchano)
- `default` ← `data.length > 0` && `filtered.length > 0`

**State override** (escape hatch per Playwright visual tests):
- Guard env-based `STATE_OVERRIDE_ENABLED` (mirror B.1 pattern, dead-code-eliminated in prod build).
- `?state=` URL param via `useSearchParams()`.
- Quando override attivo: bypassa `useAgents()` data, usa `tryLoadVisualTestFixture()` per mock data deterministico (sentinel `IS_VISUAL_TEST_BUILD`).
- Mockup pattern: `state='no-results'` → forza `setStatus('archiviato'); setQ('zzznotfound')` per produrre filter-empty deterministico (mirror mockup `useEffect` linea ~580).

#### `AgentsHero`

```ts
export interface AgentsHeroProps {
  readonly totalCount: number;          // total agenti dal hook (count attesto < 50 typical)
  readonly slotsUsed?: number;          // optional, da useUserAgentSlots Issue #417 se disponibile
  readonly slotsTotal?: number;         // optional, da useUserAgentSlots
  readonly onCreateAgent: () => void;   // CTA → triggera AgentCreationSheet modal v1
  readonly compact?: boolean;           // mobile breakpoint (caller-driven)
}

export function AgentsHero(props: AgentsHeroProps): ReactElement;
```

**A11y**:
- `<header>` semantic
- `<h1>` con i18n title `pages.agents.title` ("I tuoi agenti AI")
- Subtitle/description con `<p>` con count interpolation: i18n key `pages.agents.subtitle` (`"Esperti dei tuoi giochi, sempre disponibili — {{count}} agenti totali."`)
- CTA button con `aria-label="Crea agente"` (icon `+` solo aria-hidden)
- Slot indicator (se presente) come `<dl>` term/description per screen-reader association

**Visual**:
- Background gradient `linear-gradient(180deg, ${entityHsl('agent', 0.08)} 0%, transparent 100%)` (parità mockup).
- Padding desktop `24px 32px 16px`, mobile `14px 16px 12px`.
- CTA button: `bg-entity-agent`, `text-white`, `boxShadow: 0 4px 14px ${entityHsl('agent', 0.4)}`.
- Mobile (`compact=true`): flex column, CTA `align-self: stretch`.

#### `AgentFilters` (rinominato da `AgentsFiltersStrip`)

```ts
export type AgentsStatusKey = 'all' | 'attivo' | 'in-setup' | 'archiviato';
export type AgentsSortKey = 'recent' | 'alpha' | 'used';

export interface AgentFiltersProps {
  readonly query: string;
  readonly onQueryChange: (next: string) => void;
  readonly status: AgentsStatusKey;
  readonly onStatusChange: (next: AgentsStatusKey) => void;
  readonly sort: AgentsSortKey;
  readonly onSortChange: (next: AgentsSortKey) => void;
  readonly resultCount: number;
  readonly compact?: boolean; // mobile: hide search input, status chips horizontal scroll
}

export function AgentFilters(props: AgentFiltersProps): ReactElement;
```

**A11y / WAI-ARIA**:
- Status segmented: `role="tablist"` + 4 `role="tab"` con `aria-selected`. Roving tabindex via `useTablistKeyboardNav<AgentsStatusKey>` (orientation `horizontal`, wrap).
- Sort: native `<select>` with `<label>` `Ordina` (i18n `pages.agents.filters.sortLabel`).
- Search: `<input type="search">` con `<label>` (`<span class="sr-only">`) e icon `aria-hidden`.
- Search debounce: 300ms (mirror B.1 + A.3b precedent), trailing edge.
- Sticky positioning: `position: sticky; top: 0; z-index: 10` + `backdrop-filter: blur(12px)` + `background: var(--glass-bg)` (parità mockup).
- `data-slot="agent-filters-strip"` per scoping baselines/tests.

#### `AgentsResultsGrid`

```ts
export interface AgentsResultsGridProps {
  readonly agents: readonly AgentDto[];
  readonly compact?: boolean; // mobile: 1-col, desktop: 3-col / 1280 max
}

export function AgentsResultsGrid(props: AgentsResultsGridProps): ReactElement;
```

**Mapping a `MeepleCard`**:
```ts
// Per ogni agent → MeepleCard props
{
  entity: 'agent',
  variant: 'grid',  // mockup è grid-only, NO list view (parità AC-2)
  id: agent.id,
  title: agent.name,
  subtitle: agent.gameName ?? undefined,
  // imageUrl: agent.avatar?  → Note: AgentDto NON ha avatar field. Vedi §3.4 BLOCKER B-NEW-3
  rating: undefined,           // agenti non hanno rating
  href: `/agents/${agent.id}`, // detail page → out-of-scope B.2 (orphan stub)
  // Status badge + connections strip → custom slots se MeepleCard li supporta, altrimenti wrap
}
```

**Layout responsive**:
- Mobile (`compact=true`): `grid grid-cols-1 gap-3 px-4`
- Tablet (≥768): `grid grid-cols-2 gap-4 px-6`
- Desktop (≥1280): `grid grid-cols-3 gap-4 px-8 max-w-[1280px] mx-auto` (parità mockup `auto-fit, minmax(320px, 1fr)`)

**Selettore CSS Grid raccomandato** (parità mockup):
```css
.agents-results-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 16px;
  padding: 16px 32px;
  max-width: 1280px;
  margin: 0 auto;
}
@media (max-width: 767px) {
  .agents-results-grid { grid-template-columns: 1fr; padding: 12px 16px; }
}
```

#### `EmptyAgents`

```ts
export type EmptyAgentsKind = 'empty' | 'filtered-empty' | 'error' | 'loading';

export interface EmptyAgentsProps {
  readonly kind: EmptyAgentsKind;
  readonly onCreateAgent?: () => void;     // required when kind === 'empty'
  readonly onClearFilters?: () => void;    // required when kind === 'filtered-empty'
  readonly onRetry?: () => void;           // required when kind === 'error'
}

export function EmptyAgents(props: EmptyAgentsProps): ReactElement;
```

**Discriminated UI**:
- `kind="loading"` → 6 skeleton cards (mobile=3) via `<Skeleton>` from `@/components/ui/feedback/skeleton`
- `kind="empty"` → mockup `EmptyAgents` cfg `no-agents` (icon 🤖, CTA "Crea il tuo primo agente" → `onCreateAgent()`)
- `kind="filtered-empty"` → mockup `EmptyAgents` cfg `no-results` (icon 🔎, CTA "Azzera filtri" → `onClearFilters()`)
- `kind="error"` → mockup `ErrorState` (icon ⚠, CTA "Riprova" → `onRetry()`)
- `data-slot="agents-empty-state"` + `data-kind="<kind>"` per scoping E2E (mirror B.1 `games-empty-state`).

### 3.3 Data shape — `AgentDto`

Backend DTO `AgentDto` ([source](../../../apps/web/src/lib/api/schemas/agents.schemas.ts:1)):
```ts
{
  id: string,                    // UUID
  name: string,
  type: string,                  // libera string, non enum
  strategyName: string,          // 'Tutor' | 'Arbitro' | ... (libera)
  strategyParameters: Record<string, any>,
  isActive: boolean,             // ⚠️ unico segnale "stato"
  createdAt: string,             // ISO datetime
  lastInvokedAt: string | null,  // ISO datetime
  invocationCount: number,       // counter
  isRecentlyUsed: boolean,       // server-derived
  isIdle: boolean,               // server-derived
  gameId?: string | null,        // UUID
  gameName?: string | null,
  createdByUserId?: string | null,
  // ❌ NO status field
  // ❌ NO tags field
  // ❌ NO chatCount field
  // ❌ NO avatar field
  // ❌ NO sessionCount field
  // ❌ NO kbCount field
  // ❌ NO lastActive (string description), only lastInvokedAt (ISO)
}
```

**Mapping mockup → AgentDto** (vedi §3.4 BLOCKERs B-NEW-2 to B-NEW-4):

| Mockup field | AgentDto source | Strategy |
|--------------|-----------------|----------|
| `agent.title` | `agent.name` | direct |
| `agent.status` | derived | client-side `deriveStatus(agent)` (B-NEW-2) |
| `agent.tags` | derived | `[agent.type, agent.strategyName]` pseudo-tags (B-NEW-3) |
| `agent.chatCount` | `agent.invocationCount` | rename for sort/filter (B-NEW-4) |
| `agent.kbCount` | ⚠️ unavailable | **omit from card UI** o file follow-up |
| `agent.sessionCount` | ⚠️ unavailable | **omit from card UI** |
| `agent.lastActive` (relative string) | `agent.lastInvokedAt` (ISO) | client-side relative formatter (`formatDistanceToNow`) |
| `agent.avatar` (emoji) | ⚠️ unavailable | **deterministic placeholder** (e.g. emoji da `agent.id` hash) |
| `agent.desc` | ⚠️ unavailable | **omit from card UI** o file follow-up |
| `agent.model` | `agent.strategyName` | rename for display |
| `agent.invocations` | `agent.invocationCount` | direct |

**Filter mapping**:

| Mockup status key | UI label IT/EN | Filter predicate (using `deriveStatus`) |
|-------------------|----------------|------------------------------------------|
| `all` | Tutti / All | `() => true` |
| `attivo` | Attivi / Active | `a => deriveStatus(a) === 'attivo'` |
| `in-setup` | In setup / In Setup | `a => deriveStatus(a) === 'in-setup'` |
| `archiviato` | Archiviati / Archived | `a => deriveStatus(a) === 'archiviato'` |

**Sort mapping**:

| Mockup sort key | UI label IT/EN | Sort comparator |
|-----------------|----------------|-----------------|
| `recent` | Più recenti / Recent | `(b.lastInvokedAt ?? b.createdAt).localeCompare(a.lastInvokedAt ?? a.createdAt)` desc |
| `alpha` | Alfabetico / Alphabetical | `a.name.localeCompare(b.name, 'it')` asc |
| `used` | Più usati / Most used | `b.invocationCount - a.invocationCount` desc |

**Search predicate** (free-text query):
- Match case-insensitive su: `agent.name`, `agent.gameName ?? ''`, pseudo-tags `[type, strategyName]`.
- Mockup linea 537 references `(a.tags || []).some(...)` → applichiamo pseudo-tag mapping.

### 3.4 Backend dependencies & verification (4 NEW BLOCKERs)

**Pre-implementation verification step (BLOCKING for impl)**:

#### **BLOCKER B-NEW-1 — `GET /api/v1/agents` route NOT registered** (HIGH severity)

**Discovery**: `GetAllAgentsQueryHandler` esiste in `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryHandler.cs` ma NESSUN `MapGet("/api/v1/agents", ...)` in `apps/api/src/Api/Routing/`. Handler è invocato SOLO internamente da `GameEndpoints.cs:370` per game-journey steps.

**Frontend impact**: `agentsClient.ts:getAll(activeOnly?, type?)` ha `@todo BACKEND MISSING: No route registered` marker e ritorna `[]` via fallback. `useAgents()` hook → empty state always in production.

**Verification**:
```bash
# Run backend
cd infra && make dev-core

# Test endpoint
curl -i http://localhost:8080/api/v1/agents \
  -H "Cookie: $(cat ~/.meepleai/dev-session-cookie)"
# Expected: 404 Not Found (route not registered)
```

**Decision tree**:

- **Option A — Bundle backend route registration in Wave B.2**:
  - Add `GET /api/v1/agents?activeOnly=&type=` MapGet in `apps/api/src/Api/Routing/AgentEndpoints.cs` (or KbEndpoints)
  - Auth: `RequireAuthenticatedUser`
  - Estimated +1-2 days backend work + integration test
  - Mirror precedent: Wave A.5a (#607 → PR #610) bundled backend in feature branch
  - Pro: production-ready su merge
  - Contro: scope creep, blocca frontend ship se backend rallenta
- **Option B — Ship frontend with visual-test fixture, file follow-up backend issue (RECOMMENDED)**:
  - Frontend ships con `lib/agents/visual-test-fixture.ts` per CI baselines deterministici
  - Workflow `visual-regression-migrated.yml` setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` → fixture short-circuits `useAgents` → 5 agenti deterministici
  - Production deploy: empty state (`useAgents` returns `[]` via `agentsClient.getAll` fallback) finché backend non registra route
  - Filed follow-up issue tracked in Wave B.4 backlog: "Register `GET /api/v1/agents` route + integration tests"
  - Pro: ship frontend immediately, unblocks user testing visual baselines, frontend code production-ready
  - Contro: production UX shows empty state until backend lands

**Recommendation**: **Option B** (ship frontend, file follow-up backend issue). Mirror B.1 pattern dove backend dependency `playCount` era marked deferred ma frontend shippato comunque.

**Implementation note**: documentare visibly in PR body sezione "Production caveat" + file follow-up issue PRIMA del merge.

#### **BLOCKER B-NEW-2 — `AgentDto.status` field missing** (MEDIUM severity)

**Discovery**: mockup ha `agent.status: 'attivo' | 'in-setup' | 'archiviato'` (3 valori). Backend `AgentDto` ha solo `isActive: boolean` + `invocationCount: number` + `isIdle: boolean`.

**Resolution**: client-side derivation pure helper.

```ts
// apps/web/src/lib/agents/derive-status.ts
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

export type AgentStatus = 'attivo' | 'in-setup' | 'archiviato';

export function deriveStatus(agent: AgentDto): AgentStatus {
  if (!agent.isActive) return 'archiviato';
  if (agent.invocationCount === 0) return 'in-setup';
  return 'attivo';
}
```

**Edge cases**:
- `isActive=false, invocationCount=0` → `archiviato` (priorità isActive)
- `isActive=false, invocationCount=10` → `archiviato` (agente disattivato dopo uso)
- `isActive=true, invocationCount=0, isIdle=true` → `in-setup` (mai usato)
- `isActive=true, invocationCount>0` → `attivo`

**Test in `derive-status.test.ts`** (~10 test): tutte le 4 combinazioni (active×count) + boundary `invocationCount=0` vs `>0` + assertion isIdle non-influence.

**Filed follow-up backend issue** (Wave B.4+ candidate): "Add `status: 'attivo' | 'in-setup' | 'archiviato'` field to `AgentDto` for unified agent state representation. Owner can override via UI action."

#### **BLOCKER B-NEW-3 — `AgentDto.tags` + `avatar` + `desc` + `kbCount` + `sessionCount` missing** (LOW severity, scope reduction)

**Resolution**: scope reduction strategy.

| Field missing | Strategy |
|---------------|----------|
| `tags` | Pseudo-tags `[agent.type, agent.strategyName]` per search match. Mockup search predicate aggiornato. |
| `avatar` | **Deterministic emoji placeholder**: hash `agent.id` → emoji da set `[🤖, 🧠, 💡, 🎯, ⚡, 🔮, 🎭, 🦉, 🌟, 🎲]`. Test deterministic (same id → same emoji). Helper `lib/agents/avatar-placeholder.ts`. |
| `desc` | Static fallback i18n `pages.agents.card.descFallback` ("Agente esperto del gioco, pronto a rispondere alle domande sulle regole.") — parità mockup linea 221. |
| `kbCount` + `sessionCount` | **Omit from card UI**. ConnectionChipStrip mostra solo `[{entityType: 'game', count: 1, isEmpty: !agent.gameId}]` se gameId presente, altrimenti "Nessuna connessione". |

**Filed follow-up backend issues** (Wave B.4+ candidates):
- "Extend `AgentDto` with `tags: string[]` for user-facing categorization"
- "Extend `AgentDto` with computed `kbCount` + `sessionCount` for connection strip display"
- "Allow user-uploaded `avatarUrl` on agent creation"

#### **BLOCKER B-NEW-4 — `chatCount` mapping** (TRIVIAL severity)

**Resolution**: rename mapping in card view: `mockup.chatCount → AgentDto.invocationCount`. Sort key `used` usa `invocationCount`. Aggiornare i18n key da "Chat count" → "Invocations" o usare label `{{count}}` neutrale.

**Decision**: usare label "Utilizzi" (Italian) / "Uses" (English) — semantica corretta per backend `invocationCount`.

### 3.5 i18n keys (`pages.agents.*`)

```jsonc
{
  "pages": {
    "agents": {
      "title": "I tuoi agenti AI",
      "subtitle": "Esperti dei tuoi giochi, sempre disponibili — {{count}} agenti totali.",
      "createAgentCta": "Crea agente",
      "createAgentCtaCompact": "+ Crea",
      "slotsLabel": "Slot agenti",
      "slotsValue": "{{used}} / {{total}}",
      "filters": {
        "searchPlaceholder": "Cerca agente o gioco…",
        "searchClear": "Pulisci",
        "statusLabel": "Stato",
        "status": {
          "all": "Tutti",
          "attivo": "Attivi",
          "inSetup": "In setup",
          "archiviato": "Archiviati"
        },
        "sortLabel": "Ordina",
        "sort": {
          "recent": "Più recenti",
          "alpha": "Alfabetico",
          "used": "Più usati"
        },
        "resultCount": "{{count}} agenti"
      },
      "card": {
        "descFallback": "Agente esperto del gioco, pronto a rispondere alle domande sulle regole.",
        "uses": "{{count}} utilizzi",
        "lastActive": "Ultimo uso: {{relative}}",
        "lastActiveNever": "Mai usato",
        "statusBadge": {
          "attivo": "● Attivo",
          "inSetup": "⚙ In setup",
          "archiviato": "⊘ Archiviato"
        }
      },
      "emptyState": {
        "empty": {
          "title": "Non hai ancora creato agenti",
          "subtitle": "Gli agenti AI rispondono alle domande sui tuoi giochi usando i regolamenti che carichi. Crea il primo per iniziare.",
          "cta": "Crea il tuo primo agente"
        },
        "filteredEmpty": {
          "title": "Nessun agente corrisponde",
          "subtitle": "Prova a cambiare filtro stato o azzera la ricerca per vedere tutti gli agenti.",
          "cta": "Azzera filtri"
        },
        "error": {
          "title": "Impossibile caricare gli agenti",
          "subtitle": "Si è verificato un errore di rete. Verifica la connessione e riprova.",
          "cta": "Riprova"
        }
      }
    }
  }
}
```

EN mirror in `apps/web/src/locales/en.json`. Total ~35 keys × 2 locales.

### 3.6 Server/client split

`/agents/page.tsx` rimane `'use client'` come oggi (existing brownfield, già client). NO server component split per B.2 — out-of-scope (parità B.1).

`AgentsLibraryView` is client-side, riceve `searchParams` via `useSearchParams()` per `?state=` override (dev/test only).

**Reasoning**: B.1 ha shippato senza server split. Allineamento di pattern. Server fetch è candidate per Wave B.4+ ottimizzazione.

### 3.7 Bundle budget (Wiegers + Nygard)

**Target**: Δ +15-25 KB First Load JS per `/agents` rispetto a baseline pre-PR (parità B.1).
**Hard limit**: +50 KB (gate `frontend-bundle-size` PR #631 fail).
**Strategia**:
- Riuso `MeepleCard` v1 (zero duplicate). Riuso `useTablistKeyboardNav` hook (zero new lib).
- 4 v2 component nuovi inline-sized (no external deps). Stima: ~8 KB gzipped.
- i18n keys: ~35 × 2 locales ≈ 1.7 KB ciascuno.
- Skeleton + EmptyState reuse `Skeleton` v1.
- `derive-status.ts` + `library-filters.ts` + `avatar-placeholder.ts` < 1 KB combinati.

**Mitigazione overflow**:
- Se >35 KB: code-split `EmptyAgents` con `next/dynamic` (loading state già skeleton).
- Se >50 KB: investigate `MeepleCard` re-tree-shake — escalation (block PR).

## 4. Test plan

### 4.1 Visual baselines (Playwright `visual-migrated` + `v2-states`)

| Spec | Coverage | PNG count | Viewport |
|------|----------|-----------|----------|
| `visual-migrated/sp4-agents-index.spec.ts` | Default state pixel-match con mockup baseline | 2 | desktop 1440 + mobile 375 |
| `v2-states/agents-index.spec.ts` | 5 stati FSM (default/loading/empty/filtered-empty/error) | ≤9 | desktop + mobile (riduzione: error mobile-only se mobile = desktop layout) |

**Total**: 9-11 PNG canonical, gestiti via `visual-regression-migrated.yml` bootstrap (parità B.1 process).

**Bootstrap workflow**: `visual-regression-migrated.yml` (Wave A/B.1 pattern):
1. Set env `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1`
2. Set env `NEXT_PUBLIC_LOCALE=it` (default)
3. Set env `STATE_OVERRIDE_ENABLED=1`
4. `pnpm build`
5. Run Playwright → genera baselines via `--update-snapshots` su CI runner Linux
6. Upload artifact `visual-migrated-baselines` per inspect locale
7. PR review owner downloads artifact, copia PNG in `*.spec.ts-snapshots/`, commit con `chore(visual): bootstrap canonical baselines for /agents`

**Cookie consent suppression** (lesson learned A.4 + B.1): riuso `e2e/_helpers/seedCookieConsent.ts` — applicare PRIMA di `page.goto()` in tutti gli spec.

**Anti-pattern fix preventivo** (B.1 Iter 1 lesson):
```ts
// ❌ FORBIDDEN
await page.goto('/agents', { waitUntil: 'networkidle' });

// ✅ REQUIRED
await page.goto('/agents', { waitUntil: 'domcontentloaded' });
await page.waitForSelector('[data-slot="agents-library-view"]', { timeout: 30_000 });
```

**EntityBadge a11y exclusion** (B.1 Iter 2 lesson, Issue #636 still open):
```ts
// e2e/a11y/agents-index.spec.ts
const results = await new AxeBuilder({ page })
  .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
  .exclude('#webpack-dev-server-client-overlay')
  // Pre-existing debt #636 — entityColors token contrast 3.57:1 < WCAG 4.5:1 AA
  .exclude('[data-slot="meeple-card-entity-badge"]')
  .analyze();
```

### 4.2 Unit tests (Vitest)

| File | Coverage |
|------|----------|
| `lib/agents/__tests__/library-filters.test.ts` | Pure helpers: `filterByStatus(agents, key)` × 4 status, `filterByQuery(agents, query)` × edge (empty/match-name/match-game/match-pseudo-tags), `sortAgents(agents, sortKey)` × 3 sort. ~15 test |
| `lib/agents/__tests__/derive-status.test.ts` | Pure FSM: `deriveStatus(agent)` × 4 combinazioni (isActive×count), boundary, isIdle non-influence. ~10 test |
| `components/v2/agents/__tests__/AgentsHero.test.tsx` | Render con totalCount, slots optional, fallback 0 values, compact prop layout switch, onCreateAgent invocation. ~6 test |
| `components/v2/agents/__tests__/AgentFilters.test.tsx` | Search input controlled, debounce trailing-edge timing (vi.useFakeTimers 300ms), status tablist keyboard nav (Arrow/Home/End wrap), sort onChange, sticky positioning DOM check. ~15 test |
| `components/v2/agents/__tests__/AgentsResultsGrid.test.tsx` | Mapping agents → MeepleCard props, gameName subtitle, avatar placeholder deterministic, compact 1-col mobile. ~8 test |
| `components/v2/agents/__tests__/EmptyAgents.test.tsx` | 4 kind variants render, `onCreateAgent`/`onClearFilters`/`onRetry` callbacks, missing required callback throws (dev-only assert). ~8 test |
| `app/(authenticated)/agents/_components/__tests__/AgentsLibraryView.test.tsx` | FSM derivation: loading/error/empty/filtered-empty/default; useAgents mock; state override `?state=` guard; clear filters resets q+status; data-slot present. ~10 test |

**Total target**: ~72 unit tests, all green pre-PR.

### 4.3 A11y test (Playwright + axe-core)

`apps/web/e2e/a11y/agents-index.spec.ts`:
```ts
test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
  await seedCookieConsent(page);
  await page.goto('/agents', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="agents-library-view"]', { timeout: 30_000 });

  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .exclude('#webpack-dev-server-client-overlay')
    .exclude('[data-slot="meeple-card-entity-badge"]')  // #636 deferred
    .analyze();
  expect(results.violations).toEqual([]);
});

test('axe-core: no WCAG 2.1 AA violations on filtered-empty state', async ({ page }) => {
  // ...con ?state=filtered-empty
});

test('prefers-reduced-motion: card hover transitions collapse to sub-ms', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await seedCookieConsent(page);
  await page.goto('/agents', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="agents-library-view"]', { timeout: 30_000 });
  // computed transition-duration check ≤ 50ms (parità B.1 AC-8)
});
```

CI gate `frontend-a11y` (PR #602) **MUST PASS** prima merge.

### 4.4 Behavior scenarios (Given/When/Then) — pre-impl reference, NOT new test files

I 6 GWT scenarios elencati nel body issue #634 sono coperti da:
- **Search debounce**: `AgentFilters.test.tsx` con fake timers 300ms
- **Empty filtered → reset**: `AgentsLibraryView.test.tsx` clearFilters callback
- **Status keyboard nav**: hook `useTablistKeyboardNav` già testato (PR #623), + `AgentFilters.test.tsx` integrazione
- **Empty agents library**: `EmptyAgents.test.tsx` + visual snapshot `v2-states/agents-index.spec.ts`
- **prefers-reduced-motion**: `e2e/a11y/agents-index.spec.ts` setta media via Playwright + computed style assert
- **Status derivation**: `derive-status.test.ts` boundary cases

## 5. Acceptance criteria (13 SMART)

> **Convenzione**: AC-N · `<criterio testabile>` · evidence: `<test/snapshot/check>`

- **AC-1** · La route `/agents` rendera mockup `sp4-agents-index` pixel-match desktop 1440 + mobile 375 (entrambi con visual-test fixture). · Evidence: 2 PNG `visual-migrated/sp4-agents-index.spec.ts-snapshots/*-linux.png` PASS in CI.
- **AC-2** · I filtri inline rispecchiano il mockup esattamente: search + status segmented (Tutti/Attivi/In setup/Archiviati) + sort dropdown (Più recenti/Alfabetico/Più usati). NO view toggle (mockup grid-only). · Evidence: `AgentFilters.test.tsx` 15 test green.
- **AC-3** · Le card riusano `MeepleCard` v1 con `entity="agent"` e `variant="grid"`. ZERO fork del componente. · Evidence: grep `MeepleCard.*entity.*agent` in `AgentsResultsGrid.tsx` matcha; assenza di `// fork` o `MeepleCard.v2` import.
- **AC-4** · Status segmented tablist supporta WAI-ARIA APG con keyboard nav: Arrow Left/Right (wrap), Home/End. Roving tabindex. Automatic activation. · Evidence: `AgentFilters.test.tsx` keyboard scenarios + manual Tab→Arrow nav green.
- **AC-5** · 5 stati FSM (default/loading/empty/filtered-empty/error) renderano correttamente con discriminated UI. · Evidence: ≤9 PNG `v2-states/agents-index.spec.ts-snapshots/*-linux.png` PASS + `AgentsLibraryView.test.tsx` FSM coverage 100%.
- **AC-6** · Search input debouncing 300ms trailing-edge: digitando "x", "xy", "xyz" in <100ms si scatena UN SOLO filter cycle. · Evidence: `AgentFilters.test.tsx` con `vi.useFakeTimers` verifica callback chiamato 1 volta.
- **AC-7** · Empty filtered state mostra CTA "Azzera filtri" che resetta `query=''` + `status='all'` (sort resta). · Evidence: `AgentsLibraryView.test.tsx` clearFilters integration test.
- **AC-8** · `prefers-reduced-motion: reduce` disabilita transition card hover (`transform`, `box-shadow`, `border-color`). · Evidence: `e2e/a11y/agents-index.spec.ts` con `emulateMedia({ reducedMotion: 'reduce' })` + computed style assert ≤ 50ms.
- **AC-9** · `frontend-a11y` axe-core scan WCAG 2.1 AA: 0 violations su default + filtered-empty (con `[data-slot="meeple-card-entity-badge"]` exclusion documentata). · Evidence: CI gate PASS.
- **AC-10** · `frontend-bundle-size` gate: Δ First Load JS per `/agents` ≤ +50 KB hard limit (target +35 KB). · Evidence: CI gate PASS, report nel PR comment.
- **AC-11** · i18n bilingue IT+EN: 0 stringhe hard-coded nei 4 v2 component. Switch locale rerender label. · Evidence: grep `'Tutti'\|'Attivi'\|'I tuoi agenti'` in `components/v2/agents/*.tsx` ritorna 0 match (tutto da `useTranslation()`).
- **AC-12** · Test pyramid full green pre-merge: ~72 unit (Vitest) + visual snapshots (≤11 PNG totali) + 3 a11y E2E. · Evidence: PR CI summary tutti green tranne codecov/patch (pattern Wave A/B.1).
- **AC-13** · `AgentCreationSheet` modal v1 invariato (no regressioni): "Crea Agente" CTA da Hero v2 invoca lo stesso modal. · Evidence: visit manual `/agents` → click "Crea agente" → modal opens (smoke), esistenti unit test passano.

## 6. Risks & mitigations

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|-----------|
| R1 | EntityBadge color-contrast pre-existing debt #636 fa fail axe-core | High | Medium | `data-slot="meeple-card-entity-badge"` exclusion in axe spec con comment + reference #636 (B.1 Iter 2 pattern). Audit-driven scope reduction. |
| R2 | Cookie consent banner shifting layout (visual baseline flake) | Medium | High | `seedCookieConsent` helper in `e2e/_helpers/`. Lesson learned A.4 PR #605 + B.1 PR #635. |
| R3 | `GET /api/v1/agents` route NOT registered → frontend renders empty state in production | High | High | **Option B (RECOMMENDED)**: ship frontend con visual-test fixture per CI baselines + filed follow-up backend issue. Production caveat documentato in PR body. Empty state UX gracefully degraded fino a backend route registration. AC-5 `empty` snapshot copre il caso. Vedi §3.4 BLOCKER B-NEW-1. |
| R4 | `AgentDto.status` derivation heuristic ambigua | Low | Medium | `deriveStatus()` pure helper testato esaustivamente (4 combinazioni + boundary + isIdle non-influence). Filed follow-up backend issue per `status` field native. §3.4 BLOCKER B-NEW-2. |
| R5 | Search debounce timing flake CI | Low | Low | `vi.useFakeTimers` + `vi.advanceTimersByTime(300)` deterministic. Mirror B.1 pattern. |
| R6 | `networkidle` anti-pattern flake CI | Low | High | **Forbidden**: usare SEMPRE `'domcontentloaded'` + `waitForSelector('[data-slot="agents-library-view"]')`. Lesson learned B.1 Iter 1. |
| R7 | `data-*` attribute collision con `MeepleCard` shared root | Medium | Medium | Mirror B.1 Iter 3 lesson: usare `data-slot` (semantica nominale unica) per scoping, NON `data-entity` (già esposto da MeepleCard root → would 2× match). |
| R8 | Bundle size overflow >50 KB | Low | High | §3.7 mitigazione: code-split EmptyAgents con `next/dynamic`. Bundle gate fail PR. |
| R9 | Strict-mode locator violation (multi-state mounted) | Low | Medium | Scope locator a `data-slot` + `data-kind` discriminator. Pattern B.1. |
| R10 | `useAgents` hook signature change durante impl | Very Low | Low | Sniff signature in §3.3 baseline. Se cambia mid-PR, rebase. |
| R11 | i18n hot-reload stale durante dev | Low | Low | `pnpm dev` restart o `--clear-cache`. Documentato in dev guide. |
| R12 | Avatar placeholder hash collision (same emoji per multipli agenti) | Very Low | Low | Hash distribution test in `avatar-placeholder.test.ts`. Cosmetic concern. |
| R13 | Progressive reveal (`useInfiniteScroll`) regression | Low | Low | Mantenere existing logic. Smoke test manual con > 12 agenti dev seed. AC-13 catch via modal smoke. |

## 7. Out of scope (explicit)

- ❌ `AdvancedFiltersDrawer` (BLOCKER #2 deferred, `b-2 si` decision — dropped totalmente da Wave B)
- ❌ `AgentsSidebarList` + `AgentDetailPanel` (BLOCKER #1 — mockup è grid pattern, no master-detail. Stub orphan cleanup post-Wave B.)
- ❌ `AgentCreationSheet` v2 redesign (modal v1 mantenuto)
- ❌ Detail page `/agents/[id]` (route esiste come stub `apps/web/src/app/(authenticated)/agents/[id]/`, out-of-scope B.2)
- ❌ Agent slot management UI (keyboard, edit, archive actions su card)
- ❌ Server Component conversion del page principale
- ❌ Real-time SSE updates su agent list
- ❌ Backend `GET /api/v1/agents` route registration (Option B → follow-up issue)
- ❌ Backend `AgentDto` extension (`status` field, `tags`, `kbCount`, `sessionCount`, `avatar` — child issues)
- ❌ `MeepleCard` extension (variant nuovi, prop nuove)
- ❌ List view variant per `AgentsResultsGrid` (mockup è grid-only)

## 8. Sequencing — 5 commits TDD

### Commit 1 — Foundation: helpers + i18n + visual fixture

**Files**:
- `apps/web/src/lib/agents/derive-status.ts` (pure: `deriveStatus(agent) → AgentStatus`)
- `apps/web/src/lib/agents/library-filters.ts` (pure helpers: `filterByStatus`, `filterByQuery`, `sortAgents`)
- `apps/web/src/lib/agents/avatar-placeholder.ts` (deterministic emoji from agent.id hash)
- `apps/web/src/lib/agents/visual-test-fixture.ts` (sentinel pattern A.4/B.1)
- `apps/web/src/lib/agents/__tests__/derive-status.test.ts` (~10 test, all green)
- `apps/web/src/lib/agents/__tests__/library-filters.test.ts` (~15 test, all green)
- `apps/web/src/lib/agents/__tests__/avatar-placeholder.test.ts` (~5 test, all green)
- `apps/web/src/locales/it.json` (~35 keys aggiunte)
- `apps/web/src/locales/en.json` (~35 keys aggiunte)
- (riuso esistente) `apps/web/e2e/_helpers/seedCookieConsent.ts`

**Validation**:
- `pnpm test apps/web/src/lib/agents/__tests__/` → 30/30 green
- `pnpm typecheck` → 0 errors
- **Pre-flight backend verification (§3.4 B-NEW-1)**: documentare risultato curl in PR body

**Commit message**: `feat(agents): foundation helpers + i18n keys for /agents v2 (#634)`

### Commit 2 — Component family TDD

**Files**:
- `apps/web/src/components/v2/agents/AgentsHero.tsx` (impl)
- `apps/web/src/components/v2/agents/AgentsFiltersStrip.tsx` → **rinominato** `AgentFilters.tsx` (impl con `useTablistKeyboardNav`)
- `apps/web/src/components/v2/agents/AgentsResultsGrid.tsx` (impl, **file nuovo**)
- `apps/web/src/components/v2/agents/EmptyAgents.tsx` (impl)
- `apps/web/src/components/v2/agents/index.ts` (barrel)
- `apps/web/src/components/v2/agents/__tests__/*.test.tsx` (4 file, ~37 test totali)

**Order TDD per ogni component**:
1. Write failing test (red)
2. Impl minimum to green (green)
3. Refactor (no behavior change)

**Path discipline**: rename via `git mv apps/web/src/components/v2/agents/AgentsFiltersStrip.tsx apps/web/src/components/v2/agents/AgentFilters.tsx` per preservare history. NO consumer impact (stub returns null, zero usage in repo).

**Validation**:
- `pnpm test apps/web/src/components/v2/agents` → ~37/37 green
- `pnpm typecheck` → 0 errors
- `pnpm lint apps/web/src/components/v2/agents` → 0 errors

**Commit message**: `feat(agents): AgentsHero/AgentFilters/AgentsResultsGrid/EmptyAgents v2 components (#634)`

### Commit 3 — Page integration: AgentsLibraryView orchestrator

**Files**:
- `apps/web/src/app/(authenticated)/agents/_components/AgentsLibraryView.tsx` (impl)
- `apps/web/src/app/(authenticated)/agents/_components/__tests__/AgentsLibraryView.test.tsx` (~10 test)
- `apps/web/src/app/(authenticated)/agents/page.tsx` (edit: body migration → `<AgentsLibraryView>`, mantenere `AgentCreationSheet` modal v1 wrapping)

**Validation**:
- `pnpm test apps/web/src/app/(authenticated)/agents` → ~10/10 green
- Manual smoke: `pnpm dev` → `/agents` mostra v2 layout, click "+ Crea agente" → `AgentCreationSheet` modal opens (no regression)
- `pnpm typecheck` → 0 errors

**Commit message**: `feat(agents): AgentsLibraryView orchestrator + page integration (#634)`

### Commit 4 — Visual + state + a11y E2E specs (NO baselines yet)

**Files**:
- `apps/web/e2e/visual-migrated/sp4-agents-index.spec.ts`
- `apps/web/e2e/v2-states/agents-index.spec.ts`
- `apps/web/e2e/a11y/agents-index.spec.ts`

**Validation**:
- `pnpm test:e2e apps/web/e2e/a11y/agents-index.spec.ts` → 3 axe scans green (su build con env fixture)
- Visual specs FAIL su CI prima bootstrap (no baseline) — atteso, fix in commit 5

**Commit message**: `test(agents): visual-migrated + v2-states + a11y E2E for /agents (#634)`

### Commit 5 — Bootstrap canonical baselines

**Workflow CI**: trigger `visual-regression-migrated.yml` con `update_snapshots=true` su questo branch.

**Steps**:
1. Workflow run su branch genera artifact `visual-migrated-baselines-<run-id>` (≤11 PNG: 2 visual-migrated + ≤9 v2-states)
2. Download artifact local, copia PNG in:
   - `apps/web/e2e/visual-migrated/sp4-agents-index.spec.ts-snapshots/`
   - `apps/web/e2e/v2-states/agents-index.spec.ts-snapshots/`
3. Filter copy: NON sovrascrivere baselines di altre route (mirror pattern B.1)
4. Update `docs/frontend/v2-migration-matrix.md`: rows AgentsHero/AgentFilters/AgentsResultsGrid/EmptyAgents `Status: pending → done`, `PR: TBD → #<n>`.

**Validation**:
- Re-run CI workflow su branch: visual specs green, no baseline missing
- All gates green: `frontend-a11y` ✅, `frontend-bundle-size` ✅, `Migrated Routes Baseline` ✅

**Commit message**: `chore(visual): bootstrap canonical baselines for /agents v2 (#634)`

### Final — PR creation

**Title**: `feat(agents): migrate /agents to v2 design (#634)`
**Body**: include
- Closes #634 + Refs #580 + Refs #578
- Backend verification result (Option A vs B per §3.4 B-NEW-1) — **expected: Option B with production caveat**
- Filed follow-up issues (backend route registration + DTO field extensions)
- Bundle delta report
- Spec link `docs/superpowers/specs/2026-04-30-v2-migration-wave-b-2-agents.md`
- Test summary (unit count + visual baselines + a11y)
- Out-of-scope explicit (drawer dropped, sidebar/detail orphans, agent creation modal v1)

**Base branch**: `main-dev` (per CLAUDE.md PR Target Rule, parent branch `feature/issue-634-agents-fe-v2`).
**Merge strategy**: squash + `--admin --delete-branch` se needed (Wave A/B.1 pattern, codecov/patch oscillatory flake). Mirror B.1: prefer no `--admin` if MERGEABLE/CLEAN.

## 9. Bootstrap baseline workflow — recap

```bash
# Step 1: Push branch with E2E specs (commit 4)
git push -u origin feature/issue-634-agents-fe-v2

# Step 2: Trigger workflow with --update-snapshots
gh workflow run visual-regression-migrated.yml \
  --ref feature/issue-634-agents-fe-v2 \
  -f update_snapshots=true

# Step 3: Wait + download artifact
gh run watch <run-id>
gh run download <run-id> -n visual-migrated-baselines-<run-id> -D ./tmp-baselines

# Step 4: Copy filtered PNG (only for /agents, not other routes)
cp tmp-baselines/visual-migrated/sp4-agents-index.spec.ts-snapshots/*.png \
   apps/web/e2e/visual-migrated/sp4-agents-index.spec.ts-snapshots/
cp tmp-baselines/v2-states/agents-index.spec.ts-snapshots/*.png \
   apps/web/e2e/v2-states/agents-index.spec.ts-snapshots/

# Step 5: Cleanup + commit
rm -rf tmp-baselines
git add apps/web/e2e/visual-migrated/sp4-agents-index.spec.ts-snapshots/ \
        apps/web/e2e/v2-states/agents-index.spec.ts-snapshots/
git commit -m "chore(visual): bootstrap canonical baselines for /agents v2 (#634)"
git push

# Step 6: Re-run workflow → all green
gh workflow run visual-regression-migrated.yml --ref feature/issue-634-agents-fe-v2
```

## 10. Spec-panel review — applied findings

### 10.1 Original BLOCKERs (3) — resolved by user via `b-1/b-2/b-3 si`

| # | Finding | Expert | Resolution |
|---|---------|--------|-----------|
| BLOCKER #1 | Master-detail layout claim conflicts with mockup grid pattern | Wiegers + Adzic + Cockburn | **`b-1 si`**: treat `sp4-agents-index.jsx` as source of truth (grid). Rewrite Issue #634 to remove master-detail. Move `AgentsSidebarList` + `AgentDetailPanel` out of `/agents` scope. Matrix row update committato in `5d75ca4b5`. |
| BLOCKER #2 | `AdvancedFiltersDrawer` not in mockup, scope ambiguity | Wiegers + Cockburn | **`b-2 si`**: drop drawer **TOTALMENTE** from Wave B scope (B.2 + umbrella DoD #580). No follow-up issue. |
| BLOCKER #3 | Matrix naming inconsistency (`AgentsFiltersStrip` vs mockup `AgentFilters`) | Doumont + Wiegers | **`b-3 si`**: align matrix to mockup naming. `AgentsFiltersStrip` → `AgentFilters` rename (path discipline mantenuta). Matrix row update committato in `5d75ca4b5`. |

### 10.2 NEW BLOCKERs (4) — discovered during reconnaissance, resolved in spec

| # | Finding | Expert | Resolution |
|---|---------|--------|-----------|
| B-NEW-1 | `GET /api/v1/agents` route NOT registered backend (handler exists, no `MapGet`) | Nygard + Newman | **Option B (RECOMMENDED)**: ship frontend with visual-test fixture, file follow-up backend issue. Production renders empty state until backend lands. Documented as Risk R3 (HIGH). §3.4. |
| B-NEW-2 | `AgentDto.status` field missing | Wiegers + Hohpe | Client-side derivation `deriveStatus()` pure helper. Filed follow-up backend issue. §3.4 + AC-N derivation tests. |
| B-NEW-3 | `AgentDto` missing `tags`, `avatar`, `desc`, `kbCount`, `sessionCount` | Wiegers | Scope reduction strategy: pseudo-tags + emoji placeholder + i18n fallback desc + omit kbCount/sessionCount from card UI. Filed follow-up issues. §3.4. |
| B-NEW-4 | `chatCount` mockup → `invocationCount` AgentDto rename | Doumont | Trivial mapping + i18n label "Utilizzi"/"Uses" semantica corretta. §3.4. |

### 10.3 Short-term (6) — applied (parità B.1)

| # | Finding | Expert | Resolution |
|---|---------|--------|-----------|
| ST-1 | A11y status tablist WAI-ARIA APG | Crispin | AC-4 + `useTablistKeyboardNav` mandate + a11y E2E AC-9 |
| ST-2 | Search debounce timing not specified | Adzic | §3.2 AgentFilters 300ms trailing-edge + AC-6 fake timers test |
| ST-3 | prefers-reduced-motion compliance | Crispin | AC-8 + e2e a11y `emulateMedia` + computed style ≤ 50ms |
| ST-4 | Bundle budget hard limit | Nygard | §3.7 + AC-10 + mitigazione code-split |
| ST-5 | i18n hard-coded strings risk | Doumont | AC-11 grep-based assertion |
| ST-6 | Visual flake mitigation cookie banner | Crispin | §4.1 `seedCookieConsent` helper + R2 risk |

### 10.4 Long-term (3) — tracked

| # | Finding | Expert | Tracking |
|---|---------|--------|----------|
| LT-1 | Server Component conversion del page (FCP/LCP optimization) | Fowler + Newman | §2 out-of-scope. Wave B.4+ candidate. |
| LT-2 | `AgentDto` backend extension (status/tags/kbCount/sessionCount/avatar) | Wiegers + Nygard | §3.4 child issues per ogni campo. Wave B.4+ candidate. |
| LT-3 | `MeepleCard` v2 variant extension (top accent bar agent gradient + status badge slot) | Fowler | §2 out-of-scope. Spec dedicato MeepleCard v2 se needed. |

## 11. Effort estimate

- **Foundation (commit 1)**: 0.5 day (helpers + derive-status + avatar-placeholder + i18n + fixture)
- **Component TDD (commit 2)**: 2 days (4 component × 0.5 day each, includes test + rename)
- **Page integration (commit 3)**: 0.5 day (orchestrator + brownfield page edit + AgentCreationSheet wiring)
- **E2E specs (commit 4)**: 0.5 day
- **Bootstrap baselines (commit 5)**: 0.5 day (CI roundtrip + verify)
- **PR review + merge**: 0.5-1 day buffer

**Total**: 5-7 days (single-engineer, including review). Mirror B.1 estimate.

## 12. Open questions

1. **Backend route gap (R3 / B-NEW-1)**: confirm Option B (ship frontend, file follow-up) vs Option A (bundle backend). **Default: Option B** per analogia B.1 + Wave A.5b parity.
2. **Slot indicator (#417 hook reuse)**: include `useUserAgentSlots()` data in `AgentsHero` props? **Default: yes if hook is stable + cheap, otherwise omit per parità mockup**.
3. **Progressive reveal**: mantenere `useInfiniteScroll` con `PAGE_SIZE=12` o full-render? **Default: keep current behavior (>12 agenti = progressive, ≤12 = full grid)** per backward-compat.
4. **`AgentsSidebarList`/`AgentDetailPanel` orphan cleanup**: file follow-up issue per delete stub o leave in place? **Default: leave in place, file Wave B closeout cleanup issue**.

## 13. References

- Spec umbrella: [`2026-04-26-v2-design-migration.md`](2026-04-26-v2-design-migration.md) §4
- Phase 1 execution: [`2026-04-27-v2-migration-phase1-execution.md`](2026-04-27-v2-migration-phase1-execution.md) §3.3
- Wave B.1 spec (parent pattern): [`2026-04-29-v2-migration-wave-b-1-games.md`](2026-04-29-v2-migration-wave-b-1-games.md)
- Migration matrix (single source of truth): [`docs/frontend/v2-migration-matrix.md`](../../frontend/v2-migration-matrix.md)
- Bundle size budget guide: [`docs/frontend/bundle-size-budget.md`](../../frontend/bundle-size-budget.md)
- MeepleCard design tokens: [`docs/frontend/meeple-card-design-tokens.md`](../../frontend/meeple-card-design-tokens.md)
- `useTablistKeyboardNav` hook: [`apps/web/src/hooks/useTablistKeyboardNav.ts`](../../../apps/web/src/hooks/useTablistKeyboardNav.ts) (PR #623 + orientation extension PR #626)
- Mockup: [`admin-mockups/design_files/sp4-agents-index.jsx`](../../../admin-mockups/design_files/sp4-agents-index.jsx)
- Existing brownfield page: [`apps/web/src/app/(authenticated)/agents/page.tsx`](../../../apps/web/src/app/(authenticated)/agents/page.tsx)
- `AgentDto` schema: [`apps/web/src/lib/api/schemas/agents.schemas.ts`](../../../apps/web/src/lib/api/schemas/agents.schemas.ts)
- `agentsClient`: [`apps/web/src/lib/api/clients/agentsClient.ts`](../../../apps/web/src/lib/api/clients/agentsClient.ts) (multi `@todo BACKEND MISSING` markers)
- `useAgents` hook: [`apps/web/src/hooks/queries/useAgents.ts`](../../../apps/web/src/hooks/queries/useAgents.ts)
- Issue #636 (EntityBadge a11y debt): pre-existing, deferred. Exclude pattern via `data-slot="meeple-card-entity-badge"`.
