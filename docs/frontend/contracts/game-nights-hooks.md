# `/game-nights` Hook Contract — Wave 3 Phase 0.5

> **Phase 0.5 contract** per V2 migration spec sezione 3.4.
> **Tier**: L (≥3 hook composition, cartesian FSM con view-mode × month × filter × drawer state).
> **Scope**: orchestrator `GameNightsLibraryView` per route `/game-nights`.
> **Issue**: #685 (child di umbrella #681). **Mockup**: `admin-mockups/design_files/sp4-game-nights-index.{html,jsx}`.
>
> Pattern blueprint da Wave C.1 ([games-id-hooks.md](games-id-hooks.md)) e Wave C.2 ([agents-id-hooks.md](agents-id-hooks.md))
> adattato per **index page con view-mode toggle Calendar/List + drawer composition**, NO `[id]` URL param.

## 1. Route surface

| Aspect | Value |
|--------|-------|
| Route path | `/game-nights` |
| Page component | `apps/web/src/app/(authenticated)/game-nights/page.tsx` |
| Orchestrator | `apps/web/src/app/(authenticated)/game-nights/_components/GameNightsLibraryView.tsx` |
| URL state params | `?view=calendar\|list` · `?month=YYYY-MM` · `?filter=all\|organizing\|invited\|past` · `?day=YYYY-MM-DD` (drawer) |
| Param source | `useSearchParams()` (Suspense boundary required, mirror Wave 4 D1 `/players` pattern) |
| **Migration note** | Page legacy is server component. Wave 3 converte a client component thin shell con `<Suspense>` (mirror Wave C.2 page.tsx pattern, requirement Next.js 16 `useSearchParams()` CSR bailout). |
| **Default view per viewport** | Desktop (≥1024px): `calendar`. Mobile (<1024px): `list`. Override via URL persists across reloads. |

### 1.1 URL state schema (single source of truth, no useState mirror)

```ts
type ViewMode = 'calendar' | 'list';
type GameNightFilter = 'all' | 'organizing' | 'invited' | 'past';

interface UrlState {
  view: ViewMode;                  // default by viewport
  monthIso: string;                // YYYY-MM, default = current month
  filter: GameNightFilter;         // default 'all'
  drawerDayIso: string | null;     // YYYY-MM-DD, default null (drawer closed)
}

// Anti-pattern: NO useState<ViewMode> + URL sync hooks. URL è SSOT.
// Pattern Wave 4 D1: derive UrlState da searchParams + writeUrlState helper
//   con router.replace({ scroll: false, shallow: true }).
```

**`?state=` URL override** per visual fixture (gated by `STATE_OVERRIDE_ENABLED`):
- `?state=loading` → forza shell loading (Cell 2)
- `?state=empty` → forza fixture vuoto (Cell 5 — empty)
- `?state=filtered-empty` → forza fixture popolato + filter='past' senza past entries (Cell 6)
- `?state=error` → NO visual override (`isError` non riproducibile via URL deterministicamente, coperto unit test).

## 2. Hook dependency graph

```
useSearchParams() ──derive──→ UrlState { view, monthIso, filter, drawerDayIso }
                                  │
                                  ├─→ useUpcomingGameNights() [PARENT-A]
                                  │       └─→ STATE: loading | error | success([])
                                  │
                                  ├─→ useMyGameNights() [PARENT-B]
                                  │       └─→ STATE: loading | error | success([])
                                  │       (entrambi parent eseguiti in parallelo,
                                  │        il merge avviene client-side via deriveAllNights)
                                  │
                                  └─→ useGameNightRsvps(drawerDayIso ? selectedNightId : '')
                                         ─enabled: drawerDayIso !== null && nightForDay !== null
                                         └─→ STATE: disabled | loading | error | success([])
                                              (lazy fetch solo quando drawer aperto)
```

