/**
 * Visual-regression test fixture for `/library` desktop (Wave B.3, Issue #574).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The library data hook cannot reach the backend
 * in CI → the surface stays in `loading` forever → no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator (LibraryHubV2) substitutes the
 * fixture for the real fetch and renders deterministic entries.
 *
 * **Production safety**: production builds do NOT set the env var. The
 * constant `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture
 * branch is dead code, eliminated by the bundler.
 *
 * State coverage:
 *   - `'default'` → 12 mixed entries (hero stats + 3-tab grid baseline:
 *                   5 with `hasKb`, 2 with `Wishlist`, 2 with `InPrestito`,
 *                   1 with `kbCardCount > 0` but `hasKb=false` to exercise
 *                   the OR branch of `isKbEntry`)
 *   - `'empty'`   → []        (library-empty baseline)
 *   - All other v2 states (`loading`, `filtered-empty`, `error`) are
 *     simulated by the orchestrator via the `?state=...` URL override hatch
 *     and do NOT hit the fixture.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-library-desktop.spec.ts` (Commit 4)
 *   - `apps/web/e2e/v2-states/library.spec.ts` (Commit 4)
 */

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

/**
 * Deterministic UUIDv4-shaped sentinel encoding issue #574 in the last group
 * for human-debuggability. The orchestrator pivots on `IS_VISUAL_TEST_BUILD`,
 * not on this id, so its only role is documentation for triage runs.
 */
export const VISUAL_TEST_FIXTURE_LIBRARY_ID = '00000000-0000-4000-8000-000000000574' as const;

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

const NOW = '2026-04-30T10:00:00.000Z';
const USER_ID = '00000000-0000-4000-8000-000000000aaa';

const baseEntry = (
  overrides: Partial<UserLibraryEntry> & Pick<UserLibraryEntry, 'id' | 'gameId' | 'gameTitle'>
): UserLibraryEntry => ({
  userId: USER_ID,
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

/**
 * 12 deterministic entries. Distribution:
 *   - state: 7 Owned, 2 Wishlist, 2 InPrestito, 1 Nuovo
 *   - hasKb true:        5 (Catan, TM, Brass, Root, Ark Nova)
 *   - kbCardCount > 0 but hasKb=false: 1 (Spirit Island — PDF in flight)
 *   - kb tab visible total: 6 (5 hasKb + 1 in-flight)
 *
 * Hero stats expectations:
 *   - totalGames: 12
 *   - kbReady:    5 (Catan, TM, Brass, Root, Ark Nova)
 *   - wishlist:   2 (Wingspan, Gloomhaven)
 *   - loaned:     2 (Pandemic, Root)
 */
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
    hasKb: true,
    kbCardCount: 2,
    kbIndexedCount: 2,
    addedAt: '2026-04-29T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000102',
    gameId: '00000000-0000-4000-8000-000000000202',
    gameTitle: 'Terraforming Mars',
    gamePublisher: 'Stronghold Games',
    gameYearPublished: 2016,
    averageRating: 8.4,
    currentState: 'Owned',
    hasKb: true,
    kbCardCount: 1,
    kbIndexedCount: 1,
    addedAt: '2026-04-28T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000103',
    gameId: '00000000-0000-4000-8000-000000000203',
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier Games',
    gameYearPublished: 2019,
    averageRating: 8.1,
    currentState: 'Wishlist',
    addedAt: '2026-04-27T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000104',
    gameId: '00000000-0000-4000-8000-000000000204',
    gameTitle: 'Azul',
    gamePublisher: 'Plan B Games',
    gameYearPublished: 2017,
    averageRating: 7.8,
    currentState: 'Owned',
    addedAt: '2026-04-26T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000105',
    gameId: '00000000-0000-4000-8000-000000000205',
    gameTitle: 'Carcassonne',
    gamePublisher: 'Hans im Glück',
    gameYearPublished: 2000,
    averageRating: 7.4,
    currentState: 'Nuovo',
    addedAt: '2026-04-25T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000106',
    gameId: '00000000-0000-4000-8000-000000000206',
    gameTitle: 'Brass: Birmingham',
    gamePublisher: 'Roxley',
    gameYearPublished: 2018,
    averageRating: 8.6,
    currentState: 'Owned',
    isFavorite: true,
    hasKb: true,
    kbCardCount: 3,
    kbIndexedCount: 3,
    addedAt: '2026-04-24T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000107',
    gameId: '00000000-0000-4000-8000-000000000207',
    gameTitle: 'Pandemic',
    gamePublisher: 'Z-Man Games',
    gameYearPublished: 2008,
    averageRating: 7.6,
    currentState: 'InPrestito',
    addedAt: '2026-04-23T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000108',
    gameId: '00000000-0000-4000-8000-000000000208',
    gameTitle: 'Spirit Island',
    gamePublisher: 'Greater Than Games',
    gameYearPublished: 2017,
    averageRating: 8.3,
    currentState: 'Owned',
    // PDF uploaded but still processing → counts for `kb` tab via the OR branch.
    hasKb: false,
    kbCardCount: 1,
    kbIndexedCount: 0,
    kbProcessingCount: 1,
    addedAt: '2026-04-22T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-000000000109',
    gameId: '00000000-0000-4000-8000-000000000209',
    gameTitle: 'Gloomhaven',
    gamePublisher: 'Cephalofair Games',
    gameYearPublished: 2017,
    averageRating: 8.7,
    currentState: 'Wishlist',
    addedAt: '2026-04-21T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-00000000010a',
    gameId: '00000000-0000-4000-8000-00000000020a',
    gameTitle: 'Scythe',
    gamePublisher: 'Stonemaier Games',
    gameYearPublished: 2016,
    averageRating: 8.2,
    currentState: 'Owned',
    addedAt: '2026-04-20T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-00000000010b',
    gameId: '00000000-0000-4000-8000-00000000020b',
    gameTitle: 'Root',
    gamePublisher: 'Leder Games',
    gameYearPublished: 2018,
    averageRating: 8.0,
    currentState: 'InPrestito',
    hasKb: true,
    kbCardCount: 2,
    kbIndexedCount: 2,
    addedAt: '2026-04-19T08:00:00.000Z',
  }),
  baseEntry({
    id: '00000000-0000-4000-8000-00000000010c',
    gameId: '00000000-0000-4000-8000-00000000020c',
    gameTitle: 'Ark Nova',
    gamePublisher: 'Feuerland Spiele',
    gameYearPublished: 2021,
    averageRating: 8.5,
    currentState: 'Owned',
    hasKb: true,
    kbCardCount: 1,
    kbIndexedCount: 1,
    addedAt: '2026-04-18T08:00:00.000Z',
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
