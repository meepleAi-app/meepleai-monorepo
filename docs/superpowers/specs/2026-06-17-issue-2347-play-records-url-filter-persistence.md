# Spec — Issue #2347 Play Records hub URL filter persistence (minimal gap-fill)

**Status**: APPROVED · **Date**: 2026-06-17 · **Author**: DegrassiAaron + Claude Code · **Issue**: [#2347](https://github.com/meepleAi-app/meepleai-monorepo/issues/2347)

---

## Context

Sub-spec di [US-INT-2](https://github.com/meepleAi-app/meepleai-monorepo/issues/2346) (Play Records Lifecycle). Completa `/play-records` hub list + filter sul mockup `sp4-play-records-index.html`.

**Discovery post `/sc:spec-panel` 2026-06-15**: mockup + page + componenti BE/FE già shipped end-to-end. Il body originale dell'issue chiedeva BE cursor-based pagination + outcome filter + Storybook stories — sopravvalutato vs gap reali.

**Effort revised**: ~0.5-0.7gg single FTE (vs S ~3gg originale).

## DEC user-locked (brainstorming 2026-06-17)

| # | Decisione | Razionale |
|---|---|---|
| **DEC-1** | Minimal gap-fill scope | URL filter persistence + 5 stati canonici + E2E + axe AA + designer self-waiver. Mantiene status filter esistente. No BE refactor. No outcome chip. |
| **DEC-2** | URL shape: status only | `?status=InProgress\|Completed\|Planned`; default `all` = no param. Search resta local state (no history pollution). View toggle resta Zustand persist (UX preference). |
| **DEC-5** | Designer self-waiver P250 | User è designer single-person team. Applica pattern P250 automatico: `designer_approved_by: "DegrassiAaron"` + `designer_approved_on: 2026-06-17` su fidelity.json. |
| **DEC-6** | 5 stati canonici minimal verification | Verifica esistenti (default/empty/loading/error; sse N/A). Storybook stories solo se DS-17 esistenti per `/play-records`. E2E Playwright per default + filter empty. axe AA per default + filter empty. |

## Assets già shipped (verificati)

### Mockup
- `admin-mockups/design_files/sp4-play-records-index.{html,jsx,fidelity.json}` (Phase B audit `design_intent: "current"`)

### FE
- `apps/web/src/app/(authenticated)/play-records/page.tsx` — Suspense + tab=stats branching
- `apps/web/src/components/play-records/PlayHistory.tsx` (232 LOC) — Zustand `usePlayRecordsStore` consumer, gestisce 5 stati `loading/error/showFirstRunEmpty/showFilterEmpty/records`
- `apps/web/src/components/play-records/index/RecordFilters.tsx` (146 LOC) — sticky bar con `statusFilter` prop (4 chip + search + 4 dropdown stub + view toggle list/grid)
- `apps/web/src/components/play-records/index/RecordCardGrid.tsx` + `RecordCardList.tsx` — render dispatch

### BE (no changes required)
- `apps/api/src/Api/Routing/PlayRecordEndpoints.cs` — `GET /play-records/history?page=&pageSize=&gameId=` page-based pagination via `GetUserPlayHistoryQuery`
- Filter SQL: solo `gameId`. Status filter applicato lato FE post-fetch.

## Architettura (minimal gap-fill)

### Cambiamento comportamentale (1 punto)

`PlayHistory.tsx` aggiunge round-trip sync URL ↔ Zustand store per filtro status:

```typescript
import { useRouter, useSearchParams } from 'next/navigation';

// Sync URL → store on mount + URL change
useEffect(() => {
  const urlStatus = searchParams.get('status') ?? 'all';
  if (urlStatus !== filters.status) {
    setFilter('status', urlStatus as PlayRecordStatus | 'all');
  }
}, [searchParams, filters.status, setFilter]);

// Sync store → URL on filter chip click
useEffect(() => {
  const params = new URLSearchParams(searchParams.toString());
  if (filters.status === 'all') {
    params.delete('status');
  } else {
    params.set('status', filters.status);
  }
  const queryString = params.toString();
  router.replace(queryString ? `?${queryString}` : '/play-records', { scroll: false });
}, [filters.status, router, searchParams]);
```

### URL state contract

| Path | Significato |
|---|---|
| `/play-records` | Default — `status=all`, lista completa |
| `/play-records?status=InProgress` | Filtro su partite in corso |
| `/play-records?status=Completed` | Filtro su partite completate |
| `/play-records?status=Planned` | Filtro su partite pianificate |
| `/play-records?tab=stats` | Tab stats (back-compat con route consolidation #5039) |

Combinazioni miste: `?status=Completed&tab=stats` → `tab` ha precedenza (StatisticsView mounted prima del filter check).

### Validazione input URL

Status invalido (es. `?status=foo`) → fallback `all` silenzioso (no error). Discriminato via `PlayRecordStatus` enum check + `'all'` sentinel.

```typescript
const VALID_STATUSES = new Set<string>(['all', 'InProgress', 'Completed', 'Planned']);
const urlStatus = VALID_STATUSES.has(searchParams.get('status') ?? '')
  ? (searchParams.get('status') as PlayRecordStatus | 'all')
  : 'all';
```

### Components ToBe

| File | Change |
|---|---|
| `apps/web/src/components/play-records/PlayHistory.tsx` | Aggiungi `useSearchParams` + `useRouter` import. 2 `useEffect` round-trip. Helper `parseStatusParam(searchParams)` con validation. |
| `apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx` | Aggiungi 4 unit test: (1) mount con `?status=InProgress` setta store; (2) chip click setta URL; (3) `?status=foo` fallback `all`; (4) clear filter → delete param. |
| `apps/web/e2e/play-records-hub.spec.ts` | NUOVO file — 4 E2E scenario: (1) default mount; (2) chip click URL update; (3) deep-link `?status=Completed`; (4) filter empty state retry. |
| `admin-mockups/design_files/sp4-play-records-index.fidelity.json` | Aggiungi `designer_approved_by: "DegrassiAaron"` + `designer_approved_on: "2026-06-17"` (DEC-5 P250 self-waiver). |

## Testing

### Unit (Vitest)
- 4 nuovi test in `PlayHistory.test.tsx`
- Mock `next/navigation` (useSearchParams, useRouter) — pattern già consolidato in altri test del repo
- Coverage gate: minimum 85% (default project)

### E2E (Playwright)
4 scenarios in nuovo `apps/web/e2e/play-records-hub.spec.ts`:

```gherkin
Scenario 1 — Default mount:
  Given utente autenticato senza play records
  When apre /play-records
  Then chip "Tutte" è aria-pressed=true
  And URL è "/play-records" (no params)
  And empty state mostra "Aggiungi il primo"

Scenario 2 — Filter chip click → URL update:
  Given utente apre /play-records
  When clicca chip "Completate"
  Then chip "Completate" è aria-pressed=true
  And URL diventa "/play-records?status=Completed"

Scenario 3 — Deep-link with ?status=:
  When utente apre /play-records?status=InProgress
  Then chip "In corso" è aria-pressed=true al primo render
  And lista mostra solo record con status InProgress

Scenario 4 — Empty filter state retry:
  Given utente ha 0 record con status=Planned
  When apre /play-records?status=Planned
  Then mostra showFilterEmpty con "Reset filters" CTA
  And click CTA → URL torna a /play-records
```

### axe AA
- Smoke test su default state + filter empty state via `@axe-core/playwright` in stesso E2E file
- Gate: 0 violations contrast/aria/label

### Storybook (DEC-A5)
Verifica esistenza stories `play-records-index.stories.tsx` in `apps/web/.storybook/stories/`. Se inesistenti, **SKIP** (Storybook coverage è DS-17 scope separato). Documentare nello spec.

## Definition of Done

- [ ] PlayHistory.tsx aggiunge round-trip URL ↔ Zustand per `status` filter
- [ ] Validation URL param (fallback silenzioso a `all` per status invalido)
- [ ] 4 unit test passano in `PlayHistory.test.tsx`
- [ ] 4 E2E scenari passano in `play-records-hub.spec.ts`
- [ ] axe AA pass su default + filter empty
- [ ] fidelity.json: `designer_approved_by` + `designer_approved_on` populated
- [ ] Lint + typecheck clean
- [ ] CI green
- [ ] PR aperta su `main-dev`, body referenzia #2347 (closes) + #2346 (Tier 2 unblocking)

## Out of scope (future issue)

- Outcome filter chip (Vinti/Persi/Last-week) → future enhancement
- BE cursor-based pagination → future BE refactor
- Search URL persistence con debounce → future enhancement
- Storybook stories migration (DS-17 scope separato)
- Designer formal review (DEC-5 self-waiver applicato)

## References

- Parent: [#2346](https://github.com/meepleAi-app/meepleai-monorepo/issues/2346) US-INT-2 umbrella
- Umbrella: [#2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) Mockup-to-US Coverage execution
- Spec doc parent: `docs/for-developers/specs/2026-06-14-mockup-us-coverage-map.md` §4b US-GAP-PR-01
- Mockup canonical: `admin-mockups/design_files/sp4-play-records-index.{html,jsx,fidelity.json}`
- Pattern P250 (designer self-waiver): memory note `epic-2096-closure-shipped.md`
- Pattern P145 (admin-squash merge): memory note `epic-2374-g1-3col-layout-shipped.md`

---

🤖 Generated with [Claude Code](https://claude.com/claude-code) — brainstorming + spec session 2026-06-17 (post `/sc:spec-panel` discovery 2026-06-15)
