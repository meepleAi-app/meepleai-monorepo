/**
 * Visual-regression test fixture for `/players` (Wave 4 D1).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The players data hook (usePlayerStatistics)
 * cannot reach the backend in CI → the surface stays in `loading` forever →
 * no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator (PlayersView) substitutes the
 * fixture for the real fetch and renders deterministic entries.
 *
 * **Production safety**: production builds do NOT set the env var. The constant
 * `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture branch is dead
 * code, eliminated by the bundler. The fixture UUID is meaningless to a
 * production deployment.
 *
 * State coverage:
 *   - `'default'` → 5 game entries (hero stats + grid baseline)
 *   - `'empty'`   → []        (players-empty baseline)
 *   - All other v2 states (`loading`, `filtered-empty`, `error`) are simulated
 *     by the orchestrator via the `?state=...` URL override hatch and do NOT
 *     hit the fixture.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-players-index.spec.ts` (Task 4)
 *   - `apps/web/e2e/v2-states/players-index.spec.ts` (Task 4)
 */

import type { PlayerListItem } from './players-filters';

/**
 * Deterministic UUIDv4-shaped sentinel encoding the Wave 4 D1 players issue
 * in the last group for human-debuggability. The orchestrator pivots on
 * `IS_VISUAL_TEST_BUILD`, not on this id, so its only role is documentation
 * for triage runs.
 */
export const VISUAL_TEST_FIXTURE_PLAYERS_ID = '00000000-0000-4000-8000-000000000682' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 *
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

export type PlayersFixtureState = 'default' | 'empty';

/**
 * 5 deterministic PlayerListItem entries covering a realistic spread of
 * game names (including multi-word) and play counts in descending order.
 *
 * These mirror the games used in the agents/library Wave B fixtures so
 * visual baselines share a recognisable dataset across the test suite.
 */
const FIXTURE_DEFAULT: ReadonlyArray<PlayerListItem> = [
  {
    id: 'wingspan',
    displayName: 'Wingspan',
    gameName: 'Wingspan',
    playCount: 12,
  },
  {
    id: 'azul',
    displayName: 'Azul',
    gameName: 'Azul',
    playCount: 8,
  },
  {
    id: 'catan',
    displayName: 'Catan',
    gameName: 'Catan',
    playCount: 5,
  },
  {
    id: 'terraforming-mars',
    displayName: 'Terraforming Mars',
    gameName: 'Terraforming Mars',
    playCount: 3,
  },
  {
    id: 'splendor',
    displayName: 'Splendor',
    gameName: 'Splendor',
    playCount: 2,
  },
];

/**
 * Returns deterministic player (game) entries iff the build is a visual-test
 * build. Returns `null` otherwise — caller MUST fall through to the real fetch.
 */
export function tryLoadVisualTestFixture(
  state: PlayersFixtureState = 'default'
): ReadonlyArray<PlayerListItem> | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (state === 'empty') return [];
  return FIXTURE_DEFAULT;
}
