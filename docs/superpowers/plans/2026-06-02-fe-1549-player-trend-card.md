# FE #1549 — PlayerTrendCard SVG line chart (#1485 follow-up F3)

> **Status:** SHIPPED — implemented + tested + lint/typecheck pass.

**Goal:** Implement `PlayerTrendCard` as a pure presentational component with inline SVG line + area chart showing the player's win-rate trend over the last 6 ISO months. Wire into `PlayerOverviewRegion` below `PlayerTopGamesCard`. Consumes `PlayerStatistics.WinRateTrend` (shipped by Step C, BE bundle PR #1797). Closes #1549.

**Mockup source:** `admin-mockups/design_files/sp4-player-detail.jsx:594-636` (function `TrendCard`).

---

## Architecture

- **Pure presentational SRP** — no hooks, no chart library dep, no `useMemo`. All i18n strings injected via `labels`. Mirror sibling pattern from `PlayerTopGamesCard` (sessione 22) and `PlayerLeaderboardCard`.
- **Inline SVG** with `viewBox="0 0 280 100"` + `preserveAspectRatio="none"` (mockup parity). Gradient area + polyline + data-point circles. SVG is `aria-hidden="true"` (decorative); a `sr-only` summary span exposes the trend numerically to assistive tech.
- **Delta badge** computes signed integer percentage from first→last winRate; renders ↗/↘/→ glyph + colored class. Aria label uses absolute value with directional template.
- **Empty state** when `points.length < 2` — single point can't form a line.
- **Color**: `text-violet-700 dark:text-violet-400` for line + area gradient (via `currentColor` + `stop-opacity`); `text-emerald-700` for positive delta, `text-rose-700` for negative, `text-muted-foreground` for flat.

---

## Locked Decisions

| # | Decision | Rationale |
|---|---|---|
| **DEC-1** | Inline SVG (no `recharts`/chart library) | Fowler — bundle weight; mockup uses 280×100 viewbox handcrafted; future tooltips out of scope |
| **DEC-2** | `MonthlyWinRatePoint` owned by component (production input contract); fixture re-exports | Fowler — single source of truth, mirror DEC-2 of #1546 (M1 fix pattern) |
| **DEC-3** | SVG dimensions are constants (`SVG_VIEWBOX_WIDTH=280`, `SVG_PADDING_Y=8`) — top/bottom padding so circles don't clip viewbox | Crispin — testable invariants, predictable layout |
| **DEC-4** | Delta direction: `up` when last − first > 0, `down` when < 0, `flat` when == 0 (rounded to integer percent). 1 point → no delta badge | Wiegers — explicit FSM, no ambiguity |
| **DEC-5** | `deltaUp` template includes the leading `+` ("↗ +{percent}%"); `deltaDown` template uses signed token ("↘ {percent}%" with leading minus) | Adzic — two explicit templates, no string concat conditional |
| **DEC-6** | Delta accessible name uses absolute value (`deltaUpAriaLabel` says "+5%", `deltaDownAriaLabel` says "down by 5%") — no double-negative | Cockburn — natural-language phrasing |
| **DEC-7** | `monthsShort` is a 12-element array indexed by ISO month - 1 (e.g. monthsShort[0] = "Jan") — passed via labels | Adzic — i18n parity en+it explicit |
| **DEC-8** | Axis labels rendered as `<span>` per data point (1:1 with points); fallback to raw `YYYY-MM` when month parse fails | Wiegers — defensive against malformed input |
| **DEC-9** | Empty state uses `role="status"` for polite AT announcement | Crispin — mirror PlayerTopGamesCard DEC-5 |
| **DEC-10** | `useId` not used for gradient id — derive deterministic id from `points.length + first y` to avoid id collision when multiple cards render | Fowler — zero React dependency added, sufficient uniqueness for current usage |
| **DEC-11** | Trend summary sr-only template: "Win rate trend from {from}% to {to}% over {count} months" — single sentence with first/last values | Adzic — AT users get the trend gist in one breath |

## G/W/T Scenarios (T1-T6)

- **T1**: G: 3 points 0.4/0.5/0.6 / W: render / T: "↗ +20%" badge, SVG and summary present
- **T2**: G: 3 points 0.7/0.5/0.3 / W: render / T: "↘ -40%" badge, sr-only "down by 40%"
- **T3**: G: 1 point / W: render / T: empty state with `role="status"`, no SVG/summary/delta
- **T4**: G: 2 equal points / W: render / T: "→ 0%" badge, sr-only "Win rate unchanged"
- **T5**: G: 3 points months 01/02/03 / W: render / T: axis shows "Gen Feb Mar" (i18n it)
- **T6**: axe scan populated + empty → no violations; SVG aria-hidden true; sr-only summary "40% to 60% over 3 months"