**Key constraints**:
1. **Doppio parent (Upcoming + Mine)**: schema BE non ha endpoint unico "all my game nights con filtri". Composition client-side via `deriveAllNights({ upcoming, mine })` con dedupe per `id`.
2. **Sub-hook gating drawer**: `useGameNightRsvps` montata SOLO con drawer aperto E night ID risolvibile dal day click. Pattern lazy mirror Wave C.1 sez. 2.2 (Cell 4 race-condition guard).
3. **Cumulative gate**: `enabled: drawerDayIso != null && selectedNight != null && parentSuccess`.
4. **Filter applicato client-side** sui parent merged data. NO refetch su filter change. Switch view-mode (Calendar/List) NON re-fetcha.
5. **Month navigation**: `monthIso` change triggers re-derive (slice di `allNights`), NO refetch (parent caricano tutto upcoming + mine — assunzione: dataset bounded, <100 entries tipico).

### 2.1 UrlState resolution

```ts
// CORRETTO — Wave 4 D1 pattern + viewport-aware default per `view`
const searchParams = useSearchParams();
const isDesktop = useMediaQuery('(min-width: 1024px)');

const urlState: UrlState = useMemo(() => ({
  view: parseViewMode(searchParams.get('view'), isDesktop ? 'calendar' : 'list'),
  monthIso: parseMonthIso(searchParams.get('month'), currentMonthIso()),
  filter: parseFilter(searchParams.get('filter'), 'all'),
  drawerDayIso: parseDayIso(searchParams.get('day')),  // null se invalid o assente
}), [searchParams, isDesktop]);

// SBAGLIATO (anti-pattern):
//   const [view, setView] = useState<ViewMode>('calendar');  // duplica URL state
//   const [drawerDay, setDrawerDay] = useState<string | null>(null);  // diverge da URL
```

### 2.2 Sub-hook gating (drawer)

```ts
// useGameNightRsvps — montato solo a drawer aperto + night risolvibile
const selectedNight = useMemo(() => {
  if (!urlState.drawerDayIso) return null;
  return allNights.find(n => isoDayMatch(n.scheduledAt, urlState.drawerDayIso)) ?? null;
}, [allNights, urlState.drawerDayIso]);

const rsvpsQuery = useGameNightRsvps(selectedNight?.id ?? '');
// Note: useGameNightRsvps signature attuale richiede `id: string`, internal `enabled: !!id`.
// Pattern: passare empty string '' quando non vogliamo fetch — internal gate copre.
// Acceptable per Phase 0.5 — alternativa: refactor signature a {id, enabled} (TBD post-impl).
```

**Anti-pattern dal Wave C.1 PR #697 e da evitare**:
- ❌ `useGameNightRsvps(drawerDayIso ?? 'undefined')` (literal string undefined)
- ❌ Skip `selectedNight != null` check (race con drawer aperto su giorno senza night)
- ❌ Filter `?filter=past` triggera refetch parent (anti-pattern: filter is client-side derivation)

## 3. FSM cell matrix

Cartesian rilevante (subset di celle critical, cartesian view × parent × filter × drawer):

### 3.1 Index-level FSM (5-state, view-mode invariant)

| # | parents (upcoming+mine) | filter | filtered count | UI Behavior |
|---|------------------------|--------|----------------|-------------|
| 1 | both `loading` | any | — | **Shell `loading`**: skeleton header + skeleton calendar/list per `view`. |
| 2 | both `error` OR critical error | any | — | **Shell `error`**: error card + CTA retry both queries. |
| 3 | both `success([])` | any | 0 | **Shell `empty`**: hero illustrato + CTA "Crea la tua prima Game Night". |
| 4 | both `success(non-empty)` | `all` | >0 | **Shell `default`**: header + view (Calendar/List) populated. |
| 5 | both `success(non-empty)` | `organizing\|invited\|past` | 0 | **Shell `filtered-empty`**: stessa shell `default` MA view content mostra empty-state contestuale ("Nessuna serata corrispondente al filtro X"). |
| 6 | mixed (one error, one success) | any | derived | **Shell `default`** + warning banner "Alcuni dati potrebbero essere incompleti" (graceful degradation). |

### 3.2 Drawer-level FSM (independente dal index FSM, attivo solo con drawerDayIso != null)

