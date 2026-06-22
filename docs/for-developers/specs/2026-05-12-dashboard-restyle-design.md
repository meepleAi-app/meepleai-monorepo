---
title: Dashboard Restyle (Gaming Hub) — Token-First Mock Fidelity
date: 2026-05-12
status: approved (post-review fixes applied — 4 BLOCKERS + 7 MAJOR resolved, decisions D21–D30)
scope: apps/web — Gaming Hub user dashboard (`/dashboard`)
parent: design-system de-versioning umbrella (#1023)
related:
  - docs/for-developers/specs/2026-05-12-token-canonicalization.md
  - docs/for-developers/specs/2026-05-11-design-system-deversioning.md
  - docs/for-developers/specs/2026-04-26-v2-design-migration.md
  - admin-mockups/design_files/00-hub.html
  - admin-mockups/design_files/04-design-system.html
  - admin-mockups/design_files/tokens.css
  - admin-mockups/design_files/components.css
---

## 1. Context

The authenticated user dashboard (`apps/web/src/app/(authenticated)/dashboard/page.tsx` + `DashboardClient.tsx`) is the "Gaming Hub" — the landing surface for authenticated users after login. It exposes four entity zones (Giochi · Sessioni · Agenti AI · Toolkit) wrapped by a `GreetingStrip` greeting and an embedded "new-user catalog fallback" when the library is empty.

The mock library under `admin-mockups/design_files/` (entry point `00-hub.html`, design system reference `04-design-system.html`) defines a token-first design language: cream `#f7f3ee` light surface, warm-dark `#14100a` dark mode, entity-colored HSL palette, Quicksand/Nunito/JetBrains Mono typography stack, 4px spacing grid, motion via `var(--ease-out|--ease-spring)`, and component primitives (hub-card, stat-row, hero, EntityZone, ToolkitGrid).

This spec defines how to re-style the existing dashboard to **maximum mock fidelity** (Approach 3 — "Token-First Totale") while honoring two active project freezes:
- **Design System De-versioning FREEZE** (#1023): no new files under `apps/web/src/components/v2/**` or `apps/web/src/components/ui/v2/**` until Stage 2 codemod lands. All new files in this spec live under canonical paths (`components/dashboard/`, `components/ui/data-display/`).
- **Token Canonicalization** (DS-15): zero hardcoded color utilities (lint `local/no-hardcoded-color-utility` at `error`). All visual styling uses semantic tokens + entity utilities.

## 2. Goals (functional)

1. Visual parity of the user dashboard with the mock design system: typography, spacing, color tokens, motion, hub-card pattern, stat-row pattern.
2. New `StatsRow` 4-column statistics block (Giochi · Sessioni · Agenti · Eventi prossimi) replacing the inline stats in the current `GreetingStrip`.
3. Hybrid hero (Domanda 5 = C): mid-size H1 with gradient mark on user name + lead + 2 CTAs + stat-row below. Replaces `GreetingStrip` component entirely.
4. `DiscoverCarousel` primitive (mock `sp4-discover.html` pattern) for the Sessions zone: scroll-snap track, fade-edge gradients, hover-revealed arrow controls. Reusable beyond dashboard.
5. `EntityZone` header refresh: entity-tinted badge (was: colored dot), font-display title, tabular-nums count.
6. `ToolkitGrid` bare-zone rendering (no `HubLayout` wrapper, no search/filter chip).
7. Forward-looking 4th stat: `Eventi prossimi` (`useUpcomingGameNights` hook, already exists).
8. Per-component error retry UX (StatCard, EntityZone, DiscoverCarousel) — scope extended per Domanda 3 from initial restyle-only.
9. Reduced-motion fallback (border tint + shadow only, no translate/scale).
10. Dark mode parity (entity color tokens auto-shift via `[data-theme="dark"]`).
11. A11y AA compliance verified via `jest-axe` + Playwright axe on light + dark themes.

## 3. Non-Goals (out of scope)

- ❌ Cockpit-rich layout ("Now & Next" panel, AI suggestions, achievements widget) — purpose was Domanda 2 = "A con accenti di B" but scope Domanda 3 = "A (restyling)". Eventual phase 2.
- ❌ Refactor of `MeepleCard` and its variants (GridCard, FeaturedCard, CompactCard, FocusCard, HeroCard, ListCard). Used as-is.
- ❌ Refactor of `HubLayout` (used as-is for Giochi/Sessioni/Agenti zones).
- ❌ New backend endpoints. All stats and zone data come from existing hooks.
- ❌ Adoption of `02-desktop-patterns.html` Pattern A/B/C (split-view, sidebar+main+drawer, full-page tabs). Dashboard remains a vertical responsive hub.
- ❌ Onboarding / Auth flow redesign (separate mocks, separate specs).
- ❌ Cleanup of legacy token namespaces `--admin-*` and `--mc-*` (deferred to a follow-up per CLAUDE.md DS-16 note).
- ❌ Mobile native gestures (swipe-between-entities, drag-to-dismiss). Carousel is scroll-snap only.

## 4. Approach: "Token-First Totale" (Approach 3)

Maximum fidelity to the mocks. Five-step token-driven refactor:

1. Create four new components under canonical paths (FREEZE-compliant).
2. Refactor `DashboardClient.tsx` to compose the new components; drop `GreetingStrip` usage; add `useLibraryStats` + `useUpcomingGameNights` data flow.
3. Light header refresh on `EntityZone` (badge swap).
4. Delete `GreetingStrip` source + test files.
5. Update consumer tests, add new unit tests, run e2e + a11y CI.

Single PR target branch `main-dev`, branch naming `feature/dashboard-restyle-mock-fidelity`. No feature flag (visual change is reversible via `git revert`, rollback window <10 min).

## 5. Component architecture

> **Naming clarification (post-review)**: an existing `apps/web/src/components/dashboard/StatsRow.tsx` already exists, dedicated to the **admin overview** route (consumer: `apps/web/src/app/admin/(dashboard)/overview/page.tsx`, `KPIStatsRow.tsx`). It reads from `useDashboardStore`, uses `lucide-react` icons, and is not reusable for Gaming Hub. To avoid file-name collision, the new dashboard components are namespaced with the `Dashboard` prefix: `DashboardStatsRow`, `DashboardStatCard`. The existing admin `StatsRow` is left untouched.

### 5.1 JSX hierarchy (target)

```
DashboardClient (refactor)
├── <DashboardHero>                                   ← NEW (replaces GreetingStrip)
│   ├── <HeroKicker>            "BENVENUTO · {weekday, locale=it-IT}"   /* useEffect-init, see §7.4 */
│   ├── <HeroTitle>             "Ciao, {Name}" — gradient mark on {Name}
│   ├── <HeroLead>              "La tua tavola da gioco di oggi…"
│   └── <HeroActions>           CTA primary "+ Nuova sessione" · CTA secondary "+ Aggiungi gioco"
│
├── <DashboardStatsRow>                               ← NEW (4-col grid, namespaced)
│   ├── <DashboardStatCard entity="game"    value={libraryStats.totalGames}   label="Giochi"    href="/library">
│   ├── <DashboardStatCard entity="session" value={sessions.length}            label="Sessioni"  href="/sessions">
│   ├── <DashboardStatCard entity="agent"   value={agents.length}              label="Agenti"    href="/agents">
│   └── <DashboardStatCard entity="event"   value={upcoming.length}            label="Eventi"    href="/game-nights">
│
├── <EntityZone entity="game">    HubLayout → MeepleCardGrid             (variant=grid)
├── <EntityZone entity="session"> HubLayout → DiscoverCarousel<MeepleCard>
├── <EntityZone entity="agent">   HubLayout → MeepleCardGrid             (variant=grid)
└── <EntityZone entity="toolkit"> ToolkitGrid (bare, no HubLayout)
```

`EmptyCTA` (currently inline in `DashboardClient.tsx`, with hardcoded `bg-amber-600` / `border-amber-600`) is **extracted and refactored** to entity tokens — see §5.3.

### 5.2 New files (4)

| Path | Purpose |
|---|---|
| `apps/web/src/components/dashboard/DashboardHero.tsx` | Hybrid hero block. Replaces GreetingStrip. Sub-blocks (HeroKicker, HeroTitle, HeroLead, HeroActions) inlined for now; split deferred to §13.2. |
| `apps/web/src/components/dashboard/DashboardStatsRow.tsx` | Grid wrapper (responsive 4 → 2 → 1 columns). Receives `stats: { games, sessions, agents, events }` + per-key loading/error/refetch. **Namespaced to avoid collision with existing admin `StatsRow`**. |
| `apps/web/src/components/dashboard/DashboardStatCard.tsx` | Atomic stat card. Props: `entity`, `value`, `label`, `href`, `isLoading?`, `isError?`, `isFetching?`, `onRetry?`. **Namespaced** for the same reason. |
| `apps/web/src/components/ui/data-display/discover-carousel/DiscoverCarousel.tsx` | Reusable primitive. Props: `children`, `ariaLabel`, `itemWidth?`, `gap?`. Internal: scroll-snap track, fade gradients, arrow controls. Internal `useId()` wires `aria-controls` between arrows and track. Re-exported via `discover-carousel/index.ts`. |

All paths are **outside** `components/v2/**` and `components/ui/v2/**` → FREEZE-compliant.

### 5.3 Modified files (4)

| Path | Modification |
|---|---|
| `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` | Swap GreetingStrip → DashboardHero, insert DashboardStatsRow, wrap Sessions zone in DiscoverCarousel, drop HubLayout from Toolkit zone, add `useLibraryStats` + `useUpcomingGameNights` hooks. **Remove inline `EmptyCTA`** (extracted to its own file). |
| `apps/web/src/components/dashboard/EntityZone.tsx` | Replace `entity-dot` (colored circle) with `entity-badge` (`e-tint` pill). Title becomes `<h2 id={titleId}>` (font-display fs-xl); section has `aria-labelledby={titleId}`. Count tabular-nums. |
| `apps/web/src/components/dashboard/EmptyCTA.tsx` (**NEW extraction**) | New file: extract inline `EmptyCTA` from `DashboardClient.tsx`. Refactor token contract: replace `bg-amber-600` → `bg-[hsl(var(--e))]`, `border-amber-600` → `border-[hsl(var(--e)/0.6)]`, `border-[rgba(180,130,80,0.25)]` → `border-[hsl(var(--e)/0.25)]`. Accept `entity` prop for color theming. |
| `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx` | Update assertions: DashboardHero, DashboardStatsRow, DiscoverCarousel-wrapped Sessions zone, EmptyCTA imported from new path. |

> Note: `EmptyCTA.tsx` is **net-new** but conceptually a "modification" (extraction from inline). Listed here to make the lint-tokens compliance gate explicit.

### 5.4 Deleted files (2)

| Path | Reason |
|---|---|
| `apps/web/src/components/dashboard/GreetingStrip.tsx` | Replaced by DashboardHero. |
| `apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx` | Suite migrates to `DashboardHero.test.tsx`. |

### 5.5 New tests (6)

- `apps/web/src/__tests__/components/dashboard/DashboardHero.test.tsx`
- `apps/web/src/__tests__/components/dashboard/DashboardStatsRow.test.tsx`
- `apps/web/src/__tests__/components/dashboard/DashboardStatCard.test.tsx`
- `apps/web/src/__tests__/components/dashboard/EmptyCTA.test.tsx`
- `apps/web/src/components/ui/data-display/discover-carousel/__tests__/DiscoverCarousel.test.tsx`
- `apps/web/src/__tests__/components/dashboard/a11y.test.tsx` (jest-axe coverage of all new components)

### 5.6 MeepleCard — NO TOUCH

`MeepleCard` and its variants live under `apps/web/src/components/ui/data-display/meeple-card/`. They are consumed by many pages beyond the dashboard. This spec does not modify their source, variants, or features (`Carousel3D` exists but is not used: simpler `DiscoverCarousel` primitive is preferred for scroll-snap rows).

## 6. Visual & token mapping

### 6.1 Token contract (mandatory)

Use only **semantic tokens** and **entity utilities**. Hardcoded color utilities (`bg-white`, `bg-slate-*`, `text-gray-*`, `border-zinc-*`, etc.) are forbidden by ESLint `local/no-hardcoded-color-utility` at `error` (DS-15).

| Category | Allowed | Forbidden |
|---|---|---|
| Background | `bg-background`, `bg-card`, `bg-muted`, `bg-[hsl(var(--c-{entity})/0.NN)]` | `bg-white`, `bg-slate-*`, `bg-gray-*` |
| Text | `text-foreground`, `text-muted-foreground`, `text-[hsl(var(--c-{entity}))]` | `text-gray-*`, `text-slate-*` |
| Border | `border-border`, `border-border-strong`, `border-[hsl(var(--c-{entity})/0.NN)]` | `border-zinc-*` |
| Entity tint | `e-game`, `e-session`, `e-agent`, `e-event`, `e-toolkit` (via `data-entity` attr) | hardcoded HSL strings |
| Font | `font-quicksand`, `font-nunito`, `font-mono` | – |

Exemption (DS-15): `text-white` and `border-white` allowed when a colored bg is present on the same element (entity utility, gradient, `bg-primary/secondary/accent`).

### 6.2 DashboardHero

```css
.dashboard-hero {
  max-width: 1200px;
  margin: 0 auto;
  padding: var(--s-10) var(--s-6) var(--s-7);   /* 64px 24px 32px */
}
.hero-kicker {
  font-family: var(--f-mono);
  font-size: var(--fs-xs);          /* 11px */
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.12em;
  margin-bottom: var(--s-3);        /* 12px */
}
.hero-title {
  font-family: var(--f-display);
  font-size: clamp(32px, 5vw, 48px);
  line-height: var(--lh-tight);
  letter-spacing: -0.02em;
  margin-bottom: var(--s-4);
}
.hero-title .mark {
  background: linear-gradient(120deg,
    hsl(var(--c-game)),
    hsl(var(--c-event)) 70%,
    hsl(var(--c-player)));
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.hero-lead {
  font-size: var(--fs-lg);          /* 17px */
  color: var(--text-sec);
  max-width: 680px;
  line-height: var(--lh-snug);
}
.hero-actions {
  display: flex;
  gap: var(--s-2);
  margin-top: var(--s-5);
}
.hero-actions .primary {
  background: hsl(var(--c-game));
  color: #fff;
  border-radius: var(--r-pill);
  padding: var(--s-2) var(--s-5);
  font-family: var(--f-display);
  font-weight: var(--fw-bold);
  box-shadow: var(--shadow-sm);
}
.hero-actions .secondary {
  background: var(--bg-card);
  color: var(--text);
  border: 1px solid var(--border);
  border-radius: var(--r-pill);
  padding: var(--s-2) var(--s-5);
}
```

### 6.3 StatsRow + StatCard

```css
.stats-row {
  display: grid;
  gap: var(--s-4);
  grid-template-columns: repeat(4, 1fr);
  margin: 0 0 var(--s-9);
}
@media (max-width: 800px) { .stats-row { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 480px) { .stats-row { grid-template-columns: 1fr; } }

.stat-card {
  padding: var(--s-5);
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--r-xl);
  position: relative;
  overflow: hidden;
  transition: all var(--dur-md) var(--ease-spring);
}
.stat-card::before {
  content: '';
  position: absolute; inset: 0;
  background: radial-gradient(circle at top right,
              hsl(var(--e) / 0.10), transparent 60%);
  pointer-events: none;
  opacity: 0.7;
  transition: opacity var(--dur-md);
}
.stat-card:hover {
  transform: translateY(-3px) scale(1.02);
  border-color: hsl(var(--e) / 0.4);
  box-shadow: var(--shadow-md);
}
.stat-card:hover::before { opacity: 1; }
.stat-card .value {
  font-family: var(--f-display);
  font-size: var(--fs-3xl);         /* 32px */
  font-weight: var(--fw-ext);
  letter-spacing: -0.02em;
  color: hsl(var(--e));
  font-variant-numeric: tabular-nums;
}
.stat-card .label {
  font-size: var(--fs-sm);
  margin-top: var(--s-1);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--text-sec);
  font-weight: var(--fw-bold);
}

/* Dark mode tweak — restore visibility of radial tint on dark surface */
[data-theme="dark"] .stat-card::before {
  background: radial-gradient(circle at top right,
              hsl(var(--e) / 0.18), transparent 60%);
}
```

### 6.4 EntityZone (refactor)

```css
.zone-header {
  display: flex;
  align-items: center;
  gap: var(--s-2);
  padding-bottom: var(--s-3);
}
.zone-badge {
  display: inline-flex;
  align-items: center;
  gap: var(--s-1);
  padding: var(--s-1) var(--s-2);
  background: hsl(var(--e) / 0.12);
  color: hsl(var(--e));
  border-radius: var(--r-sm);
  font-family: var(--f-display);
  font-size: var(--fs-xs);          /* 11px (closest token; mock used 10px, accepted small drift) */
  font-weight: var(--fw-ext);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.zone-title {
  font-family: var(--f-display);
  font-size: var(--fs-xl);          /* 20px */
  font-weight: var(--fw-bold);
  color: var(--text);
}
.zone-count {
  font-family: var(--f-mono);
  font-size: var(--fs-xs);
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}
.zone-link {
  margin-left: auto;
  font-family: var(--f-display);
  font-size: var(--fs-sm);
  font-weight: var(--fw-bold);
  color: hsl(var(--e));
}
```

### 6.5 DiscoverCarousel (primitive)

```css
.discover-carousel { position: relative; }
.discover-track {
  display: flex;
  gap: var(--s-3);
  overflow-x: auto;
  scroll-snap-type: x mandatory;
  scrollbar-width: thin;
  scrollbar-color: var(--border) transparent;
  padding-bottom: var(--s-2);
}
.discover-track > * { scroll-snap-align: start; flex-shrink: 0; }
.discover-track:focus-visible {
  outline: 2px solid hsl(var(--c-game));
  outline-offset: 2px;
  border-radius: var(--r-md);
}
.discover-fade {
  pointer-events: none;
  position: absolute; top: 0; bottom: 0; width: 48px;
  z-index: 1;
}
.discover-fade.left  { left: 0;  background: linear-gradient(to right, var(--bg), transparent); }
.discover-fade.right { right: 0; background: linear-gradient(to left,  var(--bg), transparent); }
.discover-arrow {
  position: absolute; top: 50%; transform: translateY(-50%);
  width: 40px; height: 40px; border-radius: 50%;
  background: var(--bg-card);
  border: 1px solid var(--border);
  box-shadow: var(--shadow-md);
  display: grid; place-items: center;
  cursor: pointer;
  opacity: 0;
  transition: opacity var(--dur-sm);
  z-index: 2;
}
.discover-carousel:hover .discover-arrow,
.discover-arrow:focus-visible { opacity: 1; }
.discover-arrow:focus-visible { outline: 2px solid hsl(var(--c-game)); outline-offset: 2px; }
.discover-arrow.left  { left: var(--s-2); }
.discover-arrow.right { right: var(--s-2); }
```

### 6.6 ToolkitGrid (refresh, no HubLayout wrapper)

```css
.toolkit-grid {
  display: grid;
  gap: var(--s-3);
  grid-template-columns: repeat(2, 1fr);
}
@media (min-width: 640px) { .toolkit-grid { grid-template-columns: repeat(4, 1fr); } }

.toolkit-tile {
  display: flex; flex-direction: column; align-items: center;
  gap: var(--s-2);
  padding: var(--s-5) var(--s-3);
  border: 1px solid hsl(var(--c-toolkit) / 0.25);
  background: hsl(var(--c-toolkit) / 0.06);
  border-radius: var(--r-xl);
  text-align: center;
  transition: all var(--dur-sm) var(--ease-out);
}
.toolkit-tile:hover {
  transform: translateY(-2px);
  border-color: hsl(var(--c-toolkit) / 0.6);
  box-shadow: var(--shadow-md);
}
.toolkit-tile .icon { font-size: 28px; }
.toolkit-tile .name {
  font-family: var(--f-display);
  font-size: var(--fs-base);
  font-weight: var(--fw-bold);
  color: hsl(var(--c-toolkit));
}
.toolkit-tile .desc { font-size: var(--fs-xs); color: var(--text-muted); }
```

### 6.7 Reduced motion

The fallback retains border tint + shadow but disables transform-based effects (translate/scale). Transitions on `border-color` and `box-shadow` are preserved so the user still gets visual feedback on hover.

```css
@media (prefers-reduced-motion: reduce) {
  /* Disable transforms entirely */
  .stat-card:hover, .toolkit-tile:hover { transform: none; }
  /* But keep border + shadow transitions for non-motion visual feedback */
  .stat-card, .toolkit-tile {
    transition: border-color var(--dur-sm) var(--ease-out),
                box-shadow var(--dur-sm) var(--ease-out);
  }
  .discover-arrow { transition: opacity var(--dur-sm); }
}
```

## 7. Data flow & state management

### 7.1 Hooks used

| Hook | Status | Returns | Consumers |
|---|---|---|---|
| `useAuth()` | existing | `user.displayName`, `user.role` | DashboardHero (greeting), `RequireRole` gate |
| `useLibraryStats()` | **new dashboard usage** | `UserLibraryStats.totalGames` | StatsRow (games count) |
| `useLibrary({page:1, pageSize:12})` | existing | `PaginatedLibraryResponse.items[]` | EntityZone games (card list) |
| `useActiveSessions()` | existing | `sessionsData.sessions[]` | StatsRow (sessions count), EntityZone sessions |
| `useAgents({ activeOnly:false })` | existing | `agentsData[]` | StatsRow (agents count), EntityZone agents |
| `useUpcomingGameNights(enabled=true)` | **new dashboard usage** | `GameNightDto[]` (flat array) | StatsRow (events count) |
| `useGames(undefined, undefined, 1, 20)` | existing | catalog top — lazy fallback | NewUserGamesBlock (only when library empty) |
| `useBatchGameStatus(gameIds)` | existing | `status.inLibrary` per gameId | NewUserGamesBlock |
| `useMiniNavConfig(...)` | existing | side-effect on mini-nav store | DashboardClient layout breadcrumb |

### 7.2 Fetch ordering

All hooks fire in parallel on mount (React Query default). No sequential dependencies. New-user catalog fallback (`useGames`) is **lazy-enabled** only when `!libraryLoading && libraryData?.items?.length === 0` — unchanged from current behavior.

### 7.3 Stats derivation

```ts
const { data: libraryStats }    = useLibraryStats();
const { data: sessionsData }    = useActiveSessions();
const { data: agentsData }      = useAgents({ activeOnly: false });
const { data: upcomingNights }  = useUpcomingGameNights();   // GameNightDto[]

const stats = {
  games:    libraryStats?.totalGames ?? 0,
  sessions: sessionsData?.sessions?.length ?? 0,
  agents:   Array.isArray(agentsData) ? agentsData.length : 0,
  events:   upcomingNights?.length ?? 0,                      // flat array, not wrapped
};
```

Reference: `apps/web/src/lib/api/clients/gameNightsClient.ts:39-41` — endpoint `GET /api/v1/game-nights` returns `GameNightDto[]`, parsed via `z.array(GameNightDtoSchema)`. The hook surfaces this array directly via `useQuery.data`.

### 7.4 Loading states

| Component | Loading flag | Placeholder |
|---|---|---|
| DashboardHero | never | `displayName` cached by `useAuth` (immediate); kicker date initialized via `useEffect` (locale-aware, **client-only** to avoid SSR hydration mismatch) — initial server render shows empty kicker, populated post-mount |
| StatCard | per-key isLoading | `bg-muted animate-pulse rounded h-8 w-12` in value position; label stays visible (no layout shift) |
| EntityZone games | `libraryLoading` | `LoadingSkeleton count=6` (existing) |
| EntityZone sessions | `sessionsLoading` | `LoadingSkeleton count=4` inside DiscoverCarousel |
| EntityZone agents | `agentsLoading` | `LoadingSkeleton count=6` |
| EntityZone toolkit | never | 4 static tiles always present |
| NewUserGamesBlock | `libraryLoading` | `LoadingSkeleton count=6` + top alert |

### 7.5 Error UX (scope extended per Domanda 3 confirmation)

| Surface | Error trigger | UX |
|---|---|---|
| DashboardStatCard | `hook.isError === true` | `value` = `"—"`, `title` attr `"Errore caricamento {label}"`. Card becomes a button (or stays a Link with overlay button) invoking `onRetry()`. Disabled while `isFetching` to prevent duplicate requests. |
| DashboardStatsRow (partial failures) | 1+ hooks isError | Each card shows its own error state independently. Per-card behavior is the contract. |
| DashboardStatsRow (≥3 of 4 errors) | aggregate health degraded | Banner above row: "Connessione instabile" + button "Riprova tutto" → calls all `refetch()` sequentially with single toast. Threshold ≥3 to avoid alarmist banner on transient single-hook failures. |
| DiscoverCarousel (sessions) | `sessionsError` | EmptyCTA "Errore caricamento" + button "Riprova" (gated by `isFetching`) |
| EntityZone games | `libraryError` | EmptyCTA "Errore" + button "Riprova" (gated by `isFetching`) |
| EntityZone agents | `agentsError` | EmptyCTA "Errore" + button "Riprova" (gated by `isFetching`) |
| DashboardStatCard events | `upcomingError` | **fail silently**: shows 0, no toast (events is secondary, avoid noise if game-nights service is briefly down) |

**React Query retry override** (mandatory): the 4 dashboard hooks must set `retry: 1` (down from default 3). Rationale: with default `retry: 3` + exponential backoff, an `isError=true` state means React Query has already retried 3 times. Adding a manual retry CTA on top would queue a 4th attempt while the user is staring at a 30-second-delayed error. `retry: 1` (one quick retry, then surface error within ~2 seconds) lets the manual CTA become the primary recovery path.

Retry pattern uses `sonner` toast (`<Toaster />` confirmed mounted globally in `apps/web/src/app/providers.tsx`):

```tsx
type RetryableQuery<T> = UseQueryResult<T, Error>;

function withRetry<T>(query: RetryableQuery<T>, label: string) {
  return {
    ...query,
    retry: () => {
      if (query.isFetching) return;        // guard: no duplicate fire while already fetching
      query.refetch();
      toast.info(`Riprovo a caricare ${label}…`);
    },
  };
}
```

Hook configuration (in `DashboardClient`):
```ts
const { ... } = useUpcomingGameNights({ retry: 1 });    // if hook accepts options; otherwise patch hook
// Otherwise use queryClient default options scoped to dashboard route
```

> Implementation note: `useUpcomingGameNights` currently does not accept options. Either (a) extend the hook to accept `Pick<UseQueryOptions, 'retry'>`, or (b) configure `retry: 1` at the QueryClient level via `defaultOptions.queries` for the dashboard route subtree. Option (a) is preferred — single-file hook change, no global side effect.

### 7.6 Empty states

| Zone | Threshold | Fallback |
|---|---|---|
| games | `items.length === 0` | `NewUserGamesBlock` (catalog top 12, **unchanged behavior**) |
| sessions | `sessions.length === 0` | EmptyCTA "Nessuna sessione" + primary CTA "Crea sessione" → `/sessions/new` |
| agents | `agents.length === 0` | EmptyCTA "Nessun agente" + primary "Chat" + secondary "Crea agente" |
| events (stat) | `upcoming.length === 0` | StatCard shows 0; link active to `/game-nights` |
| toolkit | never empty | 4 tiles always rendered |

### 7.7 Cache / invalidation

Dashboard is read-only — no mutations originate from `DashboardClient`. Existing mutations (`useAddGameToLibrary` invoked inside `NewUserGamesBlock`) already invalidate `library.*` and `library.stats` keys, which propagates to StatsRow automatically via React Query subscription.

### 7.8 Recents store

```tsx
useEffect(() => {
  useRecentsStore.getState().push({
    id: 'section-dashboard',
    entity: 'game',
    title: 'Home',
    href: '/dashboard',
  });
}, []);
```

Unchanged from current implementation.

## 8. States, A11y, Micro-interactions

### 8.1 Empty CTA style (mock-faithful)

```
[border 1px dashed hsl(var(--e)/0.25)]
[bg hsl(var(--e)/0.04)]
[r-xl, p-8, text-center, flex-col gap-3]
  icon            font-size: 32px; aria-hidden=true
  title           font-display fw-bold fs-base text-foreground
  sub             fs-sm text-muted-foreground max-w-sm leading-relaxed
  actions         row gap-2:
    primary       bg hsl(var(--e)) text-white r-pill px-4 py-2 shadow-sm
    secondary     border hsl(var(--e)) text-hsl(var(--e)) r-pill bg-transparent
```

### 8.2 A11y (AA) checklist

| Component | Requirement |
|---|---|
| DashboardHero | `<h1>` semantic. Kicker `aria-hidden=true` (decorative + avoids reading empty placeholder during SSR). CTAs have explicit `aria-label`. |
| DashboardStatsRow | `<nav aria-label="Statistiche personali">` wrapper. |
| DashboardStatCard | `<Link>` with `aria-label="{Label}: {value} elementi. Vai a {href}"`. Focus ring `outline 2px solid hsl(var(--c-{entity}))` offset 2px. Target ≥44×44 px. **In error state**: card becomes/wraps a `<button type="button" aria-label="Errore caricamento {Label}. Premi Invio per riprovare">` with `disabled` when `isFetching`. |
| EntityZone | `<section aria-labelledby={titleId}>` where `titleId` is generated via `React.useId()` and placed on the `<h2 id={titleId}>` element (NOT on the badge — badge is decorative). ZoneBadge `role=presentation`. `zone-link` has `aria-label="Vedi tutti i {entity}, vai a {href}"`. |
| DiscoverCarousel | `role=region`, `aria-label` from prop. Track is `<div tabIndex=0 id={trackId}>` (trackId via `React.useId()`). Arrows `aria-label="Scorri verso {sinistra\|destra}"`, `aria-controls={trackId}`, `disabled={cannotScrollFurther}`. Keyboard: `Arrow Left/Right` on focused track scrolls one item (handled by browser scroll-snap + custom keydown handler that scrolls by `itemWidth + gap`). `Home/End` scroll to first/last. **Per-item semantics**: children are expected to be `<MeepleCard>` already AAA-internally; the carousel does NOT inject `role=list/listitem` to avoid interfering with consumer semantics. Document this contract in the primitive's JSDoc. |
| EmptyCTA | `role=status` SR announces section state. Icon `aria-hidden`. CTA buttons get the same focus-ring rule. |
| ToolkitGrid | Each tile `<Link>` has `aria-label="{name}: {desc}"`. Focus ring `hsl(var(--c-toolkit))`. |

**Contrast verification** (focus rings, gradient mark, dark mode):
- All semantic background/text tokens are AA-compliant per PR #876.
- **Required pre-merge check** (per M5 review feedback): measure focus-ring contrast for orange `hsl(var(--c-game))` on cream `#f7f3ee` light background and on `#14100a` dark background. Acceptance: ≥3:1 (WCAG 1.4.11 non-text contrast). Tool: Chrome DevTools color picker on a deployed Playwright preview, or `@adobe/leonardo-contrast-colors` script in CI.
- **Required pre-merge check**: the gradient mark on hero title (orange→rose→purple) over cream light surface — verify the lightest stop (orange at 70%+ on cream) still passes 4.5:1 (text). If it fails, accept a darker entity tint or add a subtle `text-shadow` to maintain readability without losing the gradient effect.
- Automated: `jest-axe` on unit tests + `@axe-core/playwright` on e2e in both themes.

### 8.3 Micro-interactions

| Component | Animation | Duration | Easing |
|---|---|---|---|
| StatCard hover | `translateY(-3px) scale(1.02) + shadow + border tint full` | `var(--dur-md)` 280ms | `var(--ease-spring)` |
| EntityZone link hover | underline | `var(--dur-xs)` 120ms | `linear` |
| ToolkitTile hover | `translateY(-2px) + border bright + shadow` | `var(--dur-sm)` 180ms | `var(--ease-out)` |
| DiscoverCarousel arrow | `opacity 0→1` on parent hover/focus-within | `var(--dur-sm)` 180ms | `var(--ease-out)` |
| EmptyCTA primary CTA | `scale(1.03) + shadow bump` on hover | `var(--dur-sm)` 180ms | `var(--ease-out)` |
| Hero gradient mark | static | – | – |

### 8.4 Reduced motion

Fallback (confirmed Domanda 8.2): hover state retains **border tint + shadow** only, no `translate`/`scale`. CSS via `@media (prefers-reduced-motion: reduce)` (shown in §6.7).

### 8.5 Dark mode

Driven by `[data-theme="dark"]` attribute (set by `next-themes`). All semantic tokens have dark variants in `tokens.css` (lines 124-161). Entity color tokens (`--c-game`, `--c-event`, …) auto-shift to dark-tuned HSL — zero additional work for entity-tinted surfaces. Single tweak: StatCard radial gradient opacity bumped to `0.18` in dark to maintain visibility on `#1e1710` surface (see §6.3 last block).

## 9. Testing strategy

### 9.1 Unit tests (Vitest + RTL)

Target coverage: **≥90% branches** for all new components.

```
DashboardHero.test.tsx
  ✓ renders displayName from useAuth mock
  ✓ kicker initializes empty on first render (SSR-safe), populates with weekday after useEffect
  ✓ gradient mark wraps user name only
  ✓ CTAs link to /sessions/new and /library?action=add
  ✓ a11y: single h1, no nested headings
  ✓ kicker has aria-hidden=true
  ✓ reduced-motion mock: no transform applied (testing computed CSS via matchMedia mock)

DashboardStatCard.test.tsx
  ✓ renders value, label, entity color via data-entity attr
  ✓ Link href matches prop
  ✓ skeleton renders when isLoading=true (no value visible)
  ✓ value renders as "—" when isError=true
  ✓ onRetry callback fires on click when isError=true
  ✓ retry button disabled when isFetching=true (no callback fires)
  ✓ aria-label format: "{Label}: {value} elementi. Vai a {href}"
  ✓ aria-label in error state: "Errore caricamento {Label}. Premi Invio per riprovare"
  ✓ min target 44×44 px

DashboardStatsRow.test.tsx
  ✓ renders 4 cards in order: game / session / agent / event
  ✓ each card receives correct value from stats prop
  ✓ per-key isLoading/isError propagates independently
  ✓ "Riprova tutto" banner visible only when ≥3 of 4 hooks errored (threshold)
  ✓ "Riprova tutto" calls all refetch functions sequentially
  ✓ <nav aria-label="Statistiche personali"> wrapper present
  ✗ responsive grid: NOT testable in JSDOM (no CSS media query evaluation) → covered in e2e Playwright

EmptyCTA.test.tsx
  ✓ renders with entity-themed border/bg
  ✓ no hardcoded `bg-amber-*` / `border-amber-*` classes present (regression guard)
  ✓ primary + secondary actions render with correct hrefs
  ✓ icon aria-hidden=true
  ✓ role=status announces

DiscoverCarousel.test.tsx
  ✓ renders children in scrollable track
  ✓ track has scroll-snap-type x mandatory
  ✓ children have scroll-snap-align start
  ✓ arrow buttons hidden by default (opacity 0)
  ✓ arrow click scrolls track by computed itemWidth + gap
  ✓ arrow disabled when scroll cannot proceed further (left at 0, right at end)
  ✓ keyboard ArrowLeft/ArrowRight on focused track scrolls by itemWidth+gap
  ✓ Home/End keys scroll to first/last
  ✓ ariaLabel prop propagated to role=region
  ✓ track id wired to arrows via aria-controls (useId-generated)
  ✓ does NOT inject role=list/listitem on children (preserves consumer semantics)
  ✓ fade gradients applied to both sides

EntityZone.test.tsx (updated)
  ✓ ZoneBadge renders with entity-tint class (was: entity-dot)
  ✓ title is <h2> with useId-generated id
  ✓ section aria-labelledby points to title id (NOT badge)
  ✓ count uses tabular-nums and renders correctly
  ✓ "Vedi tutti" link applies entity color with aria-label

DashboardClient.test.tsx (updated)
  ✓ renders DashboardHero + DashboardStatsRow + 4 EntityZones
  ✓ stats wired to useLibraryStats, useActiveSessions, useAgents, useUpcomingGameNights
  ✓ Sessions zone wraps cards in DiscoverCarousel (querySelector role=region within session zone)
  ✓ Toolkit zone has no HubLayout (no search input rendered)
  ✓ EmptyCTA imported from new path (sessions empty + agents empty)
  ✓ NewUserGamesBlock flow unchanged (existing assertions retained)
  ✓ NO <GreetingStrip> rendered (anti-regression for delete)
  ✓ recents store push on mount unchanged
  ✓ useUpcomingGameNights configured with retry: 1 (verified via mock query options)
```

### 9.2 A11y unit tests (jest-axe)

```
a11y.test.tsx
  ✓ DashboardHero has 0 axe violations
  ✓ StatCard (all states: loading/error/loaded) has 0 axe violations
  ✓ EntityZone has 0 axe violations
  ✓ DiscoverCarousel has 0 axe violations
  ✓ DashboardClient full render has 0 violations (light + dark via data-theme prop)
```

### 9.3 E2E + visual regression (Playwright)

> **Important**: `apps/web/tests/e2e/dashboard.spec.ts` **already exists** but tests an obsolete dashboard design (references "Cronologia Chat", "Apri Chat", `section[aria-label="Greeting"]`, `aria-label="Recent games"`, etc. — none of these match the current `DashboardClient`). The existing suite must be **rewritten in full**, not appended. The file is treated as MODIFIED in §5.3 implicitly (test file, not source); explicitly listed here for clarity.

```
e2e/dashboard.spec.ts (FULL REWRITE — replaces obsolete suite)
  Authentication & Middleware
    ✓ redirects unauthenticated users to /login?from=%2Fdashboard
    ✓ allows authenticated users to access /dashboard
  Component Rendering
    ✓ DashboardHero renders with displayName (h1:has-text("Ciao,"))
    ✓ DashboardStatsRow renders 4 stat cards in order
    ✓ each EntityZone has aria-labelledby with matching h2 id
    ✓ Sessions zone renders DiscoverCarousel (role=region within session zone)
    ✓ Toolkit zone renders without HubLayout search input
  Loading & Empty States
    ✓ skeleton shimmer visible during initial load
    ✓ new-user state: library mocked empty → NewUserGamesBlock visible with catalog top
    ✓ sessions empty: EmptyCTA "Crea sessione" visible
    ✓ agents empty: EmptyCTA "Chat" + "Crea agente" visible
  Error States
    ✓ all-error state: 4 hooks mocked 500 → "Connessione instabile" banner with "Riprova tutto"
    ✓ single hook error: only affected StatCard shows "—" + retry, others render normally
    ✓ retry button disabled while isFetching (anti-double-click)
  Responsive Design
    ✓ mobile 375×667: stat-row collapses to 1-col, zones stack vertically
    ✓ tablet 800×1024: stat-row 2-col
    ✓ desktop 1280×800: stat-row 4-col, zones full-width
  Theming
    ✓ light theme: full-page screenshot baseline
    ✓ dark theme (via next-themes toggle): full-page screenshot baseline
  Accessibility
    ✓ keyboard navigation: Tab order is Hero CTAs → StatsRow cards → Zone1 cards → … → Zone4 tiles
    ✓ DiscoverCarousel: track focusable, ArrowLeft/Right scrolls, Home/End jumps to extremes
    ✓ axe full-page violations = 0 (light theme)
    ✓ axe full-page violations = 0 (dark theme)
```

Baselines stored in `apps/web/tests/e2e/__screenshots__/dashboard.*.png`. The existing snapshots (if any) are deleted as part of the rewrite. New baselines committed in the implementation PR.

### 9.4 Lint & token verification

```bash
pnpm lint --max-warnings 0     # ESLint baseline + local rules
pnpm lint:tokens                # token canonicalization (0 violations on new files)
pnpm typecheck                  # TypeScript strict, no any
```

## 10. Migration & rollout

### 10.1 Implementation order (atomic PR, TDD-friendly)

```
Step 1 — Extension hook config
1.1 UPDATE useUpcomingGameNights signature to accept Pick<UseQueryOptions, 'retry'>
    (alternative: queryClient defaultOptions; preferred: hook signature extension)

Step 2 — Create EmptyCTA (extraction + DS-15 fix)
2.1 CREATE apps/web/src/components/dashboard/EmptyCTA.tsx (extract from DashboardClient.tsx)
    - swap bg-amber-600 → bg-[hsl(var(--e))]
    - swap border-amber-600 → border-[hsl(var(--e)/0.6)]
    - swap border-[rgba(180,130,80,0.25)] → border-[hsl(var(--e)/0.25)]
    - add entity prop with type MeepleEntityType
2.2 CREATE __tests__/components/dashboard/EmptyCTA.test.tsx with regression guard
    (asserts no hardcoded `amber` classes present)

Step 3 — Create DiscoverCarousel primitive
3.1 CREATE apps/web/src/components/ui/data-display/discover-carousel/DiscoverCarousel.tsx
3.2 CREATE apps/web/src/components/ui/data-display/discover-carousel/index.ts (re-export)
3.3 CREATE apps/web/src/components/ui/data-display/discover-carousel/__tests__/DiscoverCarousel.test.tsx

Step 4 — Create new dashboard atoms (TDD: tests first)
4.1 CREATE __tests__/components/dashboard/DashboardStatCard.test.tsx
4.2 CREATE apps/web/src/components/dashboard/DashboardStatCard.tsx
4.3 CREATE __tests__/components/dashboard/DashboardStatsRow.test.tsx
4.4 CREATE apps/web/src/components/dashboard/DashboardStatsRow.tsx
4.5 CREATE __tests__/components/dashboard/DashboardHero.test.tsx
4.6 CREATE apps/web/src/components/dashboard/DashboardHero.tsx
4.7 CREATE __tests__/components/dashboard/a11y.test.tsx (jest-axe for all new components)

Step 5 — Refactor EntityZone
5.1 UPDATE apps/web/src/components/dashboard/EntityZone.tsx
    (badge swap, title <h2 id={useId()}>, section aria-labelledby)
5.2 UPDATE apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx

Step 6 — Refactor DashboardClient
6.1 UPDATE apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx
    - swap GreetingStrip → DashboardHero
    - insert <DashboardStatsRow>
    - wrap Sessions zone children in <DiscoverCarousel ariaLabel="Carosello sessioni attive">
    - drop HubLayout from Toolkit zone
    - add useLibraryStats() + useUpcomingGameNights({ retry: 1 })
    - import EmptyCTA from new path
6.2 UPDATE apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx

Step 7 — Rewrite obsolete e2e suite
7.1 REWRITE apps/web/tests/e2e/dashboard.spec.ts
    (drop "Cronologia Chat" / "QuickActions" / "BottomNav" obsolete assertions;
     add light/dark theme, responsive, a11y, error-state, new-user-state cases per §9.3)
7.2 DELETE stale snapshot baselines under tests/e2e/__screenshots__/dashboard.*.png (if any)

Step 8 — Delete obsolete component
8.1 DELETE apps/web/src/components/dashboard/GreetingStrip.tsx
8.2 DELETE apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx
8.3 grep -r "GreetingStrip\|greeting-avatar" should return 0 hits

Step 9 — A11y CI workflow audit (decision point §13.1)
9.1 Inspect .github/workflows/* for "A11y" or "axe" job
9.2 If found with `continue-on-error: true`: evaluate restore to blocking
    (likely safe given new specs include axe-clean acceptance)
9.3 Document decision in PR description

Step 10 — Pre-merge verification
10.1 pnpm lint --max-warnings 0          (catches orphan imports)
10.2 pnpm lint:tokens                     (0 violations on changed files)
10.3 pnpm typecheck                       (TypeScript strict)
10.4 pnpm test                            (all Vitest pass)
10.5 pnpm test:coverage                   (FE ≥85%)
10.6 pnpm test:e2e                        (Playwright + visual snapshots updated)
10.7 Focus-ring contrast measurement      (Chrome DevTools, light + dark; ≥3:1 required)
10.8 Gradient mark contrast measurement   (light surface only; ≥4.5:1 required)
```

### 10.2 Branch & PR

- Branch: `feature/dashboard-restyle-mock-fidelity` (off `main-dev`)
- PR target: `main-dev`
- PR description: includes before/after screenshots (mandatory, per DoD §11)
- Reviewers: at least 1 frontend reviewer for visual + a11y review

### 10.3 Rollback strategy

No feature flag. Restyle is a visual change; rollback is `git revert` of the merge commit (single atomic PR). Window <10 min.

## 11. CI verifications (PR-blocking)

| Check | Tool | Pass criteria |
|---|---|---|
| Lint | `pnpm lint --max-warnings 0` | 0 warnings, 0 errors |
| Token lint | `pnpm lint:tokens` | 0 violations in changed files |
| TypeScript | `pnpm typecheck` | 0 errors |
| Vitest unit | `pnpm test` | All pass |
| Vitest coverage | `pnpm test:coverage` | FE ≥85% (gate) |
| Playwright e2e | `pnpm test:e2e` | All pass, snapshots match |
| Playwright a11y (axe) | `@axe-core/playwright` in `e2e/dashboard.spec.ts` | 0 critical/serious violations |
| Frontend - Build | `pnpm build` | next build succeeds |
| Frontend - A11y E2E | dedicated workflow if extant | **Sub-task pre-merge: verify A11y CI job presence and status; if `continue-on-error: true`, evaluate restoring blocking per CLAUDE.md DS-15 historical note. Decision deferred to implementation PR.** |

### 11.1 FREEZE compliance audit

| Rule | Status | Evidence |
|---|---|---|
| No new files under `apps/web/src/components/v2/**` | ✅ | All new paths listed in §5.2 are outside v2 |
| No new files under `apps/web/src/components/ui/v2/**` | ✅ | `discover-carousel/` lives in `components/ui/data-display/` |
| Only semantic tokens + entity utilities | ✅ | Verified by `lint:tokens` (error mode) |
| Dark mode via `data-theme` attribute | ✅ | No `dark:*` Tailwind variants on hardcoded colors |
| Token canonicalization (no new token names) | ✅ | Reuses existing `--c-*`, `--s-*`, `--r-*`, `--bg-*`, `--text-*`, `--shadow-*`, `--dur-*`, `--ease-*` |

## 12. Definition of Done

- [ ] 4 new components created in canonical paths (§5.2) — `DashboardHero`, `DashboardStatsRow`, `DashboardStatCard`, `DiscoverCarousel`
- [ ] 1 extracted component (`EmptyCTA`) created with entity-token contract, no `bg-amber-*`/`border-amber-*` remaining
- [ ] 5 new unit test suites with ≥90% branches coverage (Hero, StatsRow, StatCard, EmptyCTA, DiscoverCarousel)
- [ ] 1 new a11y test suite (jest-axe) with 0 violations
- [ ] `useUpcomingGameNights` hook signature extended to accept `retry` option (or queryClient default applied)
- [ ] `EntityZone.tsx` refreshed: title is `<h2 id={useId()}>`, `aria-labelledby` points to title, badge `role=presentation`
- [ ] `DashboardClient.tsx` refactored + test suite updated to assert new tree
- [ ] `GreetingStrip.tsx` and its test deleted; `grep -r "GreetingStrip\|greeting-avatar"` returns 0 hits
- [ ] `apps/web/tests/e2e/dashboard.spec.ts` fully rewritten (obsolete assertions removed); stale snapshots deleted
- [ ] `pnpm lint --max-warnings 0` ✅
- [ ] `pnpm lint:tokens` 0 violations on changed files ✅
- [ ] `pnpm typecheck` ✅
- [ ] `pnpm test` all pass ✅
- [ ] `pnpm test:coverage` FE ≥85% ✅
- [ ] `pnpm test:e2e` all pass ✅
- [ ] jest-axe + Playwright axe: 0 critical/serious violations
- [ ] Focus-ring contrast measured ≥3:1 (orange on cream light, orange on warm-dark dark)
- [ ] Hero gradient mark contrast measured ≥4.5:1 on light surface
- [ ] PR description includes before/after screenshots in light + dark + mobile + desktop
- [ ] PR description references this spec
- [ ] PR review approved
- [ ] PR merged to `main-dev`, branch auto-deleted (repo policy)
- [ ] A11y CI job status sub-task resolved (verified, decision documented per §13.1)

## 13. Open questions / TBD

The following are intentional deferrals — not blockers for the spec, resolved during implementation:

1. **A11y CI workflow restoration** (§11): identify the workflow file under `.github/workflows/*` that runs the Frontend A11y E2E job. If `continue-on-error: true` is still present (CLAUDE.md historical note suggests it might be), evaluate restoring `continue-on-error: false` as part of this PR. Decision must be made during implementation; if the workflow is now blocking, no action needed.
2. **DashboardHero structural split**: optionally extract `HeroKicker`, `HeroTitle`, `HeroLead`, `HeroActions` into separate files under `components/dashboard/` for testability. Default: keep inline as semantic blocks within `DashboardHero.tsx`. Decide at implementation based on file size and testing ergonomics.
3. **Toaster path verification**: `<Toaster />` confirmed in `apps/web/src/app/providers.tsx`. Implementation can import `toast` from `sonner` without additional setup.

## 14. Decisions log

| # | Decision point | Choice | Source |
|---|---|---|---|
| D1 | Target dashboard | User Gaming Hub (`/dashboard`) | Domanda 1 = A |
| D2 | Purpose | Hub of launch + minimal cockpit accents | Domanda 2 = "A con accenti di B" |
| D3 | Scope | Restyling only (no new cockpit sections) | Domanda 3 = A |
| D4 | Form factor | Responsive balanced (mobile + desktop) | Domanda 4 = C |
| D5 | Hero style | Hybrid (mid H1 + lead + stat-row 4-col below) | Domanda 5 = C |
| D6 | 4th stat | Eventi prossimi (`useUpcomingGameNights`) | Domanda 6 = B |
| D7 | Architecture | DashboardHero + DashboardStatsRow + DashboardStatCard + DiscoverCarousel primitive | confirmed |
| D8 | Delete vs alias | Delete `GreetingStrip` entirely | confirmed |
| D9 | DiscoverCarousel placement | Reusable primitive at `components/ui/data-display/` | confirmed |
| D10 | Toolkit zone wrapper | No HubLayout (bare grid) | confirmed |
| D11 | Hero gradient mark | Triplete `game→event→player` | confirmed |
| D12 | StatCard hover | More marked: `translateY(-3px) + scale(1.02) + shadow + border tint` | confirmed |
| D13 | Toolkit entity color | Verde (`--c-toolkit`) | confirmed |
| D14 | Error UX | Extended scope, per-component retry with `isFetching` guard | Domanda §7.5 = "si cambia scope" |
| D15 | Events fail | Silent on error (no toast, show 0) | confirmed |
| D16 | `useLibrary.totalCount` | Verified to exist, but use `useLibraryStats().totalGames` (dedicated stats hook) | verification step §7.3 |
| D17 | `<Toaster />` global | Verified mounted in `app/providers.tsx` | verification step §13.3 |
| D18 | A11y CI restore | Sub-task pre-merge, decision deferred to implementation | §11, §13.1 |
| D19 | Issue tracking | PR-only, no dedicated issue | confirmed |
| D20 | Implementation approach | Approach 3 — Token-First Totale | choice "3" |
| D21 | **Naming collision (`StatsRow`/`StatCard`)** | Existing admin-overview `StatsRow.tsx` left untouched. New dashboard components namespaced: `DashboardStatsRow`, `DashboardStatCard` | review feedback B1 |
| D22 | **Obsolete e2e suite** | `tests/e2e/dashboard.spec.ts` exists but tests pre-Gaming-Hub design ("Cronologia Chat", "QuickActions", "BottomNav"). Fully rewrite — no merge with old | review feedback B2 |
| D23 | **`EmptyCTA` refactor** | Currently inline in DashboardClient with hardcoded `bg-amber-*`. Extract to `components/dashboard/EmptyCTA.tsx`, parameterize by `entity`, swap to entity tokens | review feedback B3 |
| D24 | **Retry pattern** | React Query default `retry: 3` overridden to `retry: 1` on dashboard hooks; manual retry CTAs gated by `disabled={query.isFetching}` to prevent duplicate requests | review feedback B4 |
| D25 | **Kicker SSR hydration** | Date kicker initialized via `useEffect` (client-only). Server render = empty kicker; populated post-mount. Avoids hydration mismatch | review feedback M1 |
| D26 | **DiscoverCarousel aria-controls** | Internal `React.useId()` generates trackId, wires arrows ↔ track | review feedback M2 |
| D27 | **EntityZone aria-labelledby** | Title (`<h2 id={useId()}>`) carries id; section's `aria-labelledby` points to title, not badge | review feedback M4 |
| D28 | **Reduced motion CSS** | Disable transforms only; keep `border-color` + `box-shadow` transitions so users still get visual feedback | review feedback M7 |
| D29 | **DashboardStatsRow "Riprova tutto" threshold** | Banner appears only when ≥3 of 4 hooks errored (not all 4). Avoids alarmist UX on single transient failure | review feedback M6 |
| D30 | **Atomic PR vs split** | Stay atomic single PR despite scope expansion. Mitigations: TDD ordering (§10.1), small isolated steps (each step independently revertible by step boundary), focus contrast measurement pre-merge | review feedback M8 |

---

**End of spec.** Implementation plan to follow in a separate `writing-plans` document.
