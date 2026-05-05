# `/games/[id]` Hook Contract — Wave C.1 Phase 0.5

> **Phase 0.5 contract** per V2 migration spec sezione 3.4.
> **Tier**: L (≥3 hook indipendenti, cartesian FSM).
> **Scope**: orchestrator `GameDetailViewV2` per route `/games/[id]`.
> **Issue**: #581 (Wave C umbrella). **Mockup**: `admin-mockups/design_files/sp4-game-detail.jsx`.
>
> Questo documento è **prerequisito non-negoziabile** prima di dispatchare implementation
> subagent per Wave C.1 retry. Il fail di PR #697 (closed 2026-05-04) è stato causato
> dall'assenza di questo contract: sub-hook fired before parent gameId resolved
> (`/api/v1/agents/undefined` cascade), state-machine cartesian FSM non mappata,
> A11y `role="tabpanel"` mancante.

## 1. Route surface

| Aspect | Value |
|--------|-------|
| Route path | `/games/[id]` |
| Page component | `apps/web/src/app/(authenticated)/games/[id]/page.tsx` |
| Orchestrator | `apps/web/src/app/(authenticated)/games/[id]/_components/GameDetailViewV2.tsx` |
| Param source | `useParams<{ id: string }>()` |
| Param validation | `params?.id` può essere `undefined` (Next.js 16 app router pre-hydration) o stringa `'undefined'` se cast disordinato |
| **Critical**: never pass `undefined` literal o stringa `'undefined'` ai sub-hook | |

## 2. Hook dependency graph

```
useParams<{id}> ──validate──→ gameId: string | null
                                  │
                                  │ (null se params?.id è undefined o "")
                                  │
                                  ├─→ useLibraryGameDetail({ id: gameId, enabled: !!gameId })
                                  │       │
                                  │       └─→ STATE: idle | loading | error | success(data) | success(null)
                                  │
                                  ├─→ useGameInLibraryStatus({ gameId, enabled: !!gameId })
                                  │       │
                                  │       └─→ secondary, fallisce gracefully
                                  │
                                  └─→ Tab "Agents" attivo:
                                      useGameAgents({ gameId, enabled: !!gameId && libraryGameDetail.isSuccess && tab === 'agents' })
                                            │
                                            └─→ STATE: disabled | loading | error | success([]) | success([...])
```

**Key constraints**:
1. **`gameId` è `string | null`, MAI `undefined` o `'undefined'`**
2. **Sub-hook gating cumulativo**: ogni sub-hook ha `enabled: !!gameId && parentSuccess && tabActive`
3. **`useLibraryGameDetail` è il parent** — sub-hooks NON montano finché parent !== success
4. **Lazy tab fetch**: `useGameAgents` solo se `tab === 'agents'`. Tabs FAQ/Rules/Sessions/KB usano fetch lazy o link a subroute legacy

### 2.1 GameId resolution

```ts
// CORRETTO
const params = useParams<{ id: string }>();
const rawId = params?.id;
const gameId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

// SBAGLIATO (causa Wave C.1 fail)
const id = params?.id ?? '';  // empty string → !!id === false → OK ma non robusto
const id = String(params?.id);  // → 'undefined' → !!id === true → fetch /api/v1/agents/undefined
```

### 2.2 Sub-hook gating contract

```ts
// useLibraryGameDetail (parent)
const detailQuery = useLibraryGameDetail(gameId);  // hook accetta string | null

// useGameAgents (gated by parent + tab)
const agentsQuery = useGameAgents({
  gameId,
  enabled: !!gameId && detailQuery.isSuccess && tab === 'agents',
});
```

**Anti-pattern dal PR #697**:
```ts
// SBAGLIATO — gate incorretto, fixture path bypass non autorizzato
enabled: !!id && (fixture == null ? detailQuery.data != null : true),
```
Il problema: fixture branch abilitava il fetch indipendentemente dal gameId, e `id` era stringa potenzialmente `'undefined'`.

## 3. FSM cell matrix

Cartesian rilevante (subset di 3×3×4 = 36 celle teoriche, 6 celle critical):

| # | gameId | useLibraryGameDetail | useGameAgents (tab=agents) | UI Behavior |
|---|--------|---------------------|----------------------------|-------------|
| 1 | `null` | `disabled` | `disabled` | **Shell `not-found`**: hero illustrato + CTA back to /games. Nessuna fetch. |
| 2 | valid | `loading` | `disabled` (gated) | **Shell `loading`**: skeleton hero + skeleton tabs. Sub-hook NON montato. |
| 3 | valid | `error` | `disabled` (gated) | **Shell `error`**: error card + CTA retry. Sub-hook NON montato. |
| 4 | valid | `success(null)` | `disabled` (gated) | **Shell `not-found`**: game non trovato in library/catalog. |
| 5 | valid | `success(data)` | `disabled` (tab !== 'agents') | **Default render**: hero + tabs, agents tab placeholder. |
| 6 | valid | `success(data)` | `loading` (tab=agents) | **Default render** + Agents tab inline skeleton. |
| 7 | valid | `success(data)` | `error` (tab=agents) | **Default render** + Agents tab inline error banner (NO shell error). |
| 8 | valid | `success(data)` | `success([])` (tab=agents) | **Default render** + Agents tab empty state CTA "Crea agente". |
| 9 | valid | `success(data)` | `success([...])` (tab=agents) | **Default render** + Agents tab grid populated. |

