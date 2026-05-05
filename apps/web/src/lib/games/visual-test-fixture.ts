/**
 * Visual-regression test fixture for `/games?tab=library` (Wave B.1, Issue #633).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The library tab's data hook (`useLibrary`) cannot
 * reach the backend in CI → the surface stays in `loading` forever → no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator (GamesLibraryView) substitutes the
 * fixture for the real fetch and renders deterministic entries.
 *
 * **Production safety**: production builds do NOT set the env var. The constant
 * `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture branch is dead
 * code, eliminated by the bundler. The fixture UUID is meaningless to a
 * production deployment.
 *
 * State coverage:
 *   - `'default'` → 5 entries (hero stats + grid baseline)
 *   - `'empty'`   → []        (library-empty baseline)
 *   - All other v2 states (`loading`, `filtered-empty`, `error`) are simulated
 *     by the orchestrator via the `?state=...` URL override hatch and do NOT
 *     hit the fixture.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-games-library.spec.ts` (planned Commit 4)
 *   - `apps/web/e2e/v2-states/games-library.spec.ts` (planned Commit 4)
 */

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

/**
 * Deterministic UUIDv4-shaped sentinel encoding issue #633 in the last group
 * for human-debuggability. The orchestrator pivots on `IS_VISUAL_TEST_BUILD`,
 * not on this id, so its only role is documentation for triage runs.
 */
export const VISUAL_TEST_FIXTURE_LIBRARY_ID = '00000000-0000-4000-8000-000000000633' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 *
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

export type LibraryFixtureState = 'default' | 'empty';

const NOW = '2026-04-29T10:00:00.000Z';

const baseEntry = (
  overrides: Partial<UserLibraryEntry> & Pick<UserLibraryEntry, 'id' | 'gameId' | 'gameTitle'>
): UserLibraryEntry => ({
  userId: '00000000-0000-4000-8000-000000000aaa',
  gamePublisher: null,
  gameYearPublished: null,
  gameIconUrl: null,
  gameImageUrl: null,
  addedAt: NOW,
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: null,
  stateNotes: null,
  hasKb: false,
  kbCardCount: 0,
  kbIndexedCount: 0,
  kbProcessingCount: 0,
  ownershipDeclaredAt: null,
  hasRagAccess: false,
  agentIsOwned: true,
  minPlayers: null,
  maxPlayers: null,
  playingTimeMinutes: null,
  complexityRating: null,
  averageRating: null,
  privateGameId: null,
  isPrivateGame: false,
  canProposeToCatalog: false,
  ...overrides,
});

const FIXTURE_DEFAULT: readonly UserLibraryEntry[] = [
  baseEntry({
    id: '00000000-0000-4000-8000-000000000101',
    gameId: '00000000-0000-4000-8000-000000000201',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
    averageRating: 7.2,
    currentState: 'Owned',
    isFavorite: true,
    kbCardCount: 2,
    kbIndexedCount: 2,
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000102',
    gameId: '00000000-0000-4000-8000-000000000202',
    gameTitle: 'Terraforming Mars',
    gamePublisher: 'Stronghold Games',
    gameYearPublished: 2016,
    averageRating: 8.4,
    currentState: 'Owned',
    kbCardCount: 1,
    kbIndexedCount: 1,
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000103',
    gameId: '00000000-0000-4000-8000-000000000203',
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier Games',
    gameYearPublished: 2019,
    averageRating: 8.1,
    currentState: 'Wishlist',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000104',
    gameId: '00000000-0000-4000-8000-000000000204',
    gameTitle: 'Azul',
    gamePublisher: 'Plan B Games',
    gameYearPublished: 2017,
    averageRating: 7.8,
    currentState: 'Owned',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000105',
    gameId: '00000000-0000-4000-8000-000000000205',
    gameTitle: 'Carcassonne',
    gamePublisher: 'Hans im Glück',
    gameYearPublished: 2000,
    averageRating: 7.4,
    currentState: 'Nuovo',
  }),
];

/**
 * Returns deterministic library entries iff the build is a visual-test build.
 * Returns `null` otherwise — caller MUST fall through to the real fetch.
 */
export function tryLoadVisualTestFixture(
  state: LibraryFixtureState = 'default'
): readonly UserLibraryEntry[] | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (state === 'empty') return [];
  return FIXTURE_DEFAULT;
}