| # | drawerDayIso | selectedNight | rsvpsQuery | UI Behavior |
|---|--------------|---------------|------------|-------------|
| D1 | null | — | `disabled` | **Drawer chiuso** (nessuna fetch). |
| D2 | valid ISO | null | `disabled` | **Drawer aperto** + content "Nessuna serata in questa data" (state da day-click su cella vuota — protect). |
| D3 | valid ISO | non-null | `loading` | **Drawer aperto** + skeleton RSVP list. |
| D4 | valid ISO | non-null | `error` | **Drawer aperto** + error card RSVP + CTA retry. |
| D5 | valid ISO | non-null | `success([])` | **Drawer aperto** + RSVP empty state ("Nessuna risposta ancora"). |
| D6 | valid ISO | non-null | `success([...])` | **Drawer aperto** + RSVP list. |

### 3.3 Critical assertion contracts

- ⚠️ **Cell 5 (filtered-empty) NON è Cell 3 (empty)**: Cell 3 = nessun dato totale → CTA crea nuova; Cell 5 = dati esistono ma filter scope vuoto → CTA "Rimuovi filtro". Test must distinguere via `data-slot`.
- ⚠️ **Cell 6 (mixed)**: parent dual-fetch può fallire parzialmente. UX deve continuare a renderizzare la parte success + warning banner (graceful), NON full error shell.
- ⚠️ **Cell D2 protect**: clic su giorno senza night → drawer si apre vuoto (illegal state). Mitigazione: `CalendarDayCell` con `aria-disabled` se nessuna night, no click handler. List-card-click sempre garantisce night valido.
- ⚠️ **View-mode toggle preserve drawer**: switch Calendar→List NON deve chiudere drawer aperto. URL state preserves `?day=`.

### 3.4 State derivation function

```ts
// apps/web/src/lib/game-nights/game-nights-state.ts
export type GameNightsUiState =
  | 'loading'
  | 'error'
  | 'empty'
  | 'filtered-empty'
  | 'default';

export function deriveGameNightsUiState(input: {
  upcomingQuery: { isLoading: boolean; isError: boolean; data?: unknown };
  mineQuery: { isLoading: boolean; isError: boolean; data?: unknown };
  filteredCount: number;
  totalCount: number;
}): GameNightsUiState {
  const bothLoading = input.upcomingQuery.isLoading && input.mineQuery.isLoading;
  const bothError = input.upcomingQuery.isError && input.mineQuery.isError;

  if (bothLoading) return 'loading';                          // Cell 1
  if (bothError) return 'error';                              // Cell 2
  if (input.totalCount === 0) return 'empty';                 // Cell 3
  if (input.filteredCount === 0) return 'filtered-empty';     // Cell 5
  return 'default';                                            // Cells 4, 6
}
```

### 3.5 Drawer state derivation

```ts
export type DrawerUiState =
  | 'closed'
  | 'open-no-night'
  | 'open-loading'
  | 'open-error'
  | 'open-empty-rsvp'
  | 'open-success';

export function deriveDrawerUiState(input: {
  drawerDayIso: string | null;
  selectedNight: GameNightDto | null;
  rsvpsQuery: { isLoading: boolean; isError: boolean; data?: ReadonlyArray<unknown> };
}): DrawerUiState {
  if (input.drawerDayIso == null) return 'closed';            // D1
  if (input.selectedNight == null) return 'open-no-night';    // D2
  if (input.rsvpsQuery.isLoading) return 'open-loading';      // D3
  if (input.rsvpsQuery.isError) return 'open-error';          // D4
  if ((input.rsvpsQuery.data?.length ?? 0) === 0) return 'open-empty-rsvp';  // D5
  return 'open-success';                                       // D6
}
```

## 4. Component contracts

### 4.1 GameNightsHeader

```ts
interface GameNightsHeaderProps {
  badgeCategory: string;                    // "Eventi" / "Events"
  title: string;                            // "Le tue Game Nights"
  count: number;                            // total filtered count
  filter: GameNightFilter;
  onFilterChange: (next: GameNightFilter) => void;
  view: ViewMode;
  onViewChange: (next: ViewMode) => void;
  ctaCreate: () => void;                    // "+ Nuova"
  labels: GameNightsHeaderLabels;
}
```

**Pattern animation**: `tabs-animated-underline` per Calendar/List toggle (riuso Wave 1 primitive). FilterPillBar inline a destra.

### 4.2 CalendarMonthGrid