### 3.1 Critical assertion contracts

- ⚠️ **Cell 4 vs Cell 1**: stesso UI shell `not-found` ma origini diverse. Test deve coprire entrambi.
- ⚠️ **Cell 7**: errore sub-hook NON degrada a shell error globale. Inline banner contestuale.
- ⚠️ **Cell 2 → Cell 5 transition**: skeleton non deve flash vuoto durante hydration. Stable transition.
- ⚠️ **Cell 5 → Cell 6 transition** (tab change): sub-hook fired solo al tab change, NON eager.

### 3.2 State derivation function

```ts
// apps/web/src/lib/games/game-detail-state.ts
export type GameDetailUiState = 'loading' | 'error' | 'not-found' | 'default';

export function deriveGameDetailUiState(input: {
  gameId: string | null;
  isLoading: boolean;
  isError: boolean;
  hasData: boolean;
}): GameDetailUiState {
  if (input.gameId == null) return 'not-found';
  if (input.isLoading) return 'loading';
  if (input.isError) return 'error';
  if (!input.hasData) return 'not-found';
  return 'default';
}
```

**Property test**: `gameId === null` short-circuita prima di ogni altro check. Wave C.1 PR #697 derivation ometteva il primo check.

## 4. Component contracts

### 4.1 Tab shell

```ts
type TabKey = 'info' | 'rules' | 'faqs' | 'sessions' | 'agents' | 'documents';

interface GameDetailTabsAnimatedProps {
  tabs: ReadonlyArray<{ key: TabKey; label: string; badge?: number }>;
  activeKey: TabKey;
  onChange: (key: TabKey) => void;
  ariaLabel: string;
}
```

