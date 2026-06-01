# FE #1546 — PlayerTopGamesCard component (#1485 follow-up)

> **Status:** SHIPPED — implemented + tested + lint/typecheck pass in one pass.

**Goal:** Implement `PlayerTopGamesCard` as a pure presentational component derived from `PlayerStatistics.mostPlayedGames` (with `winByGame` join for win rate, #1663 Phase 2 fields). Wire into `PlayerOverviewRegion`. Closes #1546.

**Architecture:** Pure SRP component (no hooks); mapper extension in `PlayerDetailView.mapStatsToProfile` derives `topGames: ReadonlyArray<TopGameItem>`. Single-source-of-truth `TopGameItem` type lives in `player-detail-visual-test-fixture.ts` alongside `PlayerProfileFixture`; component re-exports it.

**Tech Stack:** Next.js 16 App Router + React 19 + TypeScript + Tailwind 4 + Vitest + jest-axe.

**DEC traceability:** DEC-1..DEC-9 locked via spec-panel critique 2026-06-02 (sessione 22). G/W/T S1-S6.

**Mockup source:** `admin-mockups/design_files/sp4-player-detail.jsx:528-592` (function `TopGamesCard`).

**Schema reality (key discovery vs gap report 2026-05-26):** `PlayerStatisticsSchema` post-#1663 Phase 2 exposes `mostPlayedGames: GamePlayCount[]` + `winByGame: GameWinStats[]` (both optional during BE rollout). The gap report's "Opt A graceful-hide winRate" is no longer needed when these fields are present — we use real data and only graceful-hide when the rollout hasn't reached the env.

---

## Locked Decisions

| # | Decision | Rationale |
|---|---|---|
| **DEC-1** | Data source priority: `mostPlayedGames` (BE pre-sorted, has gameId) → fallback `gamePlayCounts` (FE-sorted). `winByGame` JOIN by gameId, name fallback. | Wiegers — use real data; #1663 Phase 2 rollout-aware |
| **DEC-2** | `TopGameItem = { gameId: string \| null, gameName: string, playCount: number, winCount: number \| null }`. Single source of truth in `PlayerTopGamesCard.tsx` (production-owned input contract); fixture re-exports for fixture data. (Post-review fix M1: moved from fixture to component to avoid coupling production type to a test-fixture module's lifecycle.) | Fowler — SRP, type belongs to component contract |
| **DEC-3** | Pure presentational: NO hook in component, mapper in `PlayerDetailView.mapStatsToProfile`. Extend `PlayerProfileFixture.topGames` + `PlayerOverviewRegionLabels.topGames`. | Fowler — mirror sibling pattern (PlayerLeaderboardCard) |
| **DEC-4** | i18n keys `pages.playerDetail.sections.topGames.{title, playsLabel, playsLabelWithWins, winRateLabel, rankAriaLabel, empty}`. Two separate templates for plays (with/without wins) — no conditional string building. | Adzic — explicit templates |
| **DEC-5** | Visual mapping 1:1 mockup lines 528-592: rank-1 = 🏆 (aria-hidden), rank-2+ = `#N`; no cover image MVP (graceful no-show); subtitle mono 10px; winRate display 16px right + "WIN RATE" mono small caps. DS-15 tokens. | Cockburn — mockup fidelity |
| **DEC-6** | `maxItems` default 5, prop optional. Sort + slice FE-side defense-in-depth (even though BE may already sort). | Wiegers — testable invariant |
| **DEC-7** | NO viewAll button MVP (scope discipline #1546 body — issue mentions only `{title, winRateLabel, playsLabel, empty}` labels). NO cover image (deferred enhancement). | Fowler — scope discipline |
| **DEC-8** | i18n it.json + en.json locked MVP entrambi. | Adzic — DoD parity |
| **DEC-9** | winRate format: `{Math.round(won / played * 100)}%` when `played > 0 && winCount != null`, else badge hidden. **Policy clarification (post-review M2):** `winCount === null` ⇒ "no data, BE rollout pending" (badge hidden); `winCount === 0` ⇒ valid datum "played but never won" (badge renders "0%"). Test S3 covers null path; test S3b covers zero path. | Wiegers — disambiguates null vs zero |

## G/W/T Scenarios

- **S1**: G: items=`[{playCount:10,winCount:6},{playCount:8,winCount:3}]` / W: render / T: top item "10 partite · 6 vittorie" + "60%"
- **S2**: G: items=[] / W: render / T: empty state "Nessun gioco giocato ancora" with `role="status"`
- **S3**: G: items=`[{playCount:5,winCount:null}]` / W: render / T: "5 partite" subtitle, **NO winRate badge** (graceful hide)
- **S4**: G: items.length=10 / W: maxItems=5 (default), then maxItems=3 (override), then shuffled input / T: sort desc + slice respected in all cases
- **S5**: G: 3 items / W: render / T: rank-1 = 🏆 (aria-hidden), rank-2/3 = `#2`/`#3`, accessible names "Posizione N" sr-only
- **S6**: axe scan items + empty → no violations; "WIN RATE: 60%" exposed via sr-only

---

## File Structure

| Path | Responsibility | Type |
|------|---------------|------|
| `apps/web/src/lib/player-detail/player-detail-visual-test-fixture.ts` | Add `TopGameItem` interface + `topGames` field to `PlayerProfileFixture`; populate `FIXTURE_DEFAULT.topGames` (Wingspan-shaped) | Modify |
| `apps/web/src/components/features/player-detail/PlayerTopGamesCard.tsx` | Pure presentational component + props/labels/types + DS-15 visual + sort/slice/rank/winRate logic | Create |
| `apps/web/src/components/features/player-detail/__tests__/PlayerTopGamesCard.test.tsx` | 6 tests (S1-S6) mirror sibling pattern | Create |
| `apps/web/src/components/features/player-detail/index.ts` | Barrel export component + types | Modify |
| `apps/web/src/locales/en.json` | Add `pages.playerDetail.sections.topGames.*` (6 keys) | Modify |
| `apps/web/src/locales/it.json` | Add `pages.playerDetail.sections.topGames.*` (6 keys) | Modify |
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx` | Add `deriveTopGames` helper + extend `mapStatsToProfile` + build `topGamesLabels` + pass to `overviewLabels` | Modify |
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerOverviewRegion.tsx` | Extend `PlayerOverviewRegionLabels` with `topGames` + render `<PlayerTopGamesCard>` after the leaderboard/favoriteAgent grid | Modify |
| `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerOverviewRegion.test.tsx` | Update test fixtures with `topGames` field + add assertion for top-games slot | Modify |
| `docs/superpowers/plans/2026-06-02-fe-1546-player-top-games-card.md` | This plan doc | Create |

**Test commands:**
- Single: `cd apps/web && pnpm vitest run src/components/features/player-detail/__tests__/PlayerTopGamesCard.test.tsx`
- All player-detail: `cd apps/web && pnpm vitest run src/lib/player-detail src/components/features/player-detail src/app/\(authenticated\)/players/\[id\]/_components/__tests__`
- Typecheck: `cd apps/web && pnpm typecheck`
- Lint: `cd apps/web && pnpm lint`

---

## Tasks (executed in batch — Tier S, ~45 min direct impl)

| # | Task | Status |
|---|------|--------|
| **T1** | Extend `PlayerProfileFixture` with `topGames: ReadonlyArray<TopGameItem>` + export `TopGameItem` type | ✅ |
| **T2** | Extend `mapStatsToProfile` with `deriveTopGames` helper (mostPlayedGames priority + winByGame join + gamePlayCounts fallback) | ✅ |
| **T3** | Add i18n keys `pages.playerDetail.sections.topGames.*` en+it (6 keys each) | ✅ |
| **T4** | Create `PlayerTopGamesCard.tsx` pure component (props + labels + types + DS-15 visual) | ✅ |
| **T5** | Create `__tests__/PlayerTopGamesCard.test.tsx` 6 tests (S1-S6) | ✅ |
| **T6** | Export via barrel `index.ts` (component + 3 types) | ✅ |
| **T7** | Extend `PlayerOverviewRegion` (labels prop + render below grid) | ✅ |
| **T8** | Wire `PlayerDetailView` (build topGamesLabels + pass to overviewLabels) + fix existing `PlayerOverviewRegion.test` for new shape + plan doc + verify | ✅ |

---

## Verification

- ✅ `pnpm vitest run` PlayerTopGamesCard.test → **6/6 pass** (230ms)
- ✅ `pnpm vitest run` all player-detail tests → **93/93 pass** (incl. 3 PlayerOverviewRegion fixed + 20 PlayerDetailView untouched)
- ✅ `pnpm lint` → 0 errors, 6 pre-existing warnings on other files (kb-hub/settings/library)
- ✅ `pnpm typecheck` → 0 errors

## Acceptance Criteria (from issue #1546)

- [x] `apps/web/src/components/features/player-detail/PlayerTopGamesCard.tsx` created
- [x] Props: `{ items, maxItems=5, labels, className }`
- [x] DS-15 compliant (`bg-card`/`border-border` + entity accent for rank chip)
- [x] Empty state when items=[]
- [x] Unit tests (rendering, data-slot, DS-15 tokens, className, jest-axe scan)
- [x] Barrel export from `index.ts`
- [x] Orchestrator integration: `PlayerOverviewRegion` derives top-N from `stats.gamePlayCounts` and renders the card under the stats grid
- [x] i18n keys `pages.playerDetail.sections.topGames.{title,winRateLabel,playsLabel,empty}` added en+it (+ `playsLabelWithWins` and `rankAriaLabel` for richer UX)
- [x] Win-rate column: Opt A graceful-hide implemented, plus opportunistic real-data wiring via `winByGame` (#1663 Phase 2 fields)

## Out of Scope

- Cover image rendering (no API field; deferred enhancement)
- View-all button (issue body labels exclude it)
- `PlayerTrendCard` (#1549, blocked by BE #1550)