```ts
interface CalendarMonthGridProps {
  monthIso: string;                          // YYYY-MM
  todayIso: string;                          // YYYY-MM-DD
  nights: ReadonlyArray<GameNightDto>;       // already filtered
  selectedDayIso: string | null;
  onMonthChange: (next: string) => void;     // YYYY-MM
  onDayClick: (dayIso: string) => void;
  labels: CalendarLabels;
}
```

**Render rules**:
- 7-col grid Lun-Dom (i18n locale-aware first day, but mockup uses Lun)
- Empty days: `aria-disabled="true" tabindex="-1"`, no click
- Days with nights: focusable, click → `onDayClick` → URL update `?day=YYYY-MM-DD`
- Today cell: `aria-current="date"` + visual ring
- Selected day: `aria-pressed="true"` + entity-event background

### 4.3 CalendarDayCell

```ts
interface CalendarDayCellProps {
  dayIso: string;                            // YYYY-MM-DD
  dayNumber: number;                         // 1-31
  isToday: boolean;
  isSelected: boolean;
  isOutsideMonth: boolean;                   // for greyed-out adjacent month days
  nights: ReadonlyArray<GameNightDto>;       // max 3 displayed + "+N altri"
  onClick: () => void;
  labels: CalendarDayCellLabels;
}
```

**Render rules**:
- Max 3 event chip displayed via `EntityChip type="event"`
- Overflow: `+{N}` chip with `aria-label="N altre serate"`
- Status colored (event chip background varies by status: confirmed/planned/cancelled/completed)
- A11y: `role="gridcell"` + descriptive `aria-label="12 marzo, 3 serate"`

### 4.4 GameNightListCard

```ts
interface GameNightListCardProps {
  night: GameNightDto;
  isPast: boolean;                           // derived from scheduledAt < now
  onClick: () => void;                       // opens drawer
  ctaActions?: {                             // contextual per role + status
    rsvpYes?: () => void;
    rsvpMaybe?: () => void;
    rsvpNo?: () => void;
    edit?: () => void;
    cancel?: () => void;
  };
  labels: GameNightListCardLabels;
}
```

**Variant matrix per role+status**:
- `role=organizer` + `status=planned` → CTA "Pubblica" + "Modifica"
- `role=organizer` + `status=confirmed` → CTA "Modifica" + "Annulla"
- `role=invited` + `status=confirmed/planned` → CTA RSVP (Yes/Maybe/No)
- `role=*` + `status=cancelled/completed` → no CTA, read-only

### 4.5 DayDetailDrawer

```ts
interface DayDetailDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  drawerState: DrawerUiState;
  night: GameNightDto | null;
  rsvps: ReadonlyArray<RsvpEntry>;            // empty if not loaded
  isMobile: boolean;                          // toggle slide-right (desktop) vs bottom-sheet (mobile)
  ctaActions: GameNightListCardProps['ctaActions'];
  labels: DayDetailDrawerLabels;
}
```

**A11y critical** (Wave A.4 lessons):
- `role="dialog" aria-modal="true" aria-labelledby={titleId}`
- Focus trap on open, restore focus on close
- Escape key closes
- `aria-hidden="true"` on background + overlay click closes
- Reduced-motion: snap open/close (no slide animation)

### 4.6 FilterPillBar

```ts
interface FilterPillBarProps {
  current: GameNightFilter;
  onChange: (next: GameNightFilter) => void;
  counts: Record<GameNightFilter, number>;    // optional badge per pill
  labels: FilterPillBarLabels;
}
```

**4 pills**: Tutte (all) · Organizzo (organizing) · Invitato (invited) · Concluse (past).

