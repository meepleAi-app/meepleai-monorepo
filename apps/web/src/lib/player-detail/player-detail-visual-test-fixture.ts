/**
 * Visual-regression test fixture for `/players/[id]` (Wave 3).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The statistics hook (usePlayerStatistics) cannot
 * reach the backend in CI → the surface stays in `loading` forever → no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator substitutes the fixture for the
 * real fetch and renders a deterministic Wingspan-shaped player profile.
 *
 * **Production safety**: production builds do NOT set the env var. The constant
 * `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture branch is dead
 * code, eliminated by the bundler.
 *
 * State coverage:
 *   - `'default'`   → Sara Rossi profile (hero stats + Wingspan-shaped data)
 *   - `'not-found'` → null    (not-found state renders EmptyPlayerDetail)
 *   All other v2 states (`loading`, `error`) are simulated by the orchestrator
 *   via the `?state=...` URL override hatch and do NOT hit the fixture.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-player-detail.spec.ts` (Task 4)
 *   - `apps/web/e2e/v2-states/player-detail.spec.ts` (Task 4)
 */

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 *
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

/** The two states the fixture can simulate for visual-regression purposes. */
export type PlayerDetailFixtureState = 'default' | 'not-found';

// Re-export component-owned contracts so the fixture and the orchestrator share
// a single source of truth without coupling production code to this test-fixture
// module's lifecycle.
export type { TopGameItem } from '@/components/features/player-detail/PlayerTopGamesCard';
export type { MonthlyWinRatePoint } from '@/components/features/player-detail/PlayerTrendCard';
import type { TopGameItem } from '@/components/features/player-detail/PlayerTopGamesCard';
import type { MonthlyWinRatePoint } from '@/components/features/player-detail/PlayerTrendCard';

/** Shape of a player profile for display in the v2 player detail view. */
export interface PlayerProfileFixture {
  /** URL slug / decoded display id — mirrors the URL param. */
  playerId: string;
  /** Human-readable name decoded from the URL slug. */
  displayName: string;
  /** Total play sessions recorded (from usePlayerStatistics). */
  totalSessions: number;
  /** Total wins across all sessions. */
  totalWins: number;
  /** Win rate as a decimal (0–1). */
  winRate: number;
  /** Most played game name, or null if no games. */
  favoriteGameName: string | null;
  /** Most used agent name, or null if none. */
  favoriteAgentName: string | null;
  /** Number of achievements unlocked. */
  achievementCount: number;
  /** Leaderboard rank among all users, or null if unranked. */
  leaderboardRank: number | null;
  /**
   * Ranked top-N games (desc by playCount). Empty array when no games played.
   * The orchestrator slices to `maxItems` (default 5) inside the card.
   */
  topGames: ReadonlyArray<TopGameItem>;
  /**
   * Win-rate trend per ISO month (last 6 months sliding window). Empty array
   * when there are no completed sessions in the window. Ordered ascending
   * by month string (YYYY-MM lexicographic == chronological for ISO format).
   */
  trendPoints: ReadonlyArray<MonthlyWinRatePoint>;
}

/**
 * Deterministic Wingspan-shaped fixture entry for a realistic mid-tier player.
 * Matches the Wave B/C/D fixture game dataset (Wingspan primary game).
 */
const FIXTURE_DEFAULT: PlayerProfileFixture = {
  playerId: 'sara-rossi',
  displayName: 'Sara Rossi',
  totalSessions: 47,
  totalWins: 28,
  winRate: 0.596,
  favoriteGameName: 'Wingspan',
  favoriteAgentName: 'Mago di Wingspan',
  achievementCount: 12,
  leaderboardRank: 3,
  topGames: [
    { gameId: null, gameName: 'Wingspan', playCount: 22, winCount: 14 },
    { gameId: null, gameName: 'Terraforming Mars', playCount: 12, winCount: 7 },
    { gameId: null, gameName: 'Catan', playCount: 8, winCount: 4 },
    { gameId: null, gameName: 'Carcassonne', playCount: 3, winCount: 2 },
    { gameId: null, gameName: 'Azul', playCount: 2, winCount: 1 },
  ],
  trendPoints: [
    { month: '2026-01', winRate: 0.42 },
    { month: '2026-02', winRate: 0.55 },
    { month: '2026-03', winRate: 0.48 },
    { month: '2026-04', winRate: 0.62 },
    { month: '2026-05', winRate: 0.58 },
    { month: '2026-06', winRate: 0.71 },
  ],
};

/**
 * Returns a deterministic player profile fixture iff the build is a visual-test
 * build. Returns `null` otherwise — caller MUST fall through to the real fetch.
 *
 * @param state - `'default'` renders a populated profile; `'not-found'` returns
 *   null to let the orchestrator route to the not-found state.
 */
export function tryLoadVisualTestFixture(
  state: PlayerDetailFixtureState = 'default'
): PlayerProfileFixture | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (state === 'not-found') return null;
  return FIXTURE_DEFAULT;
}
