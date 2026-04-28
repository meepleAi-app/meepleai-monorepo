# V2 Migration Wave A.3b — Frontend `/shared-games` (Greenfield)

> **Issue**: #596 (sub-issue di #579 V2 Phase 1 Wave A)
> **Parent**: Wave A.3a (#593 / PR #594 merged 2026-04-28 `91eb3b4de`)
> **Status**: In progress
> **Owner**: Frontend
> **Spec date**: 2026-04-28
> **Methodology**: spec-panel ultrathink (Wiegers / Adzic / Fowler / Crispin / Nygard)

---

## 1. Goal & Non-Goals

### 1.1 Goal

Costruire la route pubblica `/shared-games` (greenfield: oggi esiste solo `[id]/page.tsx`) sfruttando l'estensione backend Wave A.3a già deployata. Replica fedele del mockup `admin-mockups/design_files/sp3-shared-games.jsx` (1108 LOC) con 5 stati di rendering, filtri AND-logic, sidebar contributori, e UX state riflessa nell'URL via hash.

### 1.2 Non-Goals (deferiti)

| Item | Motivazione | Tracker |
|------|-------------|---------|
| Pagination / infinite scroll | `pageSize=100` hardcoded copre alpha catalog (~113 giochi indicizzati, < soglia) | TODO issue dedicata |
| Mobile drawer per contributori | Sidebar nascosta sotto `md` breakpoint, scope creep | Wave A.4 |
| Detail page edits (`[id]/page.tsx`) | Wave A.4 dedicata | A.4 |
| Aggregate `ContributorsCount` union (Toolkits + Agents + KBs) | Backend conta solo Toolkits oggi (vedi blind spot §6.2) | Issue follow-up |

---

## 2. Discovery Summary

### 2.1 Backend contract verificato (PR #594)

`SharedGameDto` (line 22-47) — 23 campi, 7 nuovi:

```csharp
public sealed record SharedGameDto(
    // ... 16 campi base ...
    int ToolkitsCount = 0,        // tk
    int AgentsCount = 0,          // ag
    int KbsCount = 0,             // kb
    int NewThisWeekCount = 0,     // newWeek (sessions in last 7d)
    int ContributorsCount = 0,    // contrib (DISTINCT users w/ Toolkit)
    bool IsTopRated = false,      // averageRating ≥ TopRatedThreshold
    bool IsNew = false);          // CreatedAt within NewWindowDays
```

**Endpoint** `GET /shared-games` (line 88-132) — filtri esistenti + 4 nuovi: `hasToolkit`, `hasAgent`, `isTopRated`, `isNew`. SortBy enum: `Title|YearPublished|AverageRating|CreatedAt|ComplexityRating|Contrib|New`.

**Endpoint** `GET /shared-games/top-contributors?limit=5` (line 176-184) — restituisce `List<TopContributorDto>` con shape `{userId, displayName, avatarUrl, totalSessions, totalWins, score}`. Score = `totalSessions + totalWins * 2`.

**Cache**: HybridCache TTL L1 15min/L2 1h, tags `search-games` + `top-contributors`, invalidation su mutation di game/session/toolkit.

### 2.2 Mockup audit (1108 LOC)

5 stati rendering:

1. **default** — 24+ cards, sort=rating desc, no filters
2. **loading** — skeleton grid 3×3 (9 cards)
3. **empty-search** — `q="zorglub"`, 0 results → CTA "Reset cerca"
4. **filtered-empty** — chip + genere combinati 0 results → CTA "Reset filtri"
5. **api-error** — Banner tone=error + retry button (NON in mockup, aggiunto per resilienza P1)

Filtri: 4 chips (`con toolkit / con agente / top-rated / nuovi`), genere `<Select>`, sort `<Select>` con 4 opzioni, search `<Input>` con `aria-label` e debounce 300ms.

Responsive: sidebar `hidden md:block` (>= 1200px logical, > md tailwind), grid 1col mobile / 2col tablet / 3col desktop.

i18n keys (~32) sotto namespace `pages.shared-games.*` flat.

### 2.3 Frontend patterns audit

- Public route group `(public)` — già esiste con `PublicLayout` (UnifiedHeader + PublicFooter)
- `page.tsx` (server) + `page-client.tsx` (client) — split standard, vedi `(public)/library/page.tsx`
- URL state — native `useSearchParams` + `useRouter` + `usePathname` (NO custom hook lib)
- API client — factory in `src/lib/api/clients/` + Zod schemas in `src/lib/api/schemas/`
- React Query — factory pattern con `*Keys` factory
- v2 components — `src/components/ui/v2/{kebab-domain}/PascalCase.tsx`
- MeepleCard — 6 variants via dispatcher `meeple-card.tsx`; `entity="game"` ha colore amber

---

## 3. Multi-Expert Spec-Panel Critique

### 3.1 Wiegers — Requirements quality

**Verdict**: 11/13 DoD checkboxes are measurable. 2 require sharpening.

| DoD item | Quality | Note |
|----------|---------|------|
| Route `/shared-games` v2 implementata 1:1 | 75% | "1:1" ambiguo — uso visual diff < 0.1 threshold (Playwright default) |
| Server+Client split (SSR initial state per LCP) | OK | Misurabile via build output: il page.tsx rende un placeholder che hydration sostituisce |
| `useUrlHashState<T>` generico, multi-key sync | OK | Test specifica multi-key + SSR-safe |
| Search debounce 300ms | OK | Misurabile via fake timer in Vitest |
| 5 stati coperti | OK | Visual + behavioral test per ogni stato |
| Visual baseline 1 default × 2 viewport (2 PNG) | OK | Playwright `toHaveScreenshot()` |
| v2-states baseline 5 stati × 2 viewport (10 PNG via CI bootstrap) | 75% | CI bootstrap deve essere documentato (vedi §7.2) |
| Behavioral unit tests (hook, filters, sidebar, card a11y) | OK | Inventario in §7.1 |
| axe-core 0 violations | OK | Playwright + `@axe-core/playwright` |
| Bundle delta < +50 KB | OK | Confronto vs `bundle-size-baseline.json` |
| WCAG AA, MeepleCardGame come `<a href>` | OK | Render test: `<a>` element, keyboard navigable |
| i18n IT/EN complete | OK | Lint check su `useTranslation` keys missing |
| Backend config `TopRatedThreshold` allineato a 4.0 | OK | `appsettings.json` diff |

**Wiegers raccomanda**: aggiungere thresholds espliciti per "1:1 visual" (Playwright `maxDiffPixelRatio: 0.01`) e per il behavior "search debounce 300ms" (verificare che `setSearch` non triggeri fetch prima di `vi.advanceTimersByTime(300)`).

### 3.2 Adzic — Given/When/Then scenarios

**Stato 1 — Default**:
```gherkin
Given utente non autenticato visita /shared-games
When server-side render completa
Then mostra grid di N giochi (N >= 12) sort=rating desc
And mostra sidebar contributori (>= md viewport) con top-5
And nessun chip è attivo (aria-pressed=false su tutti)
And input search è vuoto
And select genere = "tutti"
And sort = "rating"
```

**Stato 2 — Loading**:
```gherkin
Given utente cambia chip "con toolkit" → attivo
When useQuery transition isFetching=true
Then mostra skeleton grid 3×3 (9 cards)
And chip "con toolkit" mostra aria-pressed=true durante loading
And input/select disabilitati durante mutation? NO — mantieni interattivi (no double-click prevent necessario, query-key dedup gestisce)
```

**Stato 3 — Empty-search**:
```gherkin
Given utente digita "zorglub" in input search
When dopo 300ms debounce, query fetch completa con total=0
Then mostra empty state con messaggio "Nessun gioco trovato per 'zorglub'"
And mostra CTA primaria "Reset cerca" che resetta solo q (non chip/genere)
And aria-live=polite annuncia il count "0 giochi"
```

**Stato 4 — Filtered-empty**:
```gherkin
Given utente attiva chip "top-rated" + genere "card-game"
When query fetch ritorna total=0
Then mostra empty state "Nessun gioco corrisponde ai filtri"
And mostra CTA "Reset filtri" che cancella chips+genere ma preserva q
And l'URL hash riflette `#chips=top&genre=card`
```

**Stato 5 — Api-error**:
```gherkin
Given query fetch fallisce (rete down / 5xx / timeout)
When useQuery.error popolato
Then mostra Banner tone=error con messaggio i18n "Impossibile caricare il catalogo"
And mostra Button "Riprova" che invoca refetch()
And la sidebar contributori si comporta indipendentemente (può mostrare errore separato)
And non c'è skeleton — l'errore sostituisce la grid
```

**Filter chip combinations** (matrix AND):

```gherkin
Given chip "con toolkit" attivo
When utente attiva chip "nuovi"
Then query params include hasToolkit=true&isNew=true
And total dei risultati <= total con solo "con toolkit"
And entrambi chip aria-pressed=true
And URL hash = #chips=tk,new
```

### 3.3 Fowler — Architecture

**Decisione 1 — URL state strategy**: hash-only? query-only? entrambi?

→ **Decisione**: solo `hash` per filtri/search/sort, niente query-string. Motivazione:
- Ricerca testuale + chip non sono crawlabili da SEO (catalogo è il SEO target, non i filtri)
- Hash non triggera SSR re-fetch (preserva LCP)
- Hash è bookmarkabile, condivisibile via copia-incolla
- `useUrlHashState<T>` riusabile A.4/A.5 (DRY)
- Trade-off accettato: refresh F5 mantiene lo stato, ma la SSR initial state legge solo defaults — il client hydrata e applica hash

**Decisione 2 — `useUrlHashState<T>` API surface**:

```typescript
function useUrlHashState<T extends Record<string, string | string[] | boolean | undefined>>(
  schema: { [K in keyof T]: { decode: (raw: string) => T[K]; encode: (v: T[K]) => string } },
  defaults: T,
): [T, (patch: Partial<T>) => void, () => void /* reset */];
```

Multi-key encoded come `#k1=v1&k2=v2,v3&k3=true`. SSR-safe: durante `typeof window === 'undefined'` ritorna `defaults`. Hydration applica hash via `useEffect`. Update via `history.replaceState` (no scroll, no rerender lato browser). Listener `hashchange` per back/forward.

**Decisione 3 — SSR initial state**:

```tsx
// page.tsx (server)
export default async function SharedGamesPage() {
  // SSR fetch: defaults (no filters), per warm cache + SEO content
  const initial = await api.searchSharedGames({ pageSize: 100, sortBy: 'AverageRating', sortDescending: true });
  return <SharedGamesPageClient initialData={initial} />;
}
```

```tsx
// page-client.tsx
'use client';
export function SharedGamesPageClient({ initialData }: { initialData: PagedSharedGames }) {
  const [state] = useUrlHashState(...);
  const { data } = useQuery({
    queryKey: ['shared-games', 'search', state],
    queryFn: () => api.searchSharedGames(buildParams(state)),
    initialData: deepEqual(state, defaults) ? initialData : undefined,
    placeholderData: keepPreviousData,
  });
  // ...
}
```

`initialData` viene applicato solo se lo state è uguale ai defaults — altrimenti React Query parte da fresh fetch.

**Decisione 4 — Component composition**:

```
SharedGamesPageClient
├── SharedGamesHeader (h1 + count summary)
├── SharedGamesFilters
│   ├── SearchInput (debounced)
│   ├── ChipGroup (4 chips)
│   ├── GenreSelect
│   └── SortSelect
├── <main>
│   ├── SharedGamesGrid (5 stati: default | loading | empty-search | filtered-empty | api-error)
│   │   └── MeepleCardGame[] | MeepleCardGameSkeleton[] | EmptyState | ErrorBanner
│   └── ContributorsSidebar (md+ only)
```

`SharedGamesPageClient` orchestra; ogni componente è dumb e prop-driven.

### 3.4 Crispin — Test strategy

**Visual baseline matrix** (12 PNG):

| Stato | Viewport | File |
|-------|----------|------|
| default | 1440×900 | `shared-games__default__desktop.png` |
| default | 768×1024 | `shared-games__default__tablet.png` |
| loading | 1440×900 | `shared-games__loading__desktop.png` |
| loading | 768×1024 | `shared-games__loading__tablet.png` |
| empty-search | 1440×900 | `shared-games__empty-search__desktop.png` |
| empty-search | 768×1024 | `shared-games__empty-search__tablet.png` |
| filtered-empty | 1440×900 | `shared-games__filtered-empty__desktop.png` |
| filtered-empty | 768×1024 | `shared-games__filtered-empty__tablet.png` |
| api-error | 1440×900 | `shared-games__api-error__desktop.png` |
| api-error | 768×1024 | `shared-games__api-error__tablet.png` |

CI bootstrap: la prima run su CI genera i PNG mancanti (commit-friendly), le run successive falliscono se diff > `maxDiffPixelRatio: 0.01`.

**Behavioral unit tests** (Vitest, target 85%+):

1. `useUrlHashState` (~12 tests): SSR-safe, multi-key encode/decode, partial patch, reset, `hashchange` listener, history.replaceState (no scroll), boolean encoding, array encoding (comma-separated), special chars URL-encoded
2. `SharedGamesFilters` (~8 tests): chip toggle aria-pressed, debounce 300ms (fake timer), genre/sort change, reset button states
3. `ContributorsSidebar` (~5 tests): top-5 ordering, score formatting, avatar fallback, loading skeleton, error fallback (silent — sidebar non blocca)
4. `MeepleCardGame` (~6 tests): rendered as `<a href>`, keyboard navigable (Enter/Space), aria-label include rating, badge tier visibility, image fallback su 404, click area copre tutto il card

**axe-core**: tutti e 5 gli stati devono passare `expect(violations).toHaveLength(0)`.

### 3.5 Nygard — Production resilience

**Failure modes**:

| Failure | Detection | Recovery |
|---------|-----------|----------|
| `/shared-games` query 5xx | `useQuery.error` populated | Banner + retry button (manual refetch) |
| `/shared-games` query slow (>3s) | `isFetching=true` after 3s | Mostra skeleton (no UX freeze) |
| `/top-contributors` 5xx | sidebar query separata | Sidebar mostra messaggio fallback `"Contributori non disponibili"`, NON blocca grid |
| `/top-contributors` slow | parallel fetch | Sidebar mostra skeleton, grid renderizza indipendentemente |
| Network offline | `navigator.onLine === false` | React Query retry policy: 3 tentativi exp backoff (default httpClient) |
| Cache miss + slow render | SSR data lag | `placeholderData: keepPreviousData` + skeleton |

**Retry policy**: per `/shared-games` la default httpClient policy (3 retries) è OK. Per `/top-contributors` accetto fail-silent dopo 1 tentativo (non critical path).

**Performance budget**:
- LCP server-rendered grid: < 1.5s p75 (no client fetch in critical path)
- Time-to-interactive: < 2.5s p75 (hydration completa)
- Bundle delta: < +50 KB (issue DoD constraint)

---

## 4. Architecture Decisions

### 4.1 URL state model

```typescript
type SharedGamesState = {
  q: string;                        // search query
  chips: ('tk' | 'ag' | 'top' | 'new')[];  // multi-select AND logic
  genre: string;                    // category slug, '' = all
  sort: 'rating' | 'contrib' | 'new' | 'title';
};

const defaults: SharedGamesState = {
  q: '',
  chips: [],
  genre: '',
  sort: 'rating',
};

// URL hash example: #q=catan&chips=tk,top&genre=family&sort=rating
```

**Mapping → backend params**:

```typescript
function buildSearchParams(state: SharedGamesState): SearchSharedGamesParams {
  return {
    search: state.q || undefined,
    hasToolkit: state.chips.includes('tk') ? true : undefined,
    hasAgent: state.chips.includes('ag') ? true : undefined,
    isTopRated: state.chips.includes('top') ? true : undefined,
    isNew: state.chips.includes('new') ? true : undefined,
    categoryIds: state.genre ? [genreSlugToId(state.genre)] : undefined,
    sortBy: SORT_MAP[state.sort],          // 'AverageRating' | 'Contrib' | 'New' | 'Title'
    sortDescending: state.sort !== 'title',
    pageSize: 100,
    pageNumber: 1,
  };
}
```

### 4.2 Backend config alignment

`apps/api/src/Api/appsettings.json` — change:

```jsonc
"SharedGameCatalog": {
  "TopRatedThreshold": 4.0,    // was 4.5 — aligned to mockup ≥8/10 (4.0 on 5-scale)
  "NewWindowDays": 14          // explicit, was implicit fallback
}
```

`SearchSharedGamesQueryHandler` line 27 already reads via `IConfiguration`.

### 4.3 Component file layout

```
apps/web/src/
├── lib/hooks/use-url-hash-state.ts        [NEW]
├── lib/api/clients/sharedGamesClient.ts   [EXTEND — add searchSharedGames + getTopContributors]
├── lib/api/schemas/shared-games.schemas.ts [EXTEND — add 7 fields + TopContributorSchema]
├── hooks/queries/useSharedGamesSearch.ts  [NEW — React Query wrapper]
├── hooks/queries/useTopContributors.ts    [NEW]
├── components/ui/v2/shared-games/
│   ├── shared-games-filters.tsx           [NEW]
│   ├── shared-games-grid.tsx              [NEW — orchestrator dei 5 stati]
│   ├── meeple-card-game.tsx               [NEW]
│   ├── meeple-card-game-skeleton.tsx      [NEW]
│   ├── contributors-sidebar.tsx           [NEW]
│   └── empty-state.tsx                    [NEW]
└── app/(public)/shared-games/
    ├── page.tsx                            [NEW — server, SSR fetch]
    └── page-client.tsx                     [NEW — client, URL state]
```

---

## 5. Implementation Plan (bottom-up)

| # | Step | Files | Status |
|---|------|-------|--------|
| 1 | Hook `useUrlHashState<T>` | `lib/hooks/use-url-hash-state.ts` + test | ⬜ |
| 2 | Zod schemas + API client | `shared-games.schemas.ts`, `sharedGamesClient.ts` | ⬜ |
| 3 | React Query hooks | `useSharedGamesSearch.ts`, `useTopContributors.ts` | ⬜ |
| 4 | UI components | `v2/shared-games/*.tsx` | ⬜ |
| 5 | Route page | `(public)/shared-games/{page,page-client}.tsx` | ⬜ |
| 6 | Backend config | `appsettings.json` + `appsettings.Development.json` | ⬜ |
| 7 | i18n keys | `it.json` + `en.json` | ⬜ |
| 8 | Tests (unit + visual + axe) | `__tests__/`, `e2e/visual/`, `e2e/accessibility.spec.ts` | ⬜ |

---

## 6. Resolved Tensions / Blind Spots

### 6.1 `TopRatedThreshold` 4.5 vs 4.0

**Tension**: Backend default era 4.5 (mockup ≥8/10 = 4.0 in scala 5). Risolto: aggiorno appsettings.json a 4.0 (DoD checklist).

### 6.2 `ContributorsCount` count semantics

**Blind spot identificato dall'audit backend**: il campo `ContributorsCount` nel DTO conta solo `DISTINCT users con toolkit` — NON l'union {Toolkit + Agent + KB contributors}. Il mockup non specifica.

**Risoluzione**: accetto il count attuale (Toolkits) per Wave A.3b — l'utente vede il count "contributori" che riflette accuratamente i creatori di toolkit. Issue follow-up tracker per estendere a union se UX feedback lo richiede.

### 6.3 `NewWindowDays` not in appsettings

**Blind spot**: backend usa fallback default `14` ma non è dichiarato in `appsettings.json`. Lo aggiungo esplicitamente nello stesso PR per chiarezza ops.

### 6.4 Search debounce 300ms

**Tension**: il mockup non specifica debounce, ma il DoD lo richiede. Risolto: 300ms è il default standard del codebase (verificato in altri input-search component) e bilancia UX (typing fluido) con load (1 fetch ogni 300ms di pausa).

### 6.5 Sidebar `<aside>` semantic

**Decisione SEO/A11y**: il sidebar contributori usa `<aside aria-label="...">` con landmark role implicit. Hidden mobile via `hidden md:block` — tailwind genera `display:none` che è OK per a11y (screen reader skip). Su mobile non c'è drawer (deferred).

### 6.6 Initial fetch on Server vs Client

**Trade-off**: SSR fetch warm sul server costa ~20-100ms ma migliora LCP e SEO. Decisione: SSR fetch con defaults; il client hydrata e — se hash non vuoto — esegue fresh fetch via React Query. `initialData` applicato solo se state == defaults (deep-equal).

---

## 7. Test Matrix

### 7.1 Unit tests (Vitest)

| File | Tests | Target |
|------|-------|--------|
| `use-url-hash-state.test.ts` | ~12 | Hook coverage |
| `shared-games-filters.test.tsx` | ~8 | Behavior |
| `contributors-sidebar.test.tsx` | ~5 | Rendering + fallback |
| `meeple-card-game.test.tsx` | ~6 | Semantic + a11y |
| `page-client.test.tsx` | ~4 | Integration (5 stati) |
| **Total** | **~35** | **85%+ coverage** |

### 7.2 Visual regression (Playwright)

`apps/web/e2e/visual/shared-games.visual.spec.ts` — bootstrap CI mode:
- First run: `--update-snapshots` flag genera baseline (committed con PR)
- Subsequent runs: fail su `maxDiffPixelRatio > 0.01`

12 PNG = 5 stati × 2 viewport + 1 default × 2 viewport (default è già coperto da [stato 1 × 2 viewport]).

**Setup mock**: ogni stato richiede mock `page.context().route()` per `/api/shared-games*` con responses fixture.

### 7.3 A11y (axe-core)

`apps/web/e2e/accessibility.spec.ts` — aggiungo entry:

```typescript
test('shared-games has no a11y violations', async ({ page }) => {
  await page.goto('/shared-games');
  const results = await new AxeBuilder({ page }).analyze();
  expect(results.violations).toHaveLength(0);
});
```

Test per ognuno dei 5 stati via mock route.

---

## 8. Definition of Done

- [ ] Route `/shared-games` v2 implementata 1:1 con mockup (`maxDiffPixelRatio: 0.01`)
- [ ] Server+Client split (SSR initial state per LCP)
- [ ] `useUrlHashState<T>` generico, multi-key sync (`#q=&chips=&genre=&sort=`), SSR-safe
- [ ] Search debounce 300ms (Vitest verifica fake timer)
- [ ] 5 stati coperti (default, loading, empty-search, filtered-empty, api-error)
- [ ] Visual baseline default 1×2 viewport (2 PNG)
- [ ] V2-states baseline 5×2 viewport (10 PNG via CI bootstrap)
- [ ] Behavioral unit tests: hook (~12), filters (~8), sidebar (~5), card a11y (~6), page-client (~4) — totale ~35
- [ ] axe-core 0 violations su tutti e 5 gli stati
- [ ] Bundle delta < +50 KB vs `bundle-size-baseline.json`
- [ ] WCAG AA, MeepleCardGame come `<a href>` (semantic + SEO + keyboard nav)
- [ ] i18n IT/EN complete (~35 keys × 2 = 70 entries)
- [ ] Backend config `TopRatedThreshold: 4.5 → 4.0` + `NewWindowDays: 14` esplicito
- [ ] PR target = `main-dev`

---

## 9. References

- Issue: meepleAi-app/meepleai-monorepo#596
- Parent: #579 (V2 Phase 1 Wave A umbrella)
- Predecessor: #593 / PR #594 (Wave A.3a backend)
- Mockup: `admin-mockups/design_files/sp3-shared-games.jsx`
- Backend DTO: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs`
- Backend endpoints: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogPublicEndpoints.cs`
- Backend handler: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/SearchSharedGamesQueryHandler.cs`
- Frontend pattern reference: `apps/web/src/app/(public)/library/page.tsx` + `_content.tsx`
- Visual test pattern: `apps/web/e2e/visual/admin-dashboard.visual.spec.ts`
- A11y test pattern: `apps/web/e2e/accessibility.spec.ts`
- Bundle baseline: `apps/web/bundle-size-baseline.json`