A11y: `role="tablist" aria-orientation="horizontal"`, ogni pill `role="tab" aria-selected aria-controls="game-nights-content"`. Roving tabindex + ArrowLeft/Right wrap (riuso `useTablistKeyboardNav` hook PR #623).

### 4.7 StatusPill

```ts
interface StatusPillProps {
  status: 'confirmed' | 'planned' | 'cancelled' | 'completed';
  labels: Record<StatusPillProps['status'], string>;
}
```

**Token mapping**:
- `confirmed` → `bg-toolkit-100/12 text-toolkit-700` (toolkit teal)
- `planned` → `bg-agent-100/12 text-agent-700` (agent purple)
- `cancelled` → `bg-rose-100 text-rose-700`
- `completed` → `bg-slate-100 text-slate-700`

### 4.8 PlayerAvatars

```ts
interface PlayerAvatarsProps {
  playerIds: ReadonlyArray<string>;
  maxVisible?: number;                       // default 3
  size?: 'sm' | 'md';                        // default 'sm'
  labels: PlayerAvatarsLabels;
}
```

**Render rules**:
- Max N avatars overlap stack (-8px margin-left)
- Overflow: `+{N}` chip avatar-shaped
- A11y: `role="list" aria-label="N partecipanti"`, ogni avatar `role="listitem"`

### 4.9 ⚠️ A11y CTA contrast pre-emption (Wave C.1 lesson, sustained)

- White-text CTA → 700-shade Tailwind: `bg-emerald-700`, `bg-amber-700`, `bg-event-700`, `bg-rose-700`.
- Audit pre-implementation: `grep -E "bg-(emerald|amber|event|rose|toolkit|agent)-600" components/v2/game-nights/`.

## 5. Test coverage plan

Per Tier L spec sez. 4.1 ratio: **50% unit / 35% integration / 15% e2e**.

### 5.1 Unit tests (~50% — ~50 tests)

| Target | Tests |
|--------|-------|
| `deriveGameNightsUiState` | Cells 1-6 + edge cases (mixed parent error, totalCount=0 vs filteredCount=0) — 12 tests |
| `deriveDrawerUiState` | Cells D1-D6 — 6 tests |
| `parseViewMode` / `parseMonthIso` / `parseFilter` / `parseDayIso` | URL parser per param × valid/invalid/disabled — 16 tests |
| `deriveAllNights` | Dedup upcoming + mine, sort scheduledAt asc — 4 tests |
| `applyFilter` | filter='all'/'organizing'/'invited'/'past' shape per role+status — 8 tests |
| `mapNightsToCalendar` | Month slice + dayIso index — 6 tests |
| 8 v2 components | Render shape per state, variant matrix (CalendarDayCell role+status, GameNightListCard role+status), discriminated unions — ~40 tests |

### 5.2 Integration tests (35% — ~25 tests)

Orchestrator-level via `renderHook` + MSW mocks. Cell matrix coverage:

| Target | Cell |
|--------|------|
| Both parent loading | Cell 1 |
| Both parent error | Cell 2 |
| Both parent empty | Cell 3 |
| Filter='past' on dataset without past entries | Cell 5 |
| One parent error one success | Cell 6 + warning banner |
| View toggle Calendar→List preserves URL state | (interaction) |
| Filter change URL update + client-side filter | (interaction) |
| Month nav `‹›` updates URL `?month=` | (interaction) |
| Day click in Calendar opens drawer with `?day=` | Cell D3-D6 |
| List card click opens drawer | Cell D3-D6 |
| Drawer Escape closes (`?day=` removed) | Cell D1 |
| `useGameNightRsvps.error` (drawer open) | Cell D4 |
| RSVP CTA mutates → invalidates queryKey | (mutation) |

**Critical assertion in tutti i test integration**:
```ts
const rsvpsHandler = vi.fn((req) => {
  const url = new URL(req.url);
  const id = url.pathname.split('/').filter(Boolean).at(-2);  // /game-nights/:id/rsvps
  expect(id).not.toBe('undefined');
  expect(id).not.toBe('null');
  expect(id).not.toBe('');
  expect(id).toMatch(/^[a-f0-9-]{36}$/);
  return HttpResponse.json([]);
});
```

### 5.3 E2E tests (15% — ~6 specs)

- `e2e/v2-states/game-nights.spec.ts` — 5 stati × 2 viewports × 2 view-modes (calendar+list) = 20 PNG (filtered-empty + drawer-open variants)
- `e2e/visual-migrated/sp4-game-nights-index.spec.ts` — visual baseline 1280×720 calendar + 1280×720 list + 375×812 list (3 PNG canonical)
- `e2e/a11y/game-nights.spec.ts` — axe-core WCAG 2.1 AA + reduced-motion contract + keyboard nav drawer + focus trap drawer
- `e2e/smoke-real-backend/game-nights.smoke.spec.ts` — deterministic seed, default view per viewport

## 6. Bundle budget

Tier L per spec sez. 1.3: max +120 KB. Estimate:
- 8 v2 components: ~52 KB (CalendarMonthGrid + DayDetailDrawer più pesanti)
- Orchestrator: ~22 KB (URL state + dual parent compose + drawer derive)
- i18n keys (it+en): ~10 KB
- Visual fixture: ~6 KB
- date helpers (fns slice): ~5 KB

**Target totale**: ~95 KB (margin 25 KB).

## 7. Coexistence flag

**Decisione**: NO flag (mirror Wave C.1/C.2/B.3 — app pre-prod, big-bang accettabile post-Phase 0.5).

Rollback path: `git revert` PR squash commit + `next.config.js` non ha redirect `/game-nights → *` (verified — see audit checklist).

## 8. Pre-implementation audit checklist (Wave C.1/C.2 lessons sustained)

⚠️ **Verificare PRIMA del dispatch implementation subagent**:

- [ ] **Redirect cleanup**: `grep -n "/game-nights" apps/web/next.config.js` — confirmed empty (no redirects defined for this route). Verificare ancora pre-merge.
- [ ] **Proxy.ts rewrite**: `grep -n "game-nights" apps/web/src/proxy.ts` — verificare nessun rewrite intercetta path
- [ ] **Subroute preservation**: `/game-nights/[id]` (detail) e `/game-nights/[id]/rsvp` (token-based RSVP, Wave A.5b) NON devono essere toccati. Wave 3 limita scope a `/game-nights` index.
- [ ] **A11y CTA contrast pre-emption**: 700-shade Tailwind per white text (sez. 4.9)
- [ ] **Page boundary normalization**: page.tsx convertita da server a client thin shell con `<Suspense>` (CRITICAL per `useSearchParams()`)
- [ ] **Triple auth helper** in E2E specs: `seedAuthSession` + `seedCookieConsent` + `mockAuthEndpoints`
- [ ] **Drawer focus trap**: verificare `focus-trap-react` o equivalente già presente nelle deps (riuso pattern Wave A.4 invites drawer se esiste, altrimenti add to bundle)
- [ ] **`useTablistKeyboardNav` riuso** per FilterPillBar (PR #623) e header view-toggle se applicabile
- [ ] **Default view-mode by viewport**: `useMediaQuery('(min-width: 1024px)')` SSR-safe (`useSyncExternalStore` o equivalente per evitare hydration mismatch)

## 9. References

- Phase 0.5 contract Wave C.1 (template): `docs/frontend/contracts/games-id-hooks.md`
- Phase 0.5 contract Wave C.2 (template multi-tab): `docs/frontend/contracts/agents-id-hooks.md`
- Spec V2 migration sez. 3.4: `docs/superpowers/specs/2026-04-26-v2-design-migration.md`
- Migration matrix Tier classification: `docs/frontend/v2-migration-matrix.md`
- Issue #685 (child Wave 3), #681 (umbrella Wave 3 + Wave 4 partial)
- Hooks già esistenti: `apps/web/src/hooks/queries/useGameNights.ts` (9 hooks)
- Mockup source: `admin-mockups/design_files/sp4-game-nights-index.{html,jsx}`
- Memory feedback files:
  - `feedback_v2-tier-dispatch-strategy.md` (Phase 0.5 pattern validated Wave C.1/C.2)
  - `feedback_brownfield-route-redirect-audit.md` (redirect cleanup gotcha)
- Wave precedenti pattern reuse:
  - Wave 4 D1 PR #717: URL state SSOT pattern + `useSearchParams + Suspense` thin shell
  - Wave A.4 PR #618: drawer focus-trap + ESC close pattern (invites)
  - Wave B.1 PR #635: visual fixture sentinel pattern + `?state=` URL override
  - PR #623: `useTablistKeyboardNav` hook per FilterPillBar

---

**Status**: DRAFT — pending review before implementation dispatch.
**Next step post-approval**: dispatch implementation subagent con prompt che referenzia esplicitamente questo contract + checklist FSM cells (6 index cells + 6 drawer cells = 12 cells) + 8 components + Tier L test ratio 50/35/15.
