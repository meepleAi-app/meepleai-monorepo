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