**A11y contract** (gap noto da Wave C.1):
- `role="tablist"` su container
- `role="tab"` + `aria-selected={isActive}` + `aria-controls={panelId}` su ogni tab button
- `role="tabpanel"` + `aria-labelledby={tabId}` + `id={panelId}` su ogni panel container
- Keyboard nav: ArrowLeft/ArrowRight + wrap, Home/End jump (riusa `useTablistKeyboardNav` hook PR #623)
- `prefers-reduced-motion`: animated underline disabled

### 4.2 GameDetailHero

```ts
interface GameDetailHeroProps {
  variant: 'own' | 'community';  // own = libraryEntryId !== null
  title: string;
  subtitle: string | null;
  imageUrl?: string;
  meta: GameDetailHeroMeta;  // designer/year/players/duration/complexity/rating
  manaPips?: ManaPip[];
  ctaPlay?: () => void;
  ctaEdit?: () => void;
  ctaShare?: () => void;
  ctaAddToLibrary?: () => void;  // visible solo if variant === 'community'
  labels: GameDetailHeroLabels;  // i18n strings injected upfront
}
```

**Contract**:
- `variant === 'community'` → mostra "Add to Library" CTA, hide "Edit"
- `variant === 'own'` → mostra "Edit" + "Play" CTAs, hide "Add to Library"
- Tutti i CTA opzionali: assenza → button non renderizzato (no placeholder disabled)

### 4.3 GameDetailAgentsList (cell 6/7/8/9)

```ts
type AgentsState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'empty'; ctaCreate: () => void }
  | { kind: 'success'; agents: GameDetailAgentEntry[] };

interface GameDetailAgentsListProps {
  state: AgentsState;
  labels: GameDetailAgentsListLabels;
}
```

**Contract**:
- Stato discriminato (mai data + loading insieme)
- Retry callback per error state
- Empty state CTA ben distinto da error CTA
- Component pure (no hook chiamato dentro): orchestrator passa `state` derivato da `agentsQuery`

### 4.4 GameDetailFaqList / GameDetailRulesAccordion / GameDetailKbDocList / GameDetailSessionsRail

**Comune contract per tabs senza dedicated hook (al momento)**:
- Renderizzano empty state con CTA verso legacy subroute (`/games/[id]/{faqs,rules,sessions}`)
- Decisione architetturale: subroute pages NON migrate in questa wave (decision documented in spec sez. 4 wave C scope)
- Quando backend espone `/api/v1/games/{id}/{faqs,rules,sessions}` aggregati, queste tab si arricchiranno di hook propri (separato follow-up issue post-merge)

### 4.5 GameDetailKpiCards

Pure display component. Riceve `manaPips?: ManaPip[]` da `useGameManaPips(gameId)` o fixture.

## 5. Test coverage plan

Per Tier L spec sez. 4.1 ratio: **50% unit / 35% integration / 15% e2e**.

### 5.1 Unit tests (50% — ~25 tests)

| Target | Tests |
|--------|-------|
| `deriveGameDetailUiState` | Property tests per ogni cella matrix sez. 3 (10 tests) |
| `parseStateOverride` | Valid override / invalid / disabled in prod (3 tests) |
| `GameDetailHero` props matrix | variant own/community × CTAs presenti/assenti (4 tests) |
| `GameDetailAgentsList` AgentsState discriminated union | 4 stati × render shape (4 tests) |
| `GameDetailTabsAnimated` keyboard nav | Riusa test del `useTablistKeyboardNav` hook (smoke 4 tests) |

### 5.2 Integration tests (35% — ~18 tests)

Orchestrator-level via `renderHook` + MSW mocks:

| Target | Cell matrix coverage |
|--------|---------------------|
| `GameDetailViewV2` con `gameId === null` | Cell 1 |
| `GameDetailViewV2` con `useLibraryGameDetail.loading` | Cell 2 — verify sub-hooks NOT mounted |
| `GameDetailViewV2` con `useLibraryGameDetail.error` | Cell 3 — error shell, NO sub-hook fetch |
| `GameDetailViewV2` con `useLibraryGameDetail.success(null)` | Cell 4 — not-found shell |
| `GameDetailViewV2` tab change (info → agents) | Cell 5 → Cell 6 transition, sub-hook fired ON change |
| `GameDetailViewV2` agents tab error | Cell 7 — inline banner, NO shell error |
| `GameDetailViewV2` agents tab empty | Cell 8 — empty state CTA |
| `GameDetailViewV2` agents tab success | Cell 9 — grid populated |

**Critical assertion in tutti i test integration**:
```ts
// MSW handler per /api/v1/agents must NEVER receive 'undefined' or empty string
const agentsHandler = vi.fn((req) => {
  const url = new URL(req.url);
  const gameId = url.searchParams.get('gameId');
  expect(gameId).not.toBe('undefined');
  expect(gameId).not.toBe('');
  expect(gameId).toMatch(/^[a-f0-9-]{36}$/);
  return HttpResponse.json([]);
});
```

### 5.3 E2E tests (15% — ~5 specs)

- `e2e/v2-states/games-id.spec.ts` — 4 stati (loading / error / not-found / default) via `?state=` URL override
- `e2e/visual-migrated/sp4-game-detail.spec.ts` — visual baseline 1280×720 + 375×812
- `e2e/a11y/games-id.spec.ts` — axe-core WCAG 2.1 AA + reduced-motion contract
- `e2e/smoke-real-backend/game-detail.smoke.spec.ts` — real backend `gh workflow_dispatch` (post-merge nightly)

## 6. Bundle budget

Tier L per spec sez. 1.3: max +120 KB.
Estimate breakdown:
- 8 v2 components: ~50 KB
- Orchestrator: ~25 KB
- i18n keys (it+en): ~10 KB
- Visual-test fixture: ~5 KB
- Coexistence flag wiring (if applied): ~5 KB

**Target totale**: ~95 KB (margin 25 KB).

## 7. Coexistence flag (opzionale)

Per spec sez. 1.2 #2 amended: route Tier L può abilitare flag.

**Decisione**: per Wave C.1 retry **NON** usare coexistence flag. Razionale:
- App pre-prod, blast radius dev/staging only
- PR #697 chiusa pre-merge — niente revert necessario per artefatto sopravvivente
- Phase 0.5 + test mix robusto = confidence sufficiente per big-bang

**Se durante implementation emergono regression cross-route**, abilitare:
```tsx
const SHOW_V2 = process.env.NEXT_PUBLIC_GAME_DETAIL_V2 === '1';
return SHOW_V2 ? <GameDetailViewV2 id={id} /> : <GameDetailLegacy id={id} />;
```

Default `0` su `.env.local` dev, `1` su CI/staging.

## 8. Acceptance criteria

Phase 0.5 contract è **completo** quando:

- [x] Hook dependency graph documentato (sez. 2)
- [x] FSM cell matrix con ≥6 celle critical (sez. 3 — 9 celle documentate)
- [x] Component contracts per tutti gli 8 v2 components (sez. 4)
- [x] Test coverage plan Tier L (50/35/15) (sez. 5)
- [x] Bundle budget breakdown (sez. 6)
- [x] Decisione coexistence flag (sez. 7)

Implementation può procedere quando:

- [ ] Phase 0.5 PR mergiata su `main-dev`
- [ ] Code review approval su contract
- [ ] Implementation prompt subagent **referenzia esplicitamente questo file** + checklist FSM cells

## 9. References

- Spec V2 migration sez. 3.4 (Phase 0.5 obligation): `docs/superpowers/specs/2026-04-26-v2-design-migration.md`
- Migration matrix Tier classification: `docs/frontend/v2-migration-matrix.md`
- Issue #581 (Wave C umbrella)
- Closed PR #697 (Wave C.1 fail) — branch `feature/issue-581-wave-c1-game-detail-fe-v2` retained for reference
- Memory feedback: `feedback_v2-tier-dispatch-strategy.md`
- Mockup source: `admin-mockups/design_files/sp4-game-detail.jsx`

---

**Status**: DRAFT — pending review before implementation dispatch.
**Next step post-approval**: dispatch implementation subagent con prompt contenente reference a questo contract + checklist FSM cells da coprire.
