# Stage 3 cluster — `/dashboard` (REFACTOR-FORWARD)

| Field | Value |
|---|---|
| Status | accepted |
| Date | 2026-05-14 |
| Author | spec-panel session (Wiegers, Adzic, Cockburn, Fowler, Crispin, Nygard) |
| Parent | issue #1026 (Stage 3 cluster fixes) · #1097 (Pre-Stage-3 mockups) |
| Umbrella | issue #1023 (De-versioning) |
| Mockup SoT | `admin-mockups/design_files/sp4-dashboard.{html,jsx}` (PR #1153) |
| Pattern reference | `2026-05-14-stage3-toolkit-detail.md`, `2026-05-14-stage3-discover.md` |
| Existing implementation | `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` (PR #309, 749 LOC) |

## 1. Problem

`/dashboard` is currently a 749-LOC monolithic `DashboardClient` (PR #309 — Gaming Hub) with 4 entity zones (Games / Sessions / Agents / Toolkit). The Pre-Stage-3 mockup `sp4-dashboard.jsx` (PR #1153) defines a forward-design with **5 entity sections** (Games / Players / Agents / Sessions / Events), greeting hero, and 4-KPI grid.

Spec-panel decision D2 (2026-05-14): **REFACTOR-FORWARD**, not conformity micro-fix. The mockup is prescriptive; PR #309 is replaced.

## 2. AC0 — Hook verification (executed 2026-05-14)

| Section | Hook | Status |
|---|---|---|
| Games | `useGames` (`@/hooks/queries/useGames`) | ✅ live |
| Players | **derived from `useActiveSessions`** (unique players aggregation) | ⚠️ no dedicated list hook — Path C |
| Agents | `useAgents` (`@/hooks/queries/useAgents`) | ✅ live |
| Sessions | `useActiveSessions` (`@/hooks/queries/useActiveSessions`) | ✅ live |
| Events | `useUpcomingGameNights` (`@/hooks/queries/useGameNights`) | ✅ live |
| KPIs | `useLibraryStats` (`@/hooks/queries/useLibrary`) | ✅ live |

**Verdict**: 5/6 enabled fully. Players section derives unique players from `useActiveSessions.data.sessions[].players[]` (deduplicate by name); when no sessions exist or no players surface, empty-state with "Invita amici" CTA.

## 3. Decisions taken

| Question | Decision | Rationale |
|---|---|---|
| Replacement strategy | Hard replace: new `DashboardClient.tsx` written ex-novo; old 749 LOC deleted in same PR | REFACTOR-FORWARD per D2. No feature-flag rollout (low blast radius, dashboard is leaf route). |
| Layout primitive | No `HubLayout` / `DetailPageLayout`. Plain semantic `<main>` + grid composition | Dashboard is hub-overview, not detail/catalog. Adopting an inappropriate primitive forces wrong slots. |
| Players hook | Derive from `useActiveSessions` instead of creating a new list endpoint | Backend list endpoint is out of scope; derive is functional and fail-safe (empty when no sessions). |
| Live session indicator | Sessions section header shows pulse-animated badge when any session has `state === 'live'` | Mockup pattern; preserves PR #309 feature (was `HeroLiveSession` — now merged into Sessions section). |
| Features dropped from #309 | Chat recent cards (→ `/chat` route), Friends row standalone (→ merged into Players section), 4×Toolkit static grid (→ remove; toolkits are catalog-discoverable via `/library` + `/hub`) | Per D2 mockup decision; mockup is the contract. |
| KPI strip | 4 KPIs (games / sessions / hoursPlayed / winRate). hoursPlayed + winRate derived from `useLibraryStats` aggregates if available, else fallback "—" | Preserves PR #309 KPI feature. |
| i18n | New `pages.dashboard.*` namespace (`it.json` + `en.json`); all greetings, section titles, empty CTAs translated | First i18n adoption for dashboard surface. |
| Telemetry | 3 PostHog events: `dashboard_section_clicked`, `dashboard_view_all_clicked`, `dashboard_empty_cta_clicked` | Engagement signal per section. |

## 4. Goals & Non-goals

### Goals

- G1 — Replace `DashboardClient.tsx` (749 LOC, PR #309) with forward-design per `sp4-dashboard.jsx`
- G2 — Hero: time-of-day greeting + 4-KPI grid (2×2 mobile / 4×1 desktop)
- G3 — 5 entity sections (Games / Players / Agents / Sessions / Events) with populated + empty states
- G4 — Layout: 1-column mobile, 2×2 desktop grid for sections 1-4, full-width row for Events
- G5 — Wire 5 hooks (4 native + 1 derived: Players from sessions)
- G6 — Live session badge in Sessions section header when any session has `state==='live'`
- G7 — i18n: `pages.dashboard.*` keys in it+en
- G8 — Telemetry: 3 PostHog events
- G9 — Delete legacy `DashboardClient.tsx` PR #309 in same PR
- G10 — Preserve `RequireRole` wrapper + metadata + `dynamic = 'force-dynamic'` in `page.tsx`

### Non-goals

- NG1 — Backend Players list endpoint (out of scope; derive from sessions)
- NG2 — Hours-played / win-rate backend exact computation if `useLibraryStats` doesn't expose them (fallback "—")
- NG3 — Notification bell in hero (not in mockup)
- NG4 — Chat recent cards (moved to `/chat` per D2)
- NG5 — Test matrix (deferred to follow-up, pattern from sibling PRs)

## 5. Architecture

### Component decomposition

```
DashboardClient (new orchestrator, ~250 LOC)
├── DashboardHero (greeting + KPI grid)
├── DashboardSection (generic section wrapper: header + body)
│   - 5 instances, one per entity
└── (no footer)
```

Section content components (route-private, route-local: `_components/sections/`):
- `GamesCarousel` — 3-card carousel (cover + title + plays count)
- `PlayersAvatarList` — 5 avatars + count badge
- `AgentsCompactGrid` — 2×2 compact cards (icon + name + model + status)
- `SessionsTimeline` — 3 rows + live indicator
- `EventsList` — 3 cards full-width with date display + participant ratio

### Layout (responsive)

```
Mobile (< sm):
  Hero (greeting + KPI 2×2)
  Games
  Players
  Agents
  Sessions
  Events

Desktop (≥ sm):
  Hero (greeting + KPI 4×1)
  ┌──────────────┬──────────────┐
  │  Games       │  Players     │
  ├──────────────┼──────────────┤
  │  Agents      │  Sessions    │
  └──────────────┴──────────────┘
  ┌─────────────────────────────┐
  │  Events (full-width)        │
  └─────────────────────────────┘
```

## 6. Acceptance criteria

- **AC1** — `DashboardClient.tsx` orchestrator renders Hero + 5 sections per layout above. Legacy `DashboardClient.tsx` (749 LOC) **removed in same PR** (verifiable via `git diff --stat`).
- **AC2** — Hero: greeting "{salute}, {name}" via `useAuth().user.displayName`; 4-KPI grid wired to `useLibraryStats` (fallback "—" for missing fields).
- **AC3** — 5 section content components implemented:
  - GamesCarousel: 3 inline cards from `useGames`
  - PlayersAvatarList: 5 avatars derived from unique `useActiveSessions.data.sessions[].players[]` by name
  - AgentsCompactGrid: 2×2 from `useAgents` (max 4 items)
  - SessionsTimeline: 3 inline + pulse badge if any `state==='live'`
  - EventsList: 3 inline from `useUpcomingGameNights`
- **AC4** — Each section has empty-state with localised CTA per spec table.
- **AC5** — Layout responsive: mobile stacked, desktop 2×2 + Events full-width.
- **AC6** — i18n: keys under `pages.dashboard.*` in `it.json` + `en.json`; no hardcoded user-facing strings.
- **AC7** — 3 PostHog events fired:
  - `dashboard_section_clicked` (`{ section, entityType }`) on "Vedi tutto" click
  - `dashboard_view_all_clicked` (`{ section, viewAllHref }`) on section header view-all
  - `dashboard_empty_cta_clicked` (`{ section, ctaHref }`) on empty-state CTA
- **AC8** — `page.tsx` unchanged: `RequireRole` wrapper, metadata export, `dynamic = 'force-dynamic'` preserved.
- **AC9** — `pnpm typecheck && pnpm lint && pnpm build` green.
- **AC10** — Parent spec §6 references this PR as concrete implementation evidence (optional, PR description suffices).
- **AC11 (deferred)** — Test matrix (unit per section + integration + E2E + visual diff) deferred to follow-up PR, consistent with sibling Stage 3 FE PRs (#1151, #1153, #1160, #1163).

## 7. Examples (Gherkin)

```gherkin
Scenario: First-run user (all empty states)
  Given user has no games, no sessions, no agents, no events
  When user opens /dashboard
  Then Hero renders greeting + 4 KPI showing 0 / 0 / 0h / —
  And all 5 sections render empty-state with localised CTA
  And no PostHog events fire on initial render

Scenario: Live session indicator
  Given user has 1 session with state='live'
  When user opens /dashboard
  Then Sessions section header shows pulse-animated "LIVE" badge
  And the live session is the first row in SessionsTimeline

Scenario: View-all navigation
  Given user has ≥1 game
  When user clicks "Vedi libreria" in Games section
  Then router pushes /library
  And PostHog event "dashboard_view_all_clicked" fires with { section: 'games', viewAllHref: '/library' }

Scenario: Players derived from sessions
  Given useActiveSessions returns 2 sessions, one with players [Marco, Sara], one with players [Marco, Luca]
  When PlayersAvatarList renders
  Then 3 unique avatars are shown: Marco, Sara, Luca
  And count badge shows "3 di 3"

Scenario: Empty state CTA telemetry
  Given user has 0 events
  When user clicks "Pianifica serata" in Events section empty-state
  Then router pushes /game-nights/new
  And PostHog event "dashboard_empty_cta_clicked" fires with { section: 'events', ctaHref: '/game-nights/new' }
```

## 8. Sub-issue

`[WS-Stage3] dashboard FE — REFACTOR-FORWARD per sp4-dashboard mockup (Stage 3 cluster)`
- Blocked-by: nothing (mockup #1153 merged, all hooks live except Players which is derived)
- Soft-coordinated-with: nothing
- Branch: `feature/issue-{N}-stage3-dashboard-fe` from `main-dev`

## 9. Rollback

Revert PR — `DashboardClient.tsx` PR #309 reinstated via revert. Net behavior: dashboard returns to pre-Stage-3 layout.

## 10. References

- Mockup SoT: `admin-mockups/design_files/sp4-dashboard.jsx`
- Existing implementation (to be replaced): `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx`
- Existing test: `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx` (will need update for new component shape)
- Parent roadmap: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` §3, §6
- Pattern precedents: #1151 (hub mockups), #1153 (dashboard mockup), #1160 (discover FE), #1163 (toolkit-detail FE)
