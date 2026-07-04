/**
 * Fixtures for LibroGameDetailView story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy:
 *   Frame01_Default  — prop-driven via `LibroGameDetailView` directly.
 *                      gameTitle = 'Nanolith' (isLibroGame allowlist) so
 *                      NanolithCampaignCTA renders the "Avvia libro game" CTA.
 *                      Matches mockup state-01 (Eldoria persona mapped to Nanolith).
 *
 *   Frame02_Loading / Frame03_Error / Frame04_NotFound — presentational mocks
 *   in `_mocks/LibroGameDetailMocks.tsx`. The real component has no loading/
 *   error/notFound prop; those states are handled by the parent page shell
 *   (`/library/[gameId]/page.tsx`). The mocks are marked forward-refactor:
 *   wire to real skeleton/error components when they are extracted as
 *   standalone primitives.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-game-detail.html
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ── Default state — Eldoria mapped to Nanolith allowlist entry ──────────────
// `isLibroGame()` checks title against a hardcoded Set {'Nanolith'}.
// We use 'Nanolith' as gameTitle so NanolithCampaignCTA renders the CTA.
// All other fields mirror the Eldoria persona from the mockup.
export const MOCK_LIBROGAME_GAME_DETAIL: LibraryGameDetail = {
  // Library entry
  libraryEntryId: 'lib-nanolith-001',
  userId: 'user-aaron-001',
  gameId: 'game-nanolith-001',
  addedAt: '2024-03-15T10:00:00Z',
  notes: null,
  isFavorite: false,
  currentState: 'playing',
  stateChangedAt: '2024-03-15T10:00:00Z',
  stateNotes: null,
  isAvailableForPlay: true,
  hasCustomPdf: false,
  hasRagAccess: true,

  // Gamebook aggregates (Issue #1288 Bug 2)
  chunkCount: 142,
  sessionsCount: 0,
  kbStatus: 'ready',

  // Game metadata — Nanolith title so isLibroGame() returns true
  gameTitle: 'Nanolith',
  gamePublisher: 'Side Room Studios',
  gameYearPublished: 2024,
  gameIconUrl: null,
  gameImageUrl: null,
  description:
    "Campagna mista narrativa-tattica per 2-4 giocatori. Lo Storybook guida la trama con paragrafi numerati (§); l'Encounter Book gestisce gli scontri sotto-sessione. Manuale e narrativa in inglese.",
  minPlayers: 2,
  maxPlayers: 4,
  playingTimeMinutes: 210, // 3–4h → formats as "3–4h"
  minAge: 14,
  complexityRating: 3.5,
  averageRating: 8.2,

  // Play statistics
  timesPlayed: 0,
  lastPlayed: null,
  winRate: null,
  avgDuration: null,

  // Extended data — no BGG id (BGG user-side asset ban #2123)
  categories: [{ id: 'cat-adventure', name: 'Adventure', slug: 'adventure' }],
  mechanics: [
    { id: 'mec-co-op', name: 'Cooperative', slug: 'cooperative' },
    { id: 'mec-story', name: 'Storytelling', slug: 'storytelling' },
  ],
  designers: [{ id: 'des-001', name: 'Side Room Studios' }],
  publishers: [{ id: 'pub-001', name: 'Side Room Studios' }],
  bggId: null, // BGG user-side asset ban #2123

  // Connection bar counts (Issue #2034)
  agentCount: 1,
  chatThreadCount: 8,
};

// ── Variant: KB indexing in progress ────────────────────────────────────────
export const MOCK_LIBROGAME_GAME_DETAIL_KB_INDEXING: LibraryGameDetail = {
  ...MOCK_LIBROGAME_GAME_DETAIL,
  kbStatus: 'indexing',
  chunkCount: 0,
  hasRagAccess: false,
};

// ── Variant: KB error ────────────────────────────────────────────────────────
export const MOCK_LIBROGAME_GAME_DETAIL_KB_ERROR: LibraryGameDetail = {
  ...MOCK_LIBROGAME_GAME_DETAIL,
  kbStatus: 'error',
  chunkCount: 0,
  hasRagAccess: false,
};
