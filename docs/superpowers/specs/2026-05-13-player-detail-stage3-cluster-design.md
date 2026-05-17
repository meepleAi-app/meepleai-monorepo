# player-detail Stage 3 cluster — design

| Field | Value |
|---|---|
| Status | accepted |
| Date | 2026-05-13 |
| Author | brainstorming session (user + assistant) |
| Parent | issue #1113 (player-detail Stage 3 cluster) |
| Grandparent | issue #1026 (Stage 3 conformity fixes), umbrella #1023 (De-versioning) |
| Composer dependency | PR #1112 — `DetailPageLayout` primitive (merged 2026-05-13) |
| Mockup SoT | `admin-mockups/design_files/sp4-player-detail.jsx` |
| Branch | `feature/issue-1113-stage3-player-detail-cluster` (from `main-dev`) |

## 1. Problem

The `/players/[id]` route currently uses a flat-scroll orchestrator (`PlayerDetailView.tsx`) that stacks `PlayerHero`, `PlayerStatsGrid`, `PlayerLeaderboardCard`, `FavoriteAgentCard`, `AchievementBadgeGrid` in a single `<div>`. The mockup `sp4-player-detail.jsx` instead specifies a tabbed layout with a 6-pip ConnectionBar between hero and tabs. Per parent spec §1.1 decision 4 ("canonical = mockup-faithful"), the mockup wins.

