# Stage 3 cluster — `hub/<entity>` FE (3 routes)

| Field | Value |
|---|---|
| Status | accepted |
| Date | 2026-05-14 |
| Author | spec-panel session (Wiegers, Adzic, Cockburn, Fowler, Nygard, Crispin) |
| Parent | issue #1026 (Stage 3 cluster fixes) · #1097 (Pre-Stage-3 mockups) |
| Umbrella | issue #1023 (De-versioning) |
| Mockups SoT | `admin-mockups/design_files/sp4-hub-{games,agents,toolkits}.{html,jsx}` (PR #1151) |
| Pattern reference | discover FE (#1147 → PR #1160), dashboard FE (#1164 → PR #1165) |
| Auth model (D1) | `/hub/games` public · `/hub/agents` + `/hub/toolkits` authenticated |

## 1. Problem

Mockups SoT for 3 hub catalog routes were merged in PR #1151 but no FE work followed. Route groups `(public)` and `(authenticated)` exist; `/hub/*` directories do not. This cluster ships the 3 routes per parent spec §6 hub composability matrix.

## 2. AC0 — Backend hook verification

| Route | Hook | Status |
|---|---|---|
| `/hub/games` | `useSharedGames` (paginated) | ✅ live |
| `/hub/agents` | `useDiscoverPopularAgents({ limit: 50 })` | ⚠️ top-N (full pagination deferred per Path C) |
| `/hub/toolkits` | `useDiscoverRecommendedToolkits({ limit: 50 })` | ⚠️ top-N (full pagination deferred per Path C) |

**Verdict**: 1 paginated + 2 top-50 catalogs. Acceptable for Stage 3 cluster ship. Full pagination tracked as future enhancement when backend list endpoints harden.

## 3. Decisions taken

| Question | Decision | Rationale |
|---|---|---|
| Single shared view vs 3 separate orchestrators | **Shared `HubCatalogView<TItem>`** parameterized per entity + 3 thin `page.tsx` | 80% structural overlap; DRY; consistent UX. |
| StickyAccessCta scope | **Only `/hub/games`** (public visitor) | D1 auth model. authenticated users don't need login CTA. |
| Install action | `/hub/games`: redirect-to-login CTA. `/hub/agents`+`/hub/toolkits`: hover-revealed install button (telemetry-only stub, no actual install API in v1) | Mockup pattern. Real install pending Phase 5. |
| Filter UI | 4 segmented pills (`Tutti / Featured / Nuovi / Top`) client-side filter over fetched list | Match mockup; no backend filter param. |
| Search | URL `?q=` state, client-side title/author fuzzy match | Same pattern as discover. |
| i18n | New `pages.hub.{games,agents,toolkits}` namespace | First i18n for hub. |
| Telemetry | 5 PostHog events (see AC6) | Engagement signal per route. |

## 4. Goals & Non-goals

### Goals

- G1 — Create 3 routes: `/hub/games`, `/hub/agents`, `/hub/toolkits`
- G2 — Shared `HubCatalogView` component handling hero + search + filters + card grid + states
- G3 — 3 card variants: `HubGameCard`, `HubAgentCard`, `HubToolkitCard`
- G4 — `StickyAccessCta` component, used only by `/hub/games`
- G5 — URL state `?q=&filter=` SSR-resolvable
- G6 — i18n: `pages.hub.{games,agents,toolkits}.*` + `pages.hub.common.*` shared
- G7 — Telemetry: 5 PostHog events
- G8 — Auth gating via route groups: `(public)` for games, `(authenticated)` for agents/toolkits

### Non-goals

- NG1 — Backend pagination for agents/toolkits (top-50 cap acceptable)
- NG2 — Real install API integration (telemetry stub only)
- NG3 — Test matrix (deferred per pattern from #1151/#1153/#1160/#1163/#1165)
- NG4 — Hub detail pages `/hub/<entity>/[id]` (separate cluster, future)
- NG5 — Notification bell or breadcrumbs (not in mockups)

## 5. Composition mapping

| Mockup region | Component |
|---|---|
| Top nav | Existing `PublicLayout` (games) or `UserShell` (authenticated) |
| Hero gradient + title + subtitle + KPI bar | `HubCatalogView` hero slot |
| Search input | Inline within hero |
| Filter pills (4 segmented) | Inline within hero, below search |
| Card grid (responsive 1/2/3/4 col) | `HubCatalogView` body, children renderer per variant |
| StickyAccessCta (games only) | `<StickyAccessCta />` floating bottom |
| Empty / loading / filtered-empty / error states | `HubCatalogView` shells |

## 6. Acceptance criteria

- **AC1** — 3 routes created: `(public)/hub/games/page.tsx`, `(authenticated)/hub/agents/page.tsx`, `(authenticated)/hub/toolkits/page.tsx`. Each wraps `HubCatalogView` with route-specific props.
- **AC2** — `HubCatalogView<TItem>` renders: hero (title+subtitle+KPI) → search → filter pills → card grid. 4 FSM shells: loading / error / empty / filtered-empty.
- **AC3** — 3 card variants implemented per mockup:
  - `HubGameCard`: cover gradient + emoji, badge top-left, entity chip top-right, install count bottom-right, title + rating + publisher
  - `HubAgentCard`: same shell, model + invocations meta, hover install button
  - `HubToolkitCard`: same shell, version + tools count + uses count, hover install button
- **AC4** — `StickyAccessCta` component with `/login?redirect=/hub/games` link; rendered only by games page.
- **AC5** — URL state: `?q=<search>&filter=<all|featured|new|top>`. SSR-resolvable. 300ms debounce on search.
- **AC6** — Telemetry (5 events):
  - `hub_card_clicked` `{ entity, itemId }`
  - `hub_search_committed` `{ entity, q }`
  - `hub_filter_changed` `{ entity, from, to }`
  - `hub_install_clicked` `{ entity, itemId }` (agents/toolkits only)
  - `hub_signin_clicked` `{ from: 'hub/games' }` (games StickyAccessCta only)
- **AC7** — i18n: all user-facing strings via `useTranslation`. Keys under `pages.hub.{games,agents,toolkits}.*` + shared `pages.hub.common.*`.
- **AC8** — `pnpm typecheck && pnpm lint && pnpm build` green.
- **AC9** — Smoke test per route: page renders without crashing, hero present, card grid present.
- **AC10 (deferred)** — Full test matrix (unit + integration + E2E + visual diff) deferred to follow-up PR.

## 7. Examples (Gherkin)

```gherkin
Scenario: Public visitor opens /hub/games
  Given user is not authenticated
  When user navigates to /hub/games
  Then page renders without auth redirect
  And hero shows "Catalogo giochi della community"
  And StickyAccessCta is visible bottom of viewport
  And clicking StickyAccessCta redirects to /login?redirect=/hub/games

Scenario: Authenticated user opens /hub/agents
  Given user is authenticated
  When user navigates to /hub/agents
  Then page renders inside (authenticated) layout
  And hero shows "Agenti community"
  And NO StickyAccessCta is rendered
  And hovering an agent card reveals "+ Installa agente" button

Scenario: Filter pill change
  Given user on /hub/toolkits with filter=all
  When user clicks "Featured" pill
  Then URL becomes /hub/toolkits?filter=featured
  And cards displayed are filtered to featured=true
  And PostHog event "hub_filter_changed" fires with { entity: 'toolkits', from: 'all', to: 'featured' }

Scenario: Search with debounce
  Given user on /hub/games with empty search
  When user types "catan" in the search input
  And waits 300ms idle
  Then URL becomes /hub/games?q=catan
  And cards filtered to titles matching "catan"
  And PostHog event "hub_search_committed" fires with { entity: 'games', q: 'catan' }

Scenario: Card click telemetry
  Given user on /hub/games
  When user clicks a card titled "Azul"
  Then PostHog event "hub_card_clicked" fires with { entity: 'games', itemId: '<uuid>' }
  And browser navigates to /games/<uuid> (or appropriate detail route)
```

## 8. Sub-issue

`[WS-Stage3] hub/<entity> FE — 3 routes (Stage 3 cluster, mockups #1151)`

## 9. Rollback

Revert PR → 3 routes return 404. `HubCatalogView` + card variants + StickyAccessCta + i18n keys removed.

## 10. References

- Mockups SoT: `admin-mockups/design_files/sp4-hub-{games,agents,toolkits}.jsx`
- Existing layouts: `apps/web/src/app/(public)/layout.tsx`, `apps/web/src/app/(authenticated)/layout.tsx`
- Pattern precedents: discover FE PR #1160, dashboard FE PR #1165, toolkit-detail FE PR #1163
- Parent roadmap: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` §6 Hub