---

## File Structure

| Path | Responsibility | Type |
|------|---------------|------|
| `apps/web/src/components/features/player-detail/PlayerTrendCard.tsx` | Pure SVG line chart component + `MonthlyWinRatePoint` type + delta computation | Create |
| `apps/web/src/components/features/player-detail/__tests__/PlayerTrendCard.test.tsx` | 6 unit tests (T1-T6) | Create |
| `apps/web/src/components/features/player-detail/index.ts` | Barrel export (component + 3 types) | Modify |
| `apps/web/src/lib/player-detail/player-detail-visual-test-fixture.ts` | Re-export `MonthlyWinRatePoint` from component; add `trendPoints` field to `PlayerProfileFixture`; populate `FIXTURE_DEFAULT.trendPoints` | Modify |
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx` | Add `trendLabels` useMemo (12 month labels + 8 strings); extend `mapStatsToProfile` to copy `stats.winRateTrend → trendPoints`; pass through `overviewLabels.trend` | Modify |
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerOverviewRegion.tsx` | Extend labels type + render `<PlayerTrendCard>` after `<PlayerTopGamesCard>` | Modify |
| `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerOverviewRegion.test.tsx` | Add `trendPoints` to fixture profile + `trend` labels object + assertion for new slot | Modify |
| `apps/web/src/locales/en.json` | Add `pages.playerDetail.sections.trend.*` (8 strings + 12 monthsShort) | Modify |
| `apps/web/src/locales/it.json` | Add `pages.playerDetail.sections.trend.*` (same shape, IT translations) | Modify |
| `docs/superpowers/plans/2026-06-02-fe-1549-player-trend-card.md` | This plan doc | Create |

**Test commands:**
- `cd apps/web && pnpm vitest run src/components/features/player-detail/__tests__/PlayerTrendCard.test.tsx`
- `cd apps/web && pnpm vitest run src/lib/player-detail src/components/features/player-detail src/app/\(authenticated\)/players/\[id\]/_components/__tests__`
- `cd apps/web && pnpm typecheck`
- `cd apps/web && pnpm lint`

---

## Verification

- ✅ `pnpm vitest run` PlayerTrendCard.test → **6/6 pass** (170ms)
- ✅ `pnpm vitest run` all player-detail tests → **104/104 pass** (incl. PlayerTopGamesCard 7 + new PlayerTrendCard 6 + PlayerOverviewRegion 3 + PlayerDetailView 24 + fixture 6 + state 12 + 9 sibling test files)
- ✅ `pnpm typecheck` → 0 errors
- ✅ `pnpm lint` → 0 errors (6 pre-existing warnings on unrelated files)

## Acceptance criteria

- [x] `PlayerTrendCard.tsx` created with props `{ points, labels, className? }`
- [x] Inline SVG line + area gradient (mockup style); axis labels; rendered as a card with `bg-card border-border rounded-2xl p-4`
- [x] Empty state when `points.length < 2` ("Not enough data to show a trend")
- [x] DS-15 compliant + jest-axe assertion
- [x] i18n keys `pages.playerDetail.sections.trend.{title, deltaUp, deltaDown, deltaFlat, deltaUpAriaLabel, deltaDownAriaLabel, deltaFlatAriaLabel, empty, trendSummaryAriaLabel, monthsShort.{jan..dec}}` added en+it
- [x] Barrel export + orchestrator wiring in `PlayerOverviewRegion` (after `PlayerTopGamesCard`)

## Out of scope

- Tooltip on hover (mockup doesn't include it; defer)
- Multi-line comparison chart (e.g. user vs avg) — single-line only
- Time range selector (always last 6 months per spec)

## Cluster #1485 player-detail follow-up status

- ✅ #1546 PlayerTopGamesCard (sessione 22)
- ✅ #1547 a11y ErrorShell+NotFoundShell (Step B PR #1796)
- ✅ #1540 + #1541 + #1550 BE bundle (Step C PR #1797)
- ✅ #1542 player achievements hook + wire (Step D PR #1798)
- ✅ **#1549 PlayerTrendCard FE Tier L (this PR, Step E)**

🎉 **Cluster #1485 player-detail follow-up: 5/5 done.**
