# Stage 3 cluster — `/discover` (mockup-faithful re-implementation)

| Field | Value |
|---|---|
| Status | accepted |
| Date | 2026-05-14 |
| Author | spec-panel session (Wiegers, Adzic, Fowler, Crispin, Nygard) |
| Parent | issue #1026 (Stage 3 cluster fixes) |
| Umbrella | issue #1023 (De-versioning) |
| Pattern reference | issue #1113 (player-detail, closed), issue #1145 (toolkit-detail FE), spec `2026-05-14-stage3-toolkit-detail.md` |
| Mockup SoT | `admin-mockups/design_files/sp4-discover.jsx` (+ `.html`) |
| Parent spec | `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` §3 Stage 3 |
| Related | issue #687 (closed, v2 migration superseded by #1026), issue #728 (backend Phase 0.5 — 4 remaining endpoints) |

## 1. Problem

`/discover` is a **catalog/hub list page**, not a detail page. The 6 Wave-3 component stubs at `apps/web/src/components/features/discover/` (TODO `return null`) and the current placeholder page (`"Scopri in arrivo."` inside `HubLayout`) must converge to the mockup `sp4-discover.jsx` — a Netflix-style 7-row catalog with hero, search, filter pills, scroll regions, and footer CTA.

Stage 1 audit (#1024) flagged all 6 entries as `missing`. The v2 migration issue #687 was closed as superseded by #1026 — this cluster IS the de facto v2 migration.

Unlike `player-detail` and `toolkit-detail`, `/discover` does **not** consume `DetailPageLayout`. It is not in parent spec §4 composability matrix. The existing `HubLayout` primitive is the correct wrapper.

## 2. AC0 — Backend endpoint verification (precondition, executed 2026-05-14)

Endpoints powering the 7 mockup rows:

| # | Row | Endpoint | Status |
|---|---|---|---|
| 1 | Trending games | `GET /api/v1/catalog/trending` | ✅ live (`useCatalogTrending`) |
| 2 | Giochi nuovi | `GET /api/v1/catalog/games/new` | ✅ live (`useDiscoverNewGames`) |
| 3 | Agenti più installati | `GET /api/v1/agents/popular` | ✅ live (`useDiscoverPopularAgents`) |
| 4 | Toolkit consigliati | `GET /api/v1/toolkits/recommended` | ❌ pending #728 |
| 5 | KB recenti | `GET /api/v1/kb-docs/recent` | ✅ live (`useDiscoverRecentKbDocs`) |
| 6 | Top contributor | `GET /api/v1/community/top-contributors` | ❌ pending #728 |
| 7 | Eventi vicini | `GET /api/v1/events/nearby` | ❌ pending #728 |
| — | Search global | `GET /api/v1/catalog/search?q=` | ⚠️ verify during impl |

**Verdict**: 4/7 rows enabled now (1, 2, 3, 5). 3 rows + global search deferred to #728. Cluster proceeds via **Path C** consensus: enabled rows fully implemented + disabled-shell for pending rows with `discover_disabled_row_visible` impression telemetry.

> Correction vs early-round count (3 enabled): re-verification confirms `useDiscoverRecentKbDocs` is also live, so row 5 (KB recenti) is enabled too. Final count: 4 enabled + 3 disabled-shell.

## 3. Decisions taken during spec-panel rounds

| Question | Decision | Rationale |
|---|---|---|
| Wrapper primitive | Reuse existing `HubLayout`; compose hero/filters/rows/footer in `children` slot | YAGNI — no new primitive. `HubLayout` is already mounted in the placeholder page; semantics fit (search + content). No findings to #1126. |
| 3 pending rows | Implement as `<HorizontalRow disabled tooltip="Disponibile in Phase 0.5">` with 3-card skeleton placeholder + impression telemetry | Same Path C strategy as toolkit-detail; preserves mockup-faithful 7-row shape; provides demand signal for #728 prioritization. |
| Search/filter state ownership | `?q=<search>&entity=<filter>` URL query params, SSR-resolvable | Shareable URLs; SSR-first paint; consistent with toolkit-detail tab-state convention. |
| Search debounce | 300ms client-side, push to URL on commit (Enter or 300ms idle) | Standard pattern; prevents URL spam. |
| Filter pill behavior | Client-side row visibility toggle (`display: none` on non-matching rows) | Mockup shows full hide-others on filter click, not in-row filtering. Zero backend cost. |
| Global search box | Verify `GET /api/v1/catalog/search?q=` exists; if absent → disabled-shell + telemetry `discover_search_attempted_unavailable` | Avoids client-side fuzzy search across heterogeneous entities (false positives, unmaintainable). |
| Scroll behavior | `role="region"` + `tabIndex={0}` for keyboard scroll; left/right arrows are visual-only hover affordance (NOT WAI-ARIA carousel) | Mockup is not auto-advancing carousel; simpler a11y semantic is correct. |
| `prefers-reduced-motion` | Disable scroll-snap smoothness; keep snap points (no JS animation) | Standard a11y. |

## 4. Goals & Non-goals

### Goals

- G1 — Replace `/discover` placeholder with full mockup-faithful 7-row catalog inside `HubLayout`
- G2 — Implement 6 Wave-3 components: `DiscoverHero`, `DiscoverSearchBox`, `EntityFilterPillBar`, `HorizontalRow`, `RowScroller`, `FooterCTA`
- G3 — Wire 4 enabled rows to existing hooks (`useCatalogTrending`, `useDiscoverNewGames`, `useDiscoverPopularAgents`, `useDiscoverRecentKbDocs`)
- G4 — Render 3 pending rows as disabled-shell with skeleton + impression telemetry
- G5 — URL state (`?q=`, `?entity=`) SSR-resolvable; client debounce 300ms; filter pill toggles row visibility
- G6 — Scroll region a11y (keyboard scroll, focus visible, reduced-motion); arrows hover-only visual
- G7 — Footer CTA gradient block per mockup
- G8 — i18n: keys under `pages.discover.*` in `messages/it.json` + `messages/en.json`
- G9 — PostHog telemetry: 4 events (see AC18)

### Non-goals

- NG1 — Backend Phase 0.5 endpoints (#728 — toolkits recommended, top contributors, events nearby, global search)
- NG2 — Personalization / recommendation engine (rows are static catalog queries; no per-user weighting)
- NG3 — Pagination / infinite scroll (mockup shows finite top-N per row; "View all" CTA per row out of scope here)
- NG4 — New shared primitive (no `CatalogPageLayout`; use `HubLayout`)
- NG5 — Change `HubLayout.tsx` itself (any change → separate PR)
- NG6 — Adopt `DetailPageLayout` (this is a list page, not a detail page)

## 5. Composition mapping

| Mockup region | Wrapper / component |
|---|---|
| Top nav + search | `HubLayout` props: `searchPlaceholder`, `onSearch` |
| Hero gradient block | `<DiscoverHero>` inside `children` slot |
| Entity filter pills (7) | `<EntityFilterPillBar value={entityFilter} onChange={...} />` |
| 7 horizontal rows | `<HorizontalRow variant={...} items={...} state={enabled|disabled}>` × 7 |
| Inner scroll region per row | `<RowScroller>` (used inside `HorizontalRow`) |
| Footer gradient CTA | `<FooterCTA>` |

## 6. Acceptance criteria

**Precondition** (verify during impl, not blocking PR open):
- **AC0** — Confirm 4 endpoints live and DTOs match Zod schemas in `apps/web/src/lib/api/schemas/discover.schemas.ts`. Confirm global search endpoint status (live → wire; absent → disabled-shell).

**FE cluster AC**:

- **AC1** — `/discover/page.tsx` renders `HubLayout` wrapping (top → bottom): `DiscoverHero` → `EntityFilterPillBar` → 7×`HorizontalRow` → `FooterCTA`
- **AC2** — 4 enabled rows wired to existing hooks; loading skeleton (3 placeholder cards), error state (inline retry), empty state (mockup placeholder)
- **AC3** — 3 pending rows render `<HorizontalRow disabled tooltip="Disponibile in Phase 0.5">` with 3-card skeleton; `discover_disabled_row_visible` event fires once per row when row enters viewport (IntersectionObserver)
- **AC4** — Search box: `q` query param SSR-resolvable; 300ms debounce; commit on Enter or idle; if backend search endpoint absent → input is `aria-disabled="true"` + tooltip + `discover_search_attempted_unavailable` telemetry on focus
- **AC5** — Entity filter pills (`Tutti|Giochi|Agenti|Toolkit|KB|Persone|Eventi`): `entity` query param SSR-resolvable; client-side row visibility toggle (non-matching rows `aria-hidden="true"` + `display: none`); `Tutti` shows all rows
- **AC6** — Six Wave-3 components implemented mockup-faithful:
  - `DiscoverHero`: 3-color gradient (event+toolkit+agent), h1, search box slot, filter pills slot
  - `DiscoverSearchBox`: controlled input, debounced commit, disabled-shell variant
  - `EntityFilterPillBar`: 7 pills with `aria-pressed`, keyboard nav (ArrowLeft/Right)
  - `HorizontalRow`: variant `featured|compact|grid|list-row`, `state="enabled|disabled"`, `items` array, loading/error/empty states
  - `RowScroller`: scroll region (`role="region"`), `tabIndex={0}`, keyboard arrow scroll, hover arrows visual-only
  - `FooterCTA`: gradient toolkit→player block with title + 2 CTA buttons
- **AC7** — Playwright visual diff vs `sp4-discover.jsx` ≤ 2% on:
  - Full page @ 375×667 (mobile) — 1 screenshot
  - Full page @ 1280×800 (desktop) — 1 screenshot
  - Hero only @ 1280×800 — 1 screenshot
  - Single row scroll mid-state (after keyboard arrow right) — 1 screenshot
  - Filter applied state (entity="agents") @ 1280×800 — 1 screenshot
- **AC8** — Test matrix:

| Layer | Tests | Examples |
|---|---|---|
| Unit (Vitest) | each component | render baseline + disabled variant; filter pill `aria-pressed` toggle; search debounce; row visibility filter |
| Unit | hooks wiring | mock MSW responses → assert row state transitions (loading→success/error/empty) |
| Integration | URL state ↔ UI | mount with `searchParams={q:'catan',entity:'games'}` → search box shows "catan", only "Giochi" row visible |
| E2E (Playwright) | search debounce | type "catan" → wait 300ms → URL contains `?q=catan` |
| E2E | filter pill toggle | click "Agenti" → only agents row visible; click "Tutti" → all rows visible |
| E2E | disabled row impression | scroll to disabled row → `discover_disabled_row_visible` event fired (assert via window event spy) |
| E2E | keyboard scroll | focus `RowScroller` → ArrowRight → scroll position changes |
| Visual | mockup diff | 5 screenshots (AC7) |
| A11y (Playwright+axe) | landmarks, region, pill bar | 0 violations; `aria-pressed` consistent; scroll region `tabIndex={0}` |

- **AC9** — `pnpm typecheck && pnpm lint && pnpm test --run` green; no regression in other `HubLayout` consumers
- **AC10** — Test coverage ≥ 85% on 6 components + page orchestrator
- **AC11** — Parent spec §4 NOT modified (no DetailPageLayout entry for `/discover`); audit `2026-05-11-mockup-conformity.md` discover rows flipped from `missing` to `done`
- **AC12** — Soft perf gate: `pnpm size-limit` output for `/discover` route + Lighthouse mobile in PR description; regression > 10% vs baseline requires `[perf-justified]`
- **AC13** — No modification to `HubLayout.tsx`. Any change → separate PR
- **AC14** — `prefers-reduced-motion` respected: scroll-snap smoothness off, snap points kept
- **AC15** — Keyboard a11y: tab order Hero → SearchBox → FilterPills → Row1...Row7 → FooterCTA; visible focus rings
- **AC16** — i18n: all user-facing strings via `useTranslations`, keys under `pages.discover.*`
- **AC17** — Loading skeleton (initial SSR fallback): hero placeholder + 7-row skeleton shape, zero CLS
- **AC18** — PostHog events:
  - `discover_search_committed` props: `q`, `entityFilter`
  - `discover_filter_pill_clicked` props: `entity`, `previousEntity`
  - `discover_disabled_row_visible` props: `row` (`toolkits|contributors|events|search`), fired once per page-view per row
  - `discover_card_clicked` props: `row`, `entityId`, `entityType`

## 7. Examples (Gherkin)

```gherkin
Scenario: SSR-resolved search query
  Given URL /discover?q=catan&entity=games
  When la pagina renderizza
  Then SearchBox value è "catan"
  And pill "Giochi" è aria-pressed="true"
  And solo row "Giochi nuovi" è visible (le altre aria-hidden="true")

Scenario: Filter pill toggle (client-side)
  Given utente su /discover (entity="all")
  When clicca pill "Agenti"
  Then URL diventa /discover?entity=agents
  And solo row "Agenti più installati" è visible
  And event PostHog "discover_filter_pill_clicked" fired

Scenario: Disabled row impression
  Given utente su /discover
  When scrolla finché row "Toolkit consigliati" entra in viewport
  Then row mostra 3 card skeleton + tooltip "Disponibile in Phase 0.5"
  And event PostHog "discover_disabled_row_visible" fired with row="toolkits"
  And event NON viene rifirato durante la stessa page-view

Scenario: Search debounce
  Given utente su /discover
  When digita "ca" → "cat" → "catan" in 200ms
  Then URL non cambia immediatamente
  When passa 300ms idle dopo "catan"
  Then URL diventa /discover?q=catan
  And event PostHog "discover_search_committed" fired

Scenario: Keyboard scroll row
  Given focus su RowScroller di "Giochi nuovi"
  When premi ArrowRight 3 volte
  Then scrollLeft del row aumenta di 3 card-width (con prefers-reduced-motion: no smooth)

Scenario: Search endpoint unavailable
  Given backend /api/v1/catalog/search NON disponibile
  When utente apre /discover
  Then SearchBox è aria-disabled="true"
  And tooltip "Ricerca disponibile in Phase 0.5" visibile su hover
  And focus sul box → event "discover_search_attempted_unavailable"
```

## 8. Sub-issue (planned)

**FE-only sub-issue** — `[WS-Stage3] /discover FE — implement 7-row catalog inside HubLayout (Stage 3 cluster)`
- Scope: this spec
- **Blocked-by**: nothing (no BE work required; uses existing endpoints + disabled-shell for pending)
- **Soft-coordinated-with**: #728 (when remaining endpoints land, follow-up FE PR replaces disabled-shells)
- Branch: `feature/issue-{FE}-stage3-discover-fe` from `main-dev`

## 9. Rollback

Revert FE PR — `/discover` returns to placeholder `"Scopri in arrivo."`. No downstream consumers affected (existing hooks remain).

## 10. References

- Mockup SoT: `admin-mockups/design_files/sp4-discover.jsx`
- Wrapper: `apps/web/src/components/ui/hub-layout/HubLayout.tsx`
- Wave 3 stubs: `apps/web/src/components/features/discover/`
- Hooks: `apps/web/src/hooks/queries/useDiscover*`, `useCatalogTrending`
- Zod schemas: `apps/web/src/lib/api/schemas/discover.schemas.ts`
- Parent roadmap: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md`
- Sister cluster spec: `docs/superpowers/specs/2026-05-14-stage3-toolkit-detail.md`
- Closed v2 migration: issue #687
- Pending backend Phase 0.5: issue #728
- Pattern precedent: issue #1113 (player-detail, closed)
