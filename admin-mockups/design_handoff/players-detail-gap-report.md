# Players Detail Gap Report — `/players/[id]` (sp4-player-detail.jsx ↔ Wave 3 PR #724 + audit-fix PR #1539)

> Conformity check applying the [`PILOT_GAP_REPORT.md`](./PILOT_GAP_REPORT.md) methodology to `/players/[id]`. Issue: [#1485](https://github.com/meepleAi-app/meepleai-monorepo/issues/1485).
> Date: 2026-05-26 · Branch: `feature/issue-1485-player-detail-audit` · Scope: read-only.
> Pre-audit baseline: PR [#1539](https://github.com/meepleAi-app/meepleai-monorepo/pull/1539) (2026-05-25) just landed 3 fixes for this route (`leaderboard.noRank` + `favoriteAgent.ariaLabel` + `stats.*` i18n keys; `PlayerDetailView` statsLabels copy-paste; `PlayerLeaderboardCard` aria-label on generic `<div>` ARIA 1.2 violation). Those are NOT re-flagged.

## TL;DR

`/players/[id]` is **already brownfield-shipped** via PR #724 (Wave 3) + cluster migration PR #1053 (Stage 3 de-versioning) + audit-fix PR #1539 (this batch). The conformity check finds **near-zero drift**: 2 nice-to-have gaps + 1 a11y polish item.

| Status | Count | Components |
|---|---|---|
| ✅ Implemented + exposed via barrel | 5 | `PlayerHero`, `PlayerStatsGrid`, `PlayerLeaderboardCard`, `FavoriteAgentCard`, `AchievementBadgeGrid` |
| 🟢 Implemented as orchestrator/region (not in feature barrel by design) | 7 | `PlayerDetailView`, `PlayerOverviewRegion`, `PlayerTabs`, `PlayerConnectionBar`, `SessionsTabPanel`, `GamesTabPanel`, `ToolkitsTabPanel` |
| ❌ Missing as standalone | 2 | `PlayerTopGamesCard`, `PlayerTrendCard` |
| ⚠️ Deferred by design (variant / subroute / layout delegation) | 5 | Guest variant rendering, `Confronta`/`Modifica`/`Share` CTAs, `/players/[id]/{games,sessions,toolkits,achievements}` sub-route content, `DesktopNav`/`PhoneTopNav` (covered by `DetailPageLayout`) |
| 🔎 To verify / a11y polish | 1 | `aria-live` announce on FSM state transitions (loading/error) |

→ **Drift ratio**: 2 ÷ 20 mockup sections ≈ **10%** (vs the pilot's 37.5% on `/games/[id]`). Excellent alignment.

→ **Effort residuo total**: ~XS + S + L = ~3-4h follow-up across 3 atomic PRs (or defer all to wave 2 if nice-to-have).

## Convenzioni

| Simbolo | Significato |
|---|---|
| ✅ | Implementato + esposto via barrel `index.ts` |
| 🟢 | Implementato (orchestrator / region / panel) — non esposto via barrel by-design |
| ❌ | Mancante — da creare |
| ⚠️ | Esiste in altro path / variant / layout — defer by design |
| 🔎 | Da verificare / a11y polish |

## 1. Mapping componenti: mockup ↔ codebase

### 1.1. Sezioni produzione del mockup (`sp4-player-detail.jsx`)

| # | Mockup name | Lines | Role | Status codebase | Path canonical |
|---|---|---|---|---|---|
| 1 | `EntityChip` | 116–134 | Reusable entity badge | 🟢 (inline util pattern) | inline in shipped components |
| 2 | `ConnectionBar` | 140–200 | Sticky pip row (6 entity connectors) | ✅ | `_components/PlayerConnectionBar.tsx` |
| 3 | `PlayerHero` | 206–404 | Avatar + name + stat chips + action bar | ✅ | `features/player-detail/PlayerHero.tsx` |
| 4 | `StatInline` | 406–419 | Stat label+value pair | 🟢 | inline in `PlayerHero` |
| 5 | `PlayerTabs` | 424–489 | Animated underline tablist (5 tabs) | ✅ | `_components/PlayerTabs.tsx` (4 tabs — Overview merged into hero) |
| 6 | `KpiCard` | 494–523 | Stats tile | 🟢 | inline `KpiTile` in `PlayerStatsGrid.tsx` |
| 7 | `TopGamesCard` | 528–592 | Ranked top-5 games + win rate per game | ❌ MANCANTE | _da creare_ → `features/player-detail/PlayerTopGamesCard.tsx` |
| 8 | `TrendCard` | 594–636 | 6-month win-rate trend (SVG line + gradient) | ❌ MANCANTE | _da creare_ → `features/player-detail/PlayerTrendCard.tsx` |
| 9 | `FavoriteAgentCard` | 638–673 | Agent cross-ref card | ✅ | `features/player-detail/FavoriteAgentCard.tsx` |
| 10 | `OverviewTab` | 675–703 | Composition wrapper | 🟢 | `_components/PlayerOverviewRegion.tsx` (moved to hero slot, not a tab) |
| 11 | `OverviewEmpty` | 705–735 | Empty state per Overview | 🟢 | empty-state branches in `PlayerDetailView` FSM |
| 12 | `SessionItem` | 740–806 | Session card row | ⚠️ | sub-route `/players/[id]/sessions` content (deferred wave 2) |
| 13 | `SessionsTab` | 808–834 | Filter chips + session list | 🟢 | `_components/SessionsTabPanel.tsx` (MVP stub) |
| 14 | `SessionsEmpty` | 836–863 | Empty state per Sessions | 🟢 | orchestrator branches |
| 15 | `GameCompactCard` | 868–907 | Game card grid item | ⚠️ | sub-route `/players/[id]/games` content (deferred) |
| 16 | `GamesTab` | 909–922 | Games grid + count | 🟢 | `_components/GamesTabPanel.tsx` (MVP stub) |
| 17 | `ToolkitItem` | 927–972 | Toolkit row | ⚠️ | sub-route `/players/[id]/toolkits` (deferred) |
| 18 | `ToolkitsTab` | 974–993 | Toolkit list + "new toolkit" CTA | 🟢 | `_components/ToolkitsTabPanel.tsx` (MVP stub) |
| 19 | `AchievementBadge` | 998–1033 | Single achievement badge | 🟢 | rendered as placeholder tiles in `AchievementBadgeGrid` |
| 20 | `AchievementsTab` | 1035–1068 | Progress bar + badge grid + filter | 🟢 (placeholder) | `features/player-detail/AchievementBadgeGrid.tsx` |

### 1.2. Variant/state mockup → codebase

| Variant | Mockup | Codebase | Status |
|---|---|---|---|
| Default (registered player) | ✅ | `PlayerDetailView` `'default'` branch (lines 368–522) | ✅ Match |
| Guest player (👤 emoji, locked tabs) | ✅ (`fixture.guest === true`) | _Not supported v1_ (BE `usePlayerStatistics` is current-user only) | ⚠️ |
| Loading skeleton | ✅ | `PlayerDetailLoadingSkeleton()` lines 147–172 (has `aria-busy` + `aria-live="polite"` ✅) | ✅ Match |
| Error shell | ✅ | `ErrorShell()` lines 176–207 (title + subtitle + retry CTA) | ✅ Match |
| Not-found shell | ✅ | `NotFoundShell()` lines 211–239 (👤 icon + back link) | ✅ Match |
| Empty (zero sessions) | ✅ (`OverviewEmpty` mockup component) | _Implicit_: KPI tiles render `0`, no dedicated empty hero | 🔎 |

### 1.3. Demo artifacts (NOT to flag)

`DesktopFrame`, `MobileScreen`, `PhoneSbar`, `PhoneTopNav`, `DesktopNav`, `PhoneShell`, root `App` — fixture harness only, not production. The shipped FE uses `DetailPageLayout` shared wrapper (same as game-detail) for nav + frame.

## 2. Gap details (componenti da creare)

### 2.1. `PlayerTopGamesCard` ❌

| Aspetto | Valore |
|---|---|
| Path canonical | `apps/web/src/components/features/player-detail/PlayerTopGamesCard.tsx` |
| Mockup source | `sp4-player-detail.jsx:528–592` (function `TopGamesCard`) |
| Shape props (proposed) | `{ items: ReadonlyArray<{ gameId, gameName, playCount, winRate, coverImageUrl? }>, maxItems?: number = 5, labels: { title, winRateLabel, playsLabel, empty }, className? }` |
| Data source | Derive from `PlayerStatistics.gamePlayCounts` + `averageScoresByGame` (already in hook); top-N by playCount; winRate = wins-per-game / plays (currently BE returns avg score not wins-per-game — see § 4) |
| Visual | Card `bg-card border-border rounded-lg p-4`, ranked rows (rank chip + game name + win-rate progress + plays count) |
| Effort | **S** (~45 min) — pure presentational, derives from existing hook data |
| Test path | `apps/web/src/components/features/player-detail/__tests__/PlayerTopGamesCard.test.tsx` |
| Wiring | `PlayerOverviewRegion` adds the card under the stats grid (mockup OverviewTab line ~690) |
| DS-15 compliance | `bg-card`/`border-border`/`text-foreground`/`text-muted-foreground` + `bg-entity-game` accent for rank chip |
| Schema gap | BE `gamePlayCounts` is `Record<gameName, number>`; for win-rate-per-game the BE needs a per-game wins field. **Defer**: render placeholder rate or open BE follow-up. |

### 2.2. `PlayerTrendCard` ❌

| Aspetto | Valore |
|---|---|
| Path canonical | `apps/web/src/components/features/player-detail/PlayerTrendCard.tsx` |
| Mockup source | `sp4-player-detail.jsx:594–636` (function `TrendCard`) |
| Shape props (proposed) | `{ points: ReadonlyArray<{ month: string, winRate: number }>, labels: { title, axisX, axisY, empty }, className? }` |
| Visual | Card with inline SVG line chart + gradient fill, 6-month sliding window, axis labels |
| Effort | **L** (~3-4h) — requires (a) BE endpoint exposing `winRateTrend` time-series, (b) chart rendering (inline SVG or `recharts` dep — TBD), (c) graceful empty state for new users with < 2 months data |
| Test path | `apps/web/src/components/features/player-detail/__tests__/PlayerTrendCard.test.tsx` |
| Wiring | `PlayerOverviewRegion` adds the card under `PlayerTopGamesCard` |
| Schema gap | BE has no `winRateTrend` field in `PlayerStatistics`. **Blocks FE impl** until BE follow-up lands. |

### 2.3. A11y polish — `aria-live` on FSM state transitions 🔎

| Aspetto | Valore |
|---|---|
| File | `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx` |
| Current state | `PlayerDetailLoadingSkeleton` HAS `aria-busy="true"` + `aria-live="polite"` (line ~151) ✅. `ErrorShell` and `NotFoundShell` do NOT announce their state change to AT — silent transition from skeleton → error/not-found. |
| Mockup behavior | TabError component (`mockup:1083-1110`) is visually distinct but has no AT cues in fixture |
| Effort | **XS** (~20 min) — add `role="status"` + `aria-live="polite"` to `ErrorShell` and `NotFoundShell` containers |
| Test path | Extend `PlayerDetailView` integration tests (or add a smoke `derivePlayerDetailUiState` axe assertion per state) |
| Why "polish" not "bug" | Visual + textual indicators are clear; AT users currently get only the title h1. Adding announce is best-practice but not WCAG AA blocker. |

## 3. Deferred by design (NOT to spawn issues for)

These are intentional shipped-FE choices that diverge from the mockup but are **defer by design**:

| Item | Mockup | Shipped | Why deferred |
|---|---|---|---|
| **Overview tab** | Default tab `overview` | Overview content moved into hero slot via `PlayerOverviewRegion` (after `PlayerHero`); `PLAYER_TAB_KEYS = ['sessions','games','toolkits','achievements']` | Sensible UX refinement (stats visible immediately without tab nav); not a regression |
| **Sub-routes content** | `SessionItem` / `GameCompactCard` / `ToolkitItem` full list views | MVP stubs in `*TabPanel.tsx` linking out to `/players/[id]/{sessions,games,toolkits}` | Sub-route impl tracked in wave 2 backlog (not currently in epic #1475 scope) |
| **Guest variant** | 👤 emoji + locked tabs + "+ Invita su MeepleAI" CTA | Not rendered (no guest BE API) | v1 schema is single-user only; Issue #1478 explicitly out-of-scope |
| **`Confronta` / `Modifica` / `Share` CTAs** | Demo-nav buttons in hero | Not in `PlayerHero` props | v2+ feature: requires Compare flow + Edit profile route + share infra |
| **`DesktopNav` / `PhoneTopNav`** | Breadcrumb + back button | `DetailPageLayout` shared wrapper supplies the nav | Layout delegation, no per-route duplication |

## 4. Schema gaps already tracked (NOT to re-flag)

These were documented during the `#1478` audit-fix cycle and have follow-up BE issues:

| Field | Issue | Notes |
|---|---|---|
| `leaderboardRank` in `PlayerStatistics` | [#1540](https://github.com/meepleAi-app/meepleai-monorepo/issues/1540) | Currently null → `PlayerLeaderboardCard` shows "noRank" copy |
| `favoriteAgentName` in `PlayerStatistics` | [#1541](https://github.com/meepleAi-app/meepleai-monorepo/issues/1541) | Currently null → `FavoriteAgentCard` shows "none" copy |
| Player achievements list + Zod schema + hook | [#1542](https://github.com/meepleAi-app/meepleai-monorepo/issues/1542) | Currently `count: 0` → `AchievementBadgeGrid` shows placeholder tiles only |
| Per-game wins (for `PlayerTopGamesCard` win-rate column) | _New follow-up_ (proposed) | `gamePlayCounts` exists but no per-game wins; trended via averages would be incorrect |
| `winRateTrend` time-series (for `PlayerTrendCard`) | _New follow-up_ (proposed) | Blocks `PlayerTrendCard` impl |

## 5. Issue follow-up — proposed

To be opened after merge of this audit PR (or per user direction):

| # | Title | Body summary | Effort | Priority |
|---|---|---|---|---|
| F1 | `feat(player-detail): PlayerTopGamesCard component (#1485 follow-up)` | New presentational card showing ranked top-N games derived from `PlayerStatistics.gamePlayCounts`; wires into `PlayerOverviewRegion`. Per-game win-rate column blocked on BE follow-up F2. | S (~45 min) | P2 |
| F2 | `feat(api): expose per-game wins in PlayerStatistics (#1485 follow-up)` | Add `winsByGame: Record<gameName, number>` (or similar) to `PlayerStatistics` so `PlayerTopGamesCard` can compute true win-rate per game. | M (~2h BE) | P3 |
| F3 | `feat(player-detail): PlayerTrendCard with win-rate trend chart (#1485 follow-up)` | New presentational card with inline SVG win-rate trend; blocks on BE F4. | L (~3-4h) | P3 |
| F4 | `feat(api): expose winRateTrend time-series in PlayerStatistics (#1485 follow-up)` | Add `winRateTrend: Array<{ month: string, winRate: number }>` (last 6 months) to enable F3. | M (~2h BE) | P3 |
| F5 | `a11y(player-detail): aria-live announce on ErrorShell + NotFoundShell (#1485 follow-up)` | Add `role="status"` + `aria-live="polite"` to error/not-found shells so AT users get state-change announcement. | XS (~20 min) | P3 |

## 6. Conformity verdict

✅ **Production-ready** for `/players/[id]` MVP. The 5 shipped components + orchestrator FSM + 4 tab panels + 6 i18n locale entries cover the mockup faithfully. The 2 missing standalone cards (`PlayerTopGamesCard`, `PlayerTrendCard`) are presentational enhancements that depend on BE schema extensions; they do NOT block route closure. The structural refactors (Overview → hero slot, subroute deferrals) are intentional UX improvements over the mockup.

**Recommended next steps** (low priority, can defer):
1. F5 (XS, 20 min) — a11y polish, ship anytime
2. F1+F2 (S+M) — if win-rate insights become a roadmap priority
3. F3+F4 (L+M) — if win-rate trend visualization becomes a roadmap priority

---

**Generated by Claude Code (Opus 4.7) in read-only mode.** Issue #1485. PR with `audit_pr` column update + audit-report-final.md entry follows in the same PR as this report.