This cluster refactors the orchestrator to adopt the `DetailPageLayout` composer (merged via PR #1112) and brings in the missing pieces: an overview region for the stats components, a 6-pip ConnectionBar wired to `PlayerStatistics`, and a 4-tab `Tabs` configuration (`Sessions` / `Games` / `Toolkits` / `Achievements`). The 5 Wave 3 component primitives (`PlayerHero`, `PlayerStatsGrid`, `PlayerLeaderboardCard`, `FavoriteAgentCard`, `AchievementBadgeGrid`) are consumed unchanged. Subroutes `/players/[id]/{stats,sessions,games,achievements}` remain unchanged (Wave 3 contract preservation per parent spec NG2).

## 2. Goals & Non-goals

### Goals

- G1 — Wrap `PlayerDetailView` in `<DetailPageLayout>` from the composer primitive shipped by PR #1112.
- G2 — Add `PlayerOverviewRegion` composition (Stats + Leaderboard + FavoriteAgent in a 3-card layout) inside the `hero` slot, alongside the existing `<PlayerHero>`.
- G3 — Add `PlayerConnectionBar` wrapper that translates `PlayerStatistics` data into 6 `ConnectionItem` entries matching the mockup's pip layout.
- G4 — Add `PlayerTabs` wrapper with 4 descriptors (`sessions`, `games`, `toolkits`, `achievements`) and ARIA tablist semantics inherited from the canonical `Tabs` primitive.
- G5 — Track active tab via `?tab=<key>` URL search param with graceful fallback to default (`sessions`) on missing/invalid values.
- G6 — Pin visual conformity vs `sp4-player-detail.html` mockup at viewports 375×667 and 1280×800 via the existing `visual-regression-conformity.yml` workflow with ≤ 2% threshold.
- G7 — Preserve all 4 existing FSM shells (`loading`, `error`, `not-found`, `default`) in `PlayerDetailView`; only the `default` shell's render body is restructured.
- G8 — Update parent spec §4 composability table: change `/players/[id]` row from "— (flat)" to the actual 4-tab list.

### Non-goals

- NG1 — Do NOT modify any Wave 3 component (`PlayerHero`, `PlayerStatsGrid`, `PlayerLeaderboardCard`, `FavoriteAgentCard`, `AchievementBadgeGrid`). They are consumed unchanged.
- NG2 — Do NOT modify or deprecate subroutes `/players/[id]/{stats,sessions,games,achievements}`. Wave 3 contract preservation per parent spec NG2.
- NG3 — Do NOT implement the guest variant. Mockup shows a guest gating pattern (locked tabs, smaller achievements limit); current FE assumes authenticated user. Guest support is a separate PR scope.
- NG4 — Do NOT extend `PlayerStatistics` schema for event/agent/toolkit/chat counts. Pips 3-6 of the ConnectionBar render with `isEmpty: true` (dashed border + plus icon, the canonical empty-pip rendering already supported by `ConnectionBar` primitive). A follow-up issue tracks the backend extension.
- NG5 — Do NOT add new API endpoints. Cluster is frontend-only per parent spec stage 3 scope.
- NG6 — Do NOT wire ConnectionBar pip click navigation. The `onPipClick` callback stays unset until the backing data lands.
- NG7 — Do NOT implement Stats as a dedicated tab. The mockup shows stats in an overview region between Hero and Tabs (not as a tab itself). The 4 tabs match the mockup exactly.

## 3. Decisions taken during brainstorming

| Question | Decision | Rationale |
|---|---|---|
| Q1 — Tab list mockup vs initial issue scope | Mockup wins (Sessions / Games / Toolkits / Achievements; no Stats tab) | Parent spec §1.1 decision 4: canonical = mockup-faithful |
| Q2 — Stats components placement | B — `PlayerOverviewRegion` between Hero and Tabs, passed via `hero` slot fragment | Mockup-faithful (stats always visible, not gated by tab); zero new primitives |
| Q3 — ConnectionBar 6-pip data sources | B — 2 real (game, session from `PlayerStatistics`) + 4 isEmpty placeholders | Mockup visually faithful, scope contained, primitive already supports `isEmpty` rendering |
| Q4 — Tab state management | B — `?tab=<key>` URL search param | Shareable links + back/forward; pattern already used in `PlayerDetailView` for `?state=…` override |
| Q5 — Subroutes coexistence | A — leave unchanged | Wave 3 contract preservation; parent NG2 |
| Q6 — Visual conformity strategy | A — Playwright vs mockup HTML via existing workflow | Reuses `visual-regression-conformity.yml` (post #1086); aligned with WS-D state matrix (#1070) |

## 4. API

No new public components beyond the wrappers in `apps/web/src/app/(authenticated)/players/[id]/_components/`. Wrappers are route-private (not exported from a barrel).

### 4.1 PlayerOverviewRegion

```tsx
interface PlayerOverviewRegionProps {
  readonly stats: PlayerProfile;          // existing shape from mapStatsToProfile
  readonly playerId: string;              // for AchievementBadgeGrid viewAllHref
  readonly statsLabels: PlayerStatsGridLabels;
  readonly leaderboardLabels: PlayerLeaderboardCardLabels;
  readonly favoriteAgentLabels: FavoriteAgentCardLabels;
  readonly onFavoriteAgentClick?: () => void;
}
```

Renders the 3 Wave 3 cards in a responsive grid:
- Mobile (< sm): vertically stacked
- Tablet+ (sm+): `PlayerStatsGrid` full-width row, `PlayerLeaderboardCard` + `FavoriteAgentCard` 2-col row

### 4.2 PlayerConnectionBar

```tsx
interface PlayerConnectionBarProps {
  readonly stats: PlayerProfile;
  readonly labels: PlayerConnectionBarLabels;
}

interface PlayerConnectionBarLabels {
  readonly topGames: string;
  readonly sessions: string;
  readonly gameNights: string;
  readonly agents: string;
  readonly toolkits: string;
  readonly chats: string;
}
```

Computes 6 `ConnectionItem` entries; pips 3-6 render with `isEmpty: true` until backend exposes the counts.

### 4.3 PlayerTabs

```tsx
type PlayerTabKey = 'sessions' | 'games' | 'toolkits' | 'achievements';

interface PlayerTabsProps {
  readonly activeTab: PlayerTabKey;
  readonly onChange: (next: PlayerTabKey) => void;
  readonly counts: Record<PlayerTabKey, number>;
  readonly labels: PlayerTabsLabels;
}

interface PlayerTabsLabels {
  readonly sessions: string;
  readonly games: string;
  readonly toolkits: string;
  readonly achievements: string;
}
```

Thin wrapper over the canonical `Tabs` primitive. ARIA tablist semantics inherited.

### 4.4 Tab panels

Three new tab panel components and one reuse:
- `SessionsTabPanel(profile)` — renders `gamePlayCounts` ranked summary + CTA "View all" → `/players/[id]/sessions`
- `GamesTabPanel(profile)` — renders `gamePlayCounts` as MeepleCard grid + CTA → `/players/[id]/games`
- `ToolkitsTabPanel()` — single `EmptyState` placeholder pending backend (follow-up tracked)
- `AchievementBadgeGrid` (existing Wave 3 component) — reused as-is for the `achievements` tab content

## 5. Routing & state

Tab state:

```ts
const VALID_TABS = ['sessions', 'games', 'toolkits', 'achievements'] as const;
type PlayerTabKey = (typeof VALID_TABS)[number];
const DEFAULT_TAB: PlayerTabKey = 'sessions';

function readTabFromUrl(searchParams: ReadonlyURLSearchParams | null): PlayerTabKey {
  const raw = searchParams?.get('tab');
  return (VALID_TABS as readonly string[]).includes(raw ?? '') ? (raw as PlayerTabKey) : DEFAULT_TAB;
}

function writeTabToUrl(next: PlayerTabKey, playerId: string, searchParams: ReadonlyURLSearchParams | null, router: AppRouterInstance): void {
  const params = new URLSearchParams(searchParams ?? '');
  if (next === DEFAULT_TAB) {
    params.delete('tab');
  } else {
    params.set('tab', next);
  }
  const qs = params.toString();
  router.replace(`/players/${playerId}${qs ? `?${qs}` : ''}`, { scroll: false });
}
```

Notes:
- Invalid `?tab=foo` falls back silently to `DEFAULT_TAB` — no error state
- Default tab omits the query param entirely (cleaner URLs)
- `router.replace` (not `push`) — tab switches don't pollute history
- `{ scroll: false }` preserves scroll position across tab switches
- Coexistence with the existing `?state=…` Wave 3 debug override is preserved by mutating the existing `URLSearchParams`

## 6. DOM composition

```tsx
<DetailPageLayout
  hero={
    <>
      <PlayerHero {...heroProps} />
      <PlayerOverviewRegion {...overviewProps} />
    </>
  }
  connections={<PlayerConnectionBar stats={profile} labels={connectionLabels} />}
  tabs={<PlayerTabs activeTab={tab} onChange={onTabChange} counts={tabCounts} labels={tabLabels} />}
>
  {renderActiveTabPanel(tab, profile)}
</DetailPageLayout>
```

`PlayerHero` keeps its existing onBack callback. The fragment in the `hero` slot is intentional — the composer accepts `ReactNode`, so passing two siblings via `<>...</>` is supported.

## 7. Visual conformity

New test: `apps/web/e2e/visual-conformity/player-detail.spec.ts`.

Pattern (mirrors existing visual-conformity specs added post #1086):

```ts
import { test, expect } from '@playwright/test';
import { applyStateOverride } from './helpers/state-override';

const ROUTE = '/players/p-test-fixture';

for (const tab of ['sessions', 'games', 'toolkits', 'achievements'] as const) {
  test(`${tab} tab matches sp4-player-detail mockup`, async ({ page }) => {
    await applyStateOverride(page, { route: ROUTE, state: 'default' });
    await page.goto(`${ROUTE}?tab=${tab}`);
    await expect(page).toHaveScreenshot(`player-detail-${tab}.png`, {
      maxDiffPixelRatio: 0.02,
    });
  });
}
```

Two viewports configured at the Playwright project level (375×667 mobile + 1280×800 desktop). The `bootstrap-mockup-baselines.yml` workflow already generates the mockup screenshots from `sp4-player-detail.html`.

State coverage matrix (Issue #1070): add a row for `sp4-player-detail` with states `[default, tab-sessions, tab-games, tab-toolkits, tab-achievements, loading, error, empty, not-found]`. Implementation detail of the matrix file format follows WS-D conventions.

## 8. Test plan

### Vitest unit

| File | Tests |
|---|---|
| `__tests__/PlayerOverviewRegion.test.tsx` | Render: stats grid + leaderboard + favorite-agent present. Layout: 3-card on desktop, stacked on mobile (snapshot DOM structure). Onclick callback passthrough on favorite agent. |
| `__tests__/PlayerConnectionBar.test.tsx` | 6 pips rendered. game/session pips show real counts. event/agent/toolkit/chat pips render with `isEmpty=true` (dashed border verified via class). All 6 pips have correct aria-labels. |
| `__tests__/PlayerTabs.test.tsx` | 4 tabs rendered with labels + counts. Active tab marked with `aria-selected="true"`. `onChange(key)` fires on click. Keyboard navigation (left/right arrow keys) inherited from `Tabs` primitive (smoke test). |
| `__tests__/PlayerDetailView.test.tsx` (extend) | New tests: (a) `?tab=games` URL → games tab active, (b) `?tab=` missing → default sessions tab, (c) `?tab=bogus` → fallback to sessions, (d) `onChange(toolkits)` writes `?tab=toolkits` and `router.replace` called, (e) `onChange(sessions)` deletes the `tab` param (back to default URL), (f) `DetailPageLayout` is the outermost wrapper — assert via `container.querySelector('[data-slot="detail-page-layout"]')` is not null, (g) FSM shells (loading/error/not-found) preserved — existing Wave 3 tests must still pass. |

Coverage target: ≥ 85% per new files; 0 regression on existing Wave 3 `__tests__/` (`PlayerHero.test.tsx`, etc).

### Playwright visual

See §7 — 4 tabs × 2 viewports = 8 screenshot snapshots.

## 9. Files touched

| File | Action |
|---|---|
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx` | UPDATED — wrap in `DetailPageLayout`, add tab state from URL, replace flat layout with composer slots |
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerOverviewRegion.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerConnectionBar.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerTabs.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/SessionsTabPanel.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/GamesTabPanel.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/ToolkitsTabPanel.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerOverviewRegion.test.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerConnectionBar.test.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerTabs.test.tsx` | NEW |
| `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerDetailView.test.tsx` | UPDATED — extended |
| `apps/web/e2e/visual-conformity/player-detail.spec.ts` | NEW |
| `apps/web/e2e/state-coverage/state-matrix.json` | UPDATED — add `sp4-player-detail` entry |
| `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` | UPDATED — §4 table `/players/[id]` row |

12 new files + 2 updated.

## 10. Acceptance criteria

- AC1 — `PlayerDetailView.tsx` `default` shell renders `<DetailPageLayout>` as the outermost element (replaces `<div data-slot="player-detail-view">`); root carries the composer's canonical `data-slot="detail-page-layout"` attribute (set by the primitive itself per PR #1112).
- AC2 — `PlayerConnectionBar` renders 6 pips: 2 with real counts (game = `gamePlayCounts.length`, session = `totalSessions`) + 4 with `isEmpty: true`.
- AC3 — `PlayerTabs` renders 4 tab descriptors matching the mockup label list (Partite / Giochi / Toolkit / Achievement in `it` locale); ARIA `role="tablist"` + `aria-selected` on active tab.
- AC4 — Active tab driven by `?tab=<key>` URL param; invalid/missing falls back to `sessions`; default tab omits the param.
- AC5 — All 4 existing FSM shells (`loading`, `error`, `not-found`, `default`) preserved; only `default` shell body restructured.
- AC6 — Existing Wave 3 component tests pass without modification (zero diff under `apps/web/src/components/features/player-detail/`).
- AC7 — New Vitest tests in §8 present and passing; Vitest coverage ≥ 85% on each new file.
- AC8 — Playwright visual diff ≤ 2% vs `sp4-player-detail` baseline at viewports 375×667 and 1280×800 for all 4 tabs.
- AC9 — `pnpm typecheck && pnpm lint && pnpm test --run` green in `apps/web`; no regression in other consumers of `DetailPageLayout` (none exist yet, but the test suite must remain green).
- AC10 — Parent spec `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` §4 composability table updated: `/players/[id]` row replaces "— (flat)" with "Sessions/Games/Toolkits/Achievements".
- AC11 — State coverage matrix (`apps/web/e2e/state-coverage/state-matrix.json`) includes `sp4-player-detail` row with the 9 states listed in §7.

## 11. Rollback

Revert this single PR. The 5 Wave 3 component primitives are untouched; subroutes `/players/[id]/{stats,sessions,games,achievements}` are untouched. The composer primitive (`DetailPageLayout`) keeps zero consumers post-revert, no other Stage 3 cluster work blocked.

## 12. Follow-ups (out of scope)

- Backend schema extension on `PlayerStatistics` to expose event/agent/toolkit/chat counts → unblocks pips 3-6 from `isEmpty` to real data. Open as separate cross-BoundedContext issue.
- Wire ConnectionBar `onPipClick` to navigation routes (`/agents`, `/game-nights/[id]`, etc) once data lands.
- Guest variant support (mockup gates `games` + `toolkits` tabs for unauthenticated; current FE assumes authenticated).
- Subroutes deprecation: `/players/[id]/games` etc. duplicate the same content as `?tab=games`. Open a separate issue for cleanup once usage analytics confirm tabs are the primary entry point.
- Toolkits tab content — currently a placeholder empty state. Needs full implementation when toolkit-detail cluster (parent spec §3 cluster #2) lands and exposes per-player toolkit data.

## 13. References

- Composer primitive: `docs/for-developers/specs/2026-05-13-detail-page-layout-design.md` + PR #1112
- Parent spec: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` §3 Stage 3, §4 composability
- Mockup: `admin-mockups/design_files/sp4-player-detail.jsx` + `sp4-player-detail.html`
- Wave 3 component family: PR #724 — `apps/web/src/components/features/player-detail/`
- Existing orchestrator: `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx`
- Visual regression workflow: `.github/workflows/visual-regression-conformity.yml`
- State coverage workflow: `.github/workflows/state-coverage-check.yml` (Issue #1070)
- Source issue: #1113
