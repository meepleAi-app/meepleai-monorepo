/**
 * Visual-regression test fixture for `/games/[id]` (Wave C.1, Issue #581).
 *
 * **Purpose**: workflow `visual-regression-migrated.yml` runs only Next.js prod
 * (no backend API at `:8080`). The library game-detail hook cannot reach the
 * backend in CI → the surface stays in `loading` forever → no screenshot.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, the orchestrator (GameDetailViewV2) substitutes the
 * fixture for the real fetch and renders deterministic data.
 *
 * **Production safety**: production builds do NOT set the env var. The
 * constant `IS_VISUAL_TEST_BUILD` evaluates to `false` and every fixture
 * branch is dead code, eliminated by the bundler.
 *
 * State coverage:
 *   - `'default'`   → full LibraryGameDetail (Wingspan-shaped) for the
 *                     populated mockup baseline (hero + KPI + tabs body)
 *   - `'not-found'` → null (mirrors a real `useLibraryGameDetail` returning
 *                     null when the game is not in the user's library)
 *   - All other v2 states (`loading`, `error`) are simulated by the
 *     orchestrator via the `?state=...` URL override hatch and do NOT hit the
 *     fixture.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-game-detail.spec.ts` (Commit 4)
 *   - `apps/web/e2e/v2-states/game-detail.spec.ts` (Commit 4)
 */

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

/**
 * Deterministic UUIDv4-shaped sentinel encoding issue #581 in the last group
 * for human-debuggability. The orchestrator pivots on `IS_VISUAL_TEST_BUILD`,
 * not on this id, so its only role is documentation for triage runs.
 */
export const VISUAL_TEST_FIXTURE_GAME_DETAIL_ID = '00000000-0000-4000-8000-000000000581' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 *
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

export type GameDetailFixtureState = 'default' | 'not-found';

const NOW = '2026-05-03T10:00:00.000Z';
const USER_ID = '00000000-0000-4000-8000-000000000aaa';
const GAME_ID = '00000000-0000-4000-8000-000000000581';

const FIXTURE_DEFAULT: LibraryGameDetail = {
  // Library-specific data
  libraryEntryId: '00000000-0000-4000-8000-000000000581',
  userId: USER_ID,
  gameId: GAME_ID,
  addedAt: '2026-04-15T08:00:00.000Z',
  notes: null,
  isFavorite: true,
  currentState: 'Owned',
  stateChangedAt: NOW,
  stateNotes: null,
  isAvailableForPlay: true,
  hasCustomPdf: true,
  hasRagAccess: true,
  // Game metadata
  gameTitle: 'Wingspan',
  gamePublisher: 'Stonemaier Games',
  gameYearPublished: 2019,
  gameIconUrl: null,
  gameImageUrl: null,
  description:
    'In Wingspan sei un appassionato ornitologo che cerca di scoprire e attirare i migliori uccelli nella tua riserva. Ogni uccello attiva una catena di poteri progressivi nei tre habitat (foresta, prateria, zone umide). Combo, motori e decisioni di tempistica caratterizzano una partita di 60-90 minuti. Vince chi accumula più punti tra uccelli, uova, cibo e bonus di carta finale.',
  minPlayers: 1,
  maxPlayers: 5,
  playingTimeMinutes: 70,
  minAge: 14,
  complexityRating: 2.4,
  averageRating: 8.1,
  // Play statistics
  timesPlayed: 17,
  lastPlayed: '2026-04-30T19:00:00.000Z',
  winRate: '59%',
  avgDuration: '1h 18m',
  // Extended data
  categories: [
    { id: 'cat-strategy', name: 'Strategia', slug: 'strategy' },
    { id: 'cat-card', name: 'Card-driven', slug: 'card-driven' },
  ],
  mechanics: [
    { id: 'mech-engine', name: 'Engine building', slug: 'engine-building' },
    { id: 'mech-drafting', name: 'Card drafting', slug: 'card-drafting' },
    { id: 'mech-set-collection', name: 'Set collection', slug: 'set-collection' },
  ],
  designers: [{ id: 'des-elizabeth', name: 'Elizabeth Hargrave' }],
  publishers: [{ id: 'pub-stonemaier', name: 'Stonemaier Games' }],
  bggId: 266192,
  recentSessions: [
    {
      id: 's-w-101',
      playedAt: '2026-04-30T19:00:00.000Z',
      durationMinutes: 88,
      durationFormatted: '1h 28m',
      didWin: true,
      players: 'Marco R., Sara T., Luca G., Giulia P.',
      notes: 'Punteggio finale 92.',
    },
    {
      id: 's-w-102',
      playedAt: '2026-04-22T20:30:00.000Z',
      durationMinutes: 72,
      durationFormatted: '1h 12m',
      didWin: false,
      players: 'Sara T., Marco R., Andrea M.',
      notes: 'Punteggio finale 89.',
    },
    {
      id: 's-w-103',
      playedAt: '2026-04-15T18:00:00.000Z',
      durationMinutes: 42,
      durationFormatted: '42m',
      didWin: true,
      players: 'Marco R.',
      notes: 'Solitario.',
    },
  ],
};

/**
 * Returns deterministic LibraryGameDetail iff the build is a visual-test build.
 * Returns `null` for `'not-found'` state. Returns `null` otherwise — caller
 * MUST fall through to the real fetch.
 */
export function tryLoadVisualTestFixture(
  state: GameDetailFixtureState = 'default'
): LibraryGameDetail | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (state === 'not-found') return null;
  return FIXTURE_DEFAULT;
}
