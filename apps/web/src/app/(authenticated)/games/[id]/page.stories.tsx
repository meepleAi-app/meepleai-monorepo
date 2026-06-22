/**
 * @mockup admin-mockups/design_files/sp4-game-detail.html
 * @mockup admin-mockups/design_files/sp4-game-detail.jsx
 *
 * sp4-game-detail — DS-17-12 #2214.
 *
 * Mockup parity: admin-mockups/design_files/sp4-game-detail.{html,jsx}.
 * Post Stage 0 BGG cleanup (line 390 "Rating BGG" → "Voto community", commit 79bcbda0e).
 * design_intent: current (see sp4-game-detail.fidelity.json).
 *
 * POST-EPIC #2096 M1-M7 wire verify (PR #2207 b98e4328b):
 * - M1 GameHero v2        → components/game-detail/GameHero.tsx
 * - M2 Tabs animated      → components/features/game-detail (GameDetailTabsAnimated)
 * - M3 ConnectionBar      → components/ui/data-display/connection-bar
 * - M4 Toolbox 1-Card     → GameTabsPanel Toolbox tab
 * - M5 ContributorsStrip  → components/features/library/SessionContributorsStrip
 * - M6 Info 3-Card        → GameTabsPanel Info tab (KPI + Specs + Rules)
 * - M7 Layout             → components/game-detail/GameDetailDesktop.tsx
 * - Orchestrator          → _components/GameDetailView.tsx
 *
 * MSW handlers: story-level overrides for the 3 parallel fetches in
 * useLibraryGameDetail (library/games/:id + shared-games/:id + gamebooks)
 * and for the session-contributors strip (games/:id/session-contributors).
 * Global handlers in src/__tests__/mocks/handlers do NOT cover these paths.
 *
 * Refs: #2214, EPIC #2096 closure PR #2207, spec § 4.2.
 */

import { http, HttpResponse } from 'msw';

import GameDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

// ─── Fixture UUID ────────────────────────────────────────────────────────────
// Must be a valid UUID: `getGameDetail` and `sharedGames.getById` both validate
// the format and throw/return null for non-UUID strings. Without a valid UUID,
// useLibraryGameDetail resolves to null → page renders "not-found" state.
const FIXTURE_GAME_ID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';

// ─── Fixture data ─────────────────────────────────────────────────────────────

/** LibraryGameDetail-compatible shape returned by /api/v1/library/games/:id */
const mockLibraryGameDetail = {
  libraryEntryId: 'entry-001',
  userId: 'storybook-user',
  gameId: FIXTURE_GAME_ID,
  addedAt: '2025-09-01T10:00:00Z',
  notes: 'Grande gioco da tavolo',
  isFavorite: true,
  currentState: 'Posseduto',
  stateChangedAt: '2025-09-01T10:00:00Z',
  stateNotes: null,
  isAvailableForPlay: true,
  hasCustomPdf: false,
  hasRagAccess: true,
  gameTitle: 'Terraforming Mars',
  gamePublisher: 'FryxGames',
  gameYearPublished: 2016,
  gameIconUrl: null,
  gameImageUrl: null,
  description:
    'Corporations compete to terraform Mars by raising the temperature, oxygen level, and ocean coverage. Players build infrastructure and accumulate victory points.',
  minPlayers: 1,
  maxPlayers: 5,
  playingTimeMinutes: 120,
  minAge: 12,
  complexityRating: 3.2,
  averageRating: 8.4,
  timesPlayed: 7,
  lastPlayed: '2026-05-20T19:30:00Z',
  winRate: '43%',
  avgDuration: '118m',
  categories: [
    { id: 'cat-1', name: 'Strategia', slug: 'strategia' },
    { id: 'cat-2', name: 'Sci-Fi', slug: 'sci-fi' },
  ],
  mechanics: [
    { id: 'mec-1', name: 'Gestione mano', slug: 'gestione-mano' },
    { id: 'mec-2', name: 'Posizionamento tessere', slug: 'posizionamento-tessere' },
  ],
  designers: [{ id: 'des-1', name: 'Jacob Fryxelius' }],
  publishers: [{ id: 'pub-1', name: 'FryxGames' }],
  bggId: null,
  recentSessions: [
    {
      id: 'sess-1',
      playedAt: '2026-05-20T19:30:00Z',
      durationMinutes: 105,
      durationFormatted: '1h 45m',
      didWin: true,
      players: 'Alice, Bob, Carol',
      notes: null,
    },
  ],
  customCoverR2Key: null,
  agentCount: 2,
  chatThreadCount: 1,
};

/** Minimal SharedGameDetail returned by /api/v1/shared-games/:id (parallel fetch) */
const mockSharedGame = {
  id: FIXTURE_GAME_ID,
  title: 'Terraforming Mars',
  publisher: 'FryxGames',
  yearPublished: 2016,
  thumbnailUrl: null,
  imageUrl: null,
  description: mockLibraryGameDetail.description,
  minPlayers: 1,
  maxPlayers: 5,
  playingTimeMinutes: 120,
  complexityRating: 3.2,
  averageRating: 8.4,
  categories: mockLibraryGameDetail.categories,
  mechanics: mockLibraryGameDetail.mechanics,
  designers: mockLibraryGameDetail.designers,
  publishers: mockLibraryGameDetail.publishers,
  bggId: null,
  isActive: true,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: '2025-01-01T00:00:00Z',
};

/** SessionContributorDto[] returned by /api/v1/games/:id/session-contributors */
const mockContributors = [
  {
    userId: 'user-001',
    displayName: 'Alice Rossi',
    avatarUrl: null,
    sessionCount: 12,
    lastPlayedAt: '2026-05-20T19:30:00Z',
  },
  {
    userId: 'user-002',
    displayName: 'Marco Bianchi',
    avatarUrl: null,
    sessionCount: 8,
    lastPlayedAt: '2026-04-15T21:00:00Z',
  },
];

// ─── Shared MSW handlers ──────────────────────────────────────────────────────

const gameDetailHandlers = [
  // Primary: library entry with full LibraryGameDetail shape
  http.get(`*/api/v1/library/games/${FIXTURE_GAME_ID}`, () =>
    HttpResponse.json(mockLibraryGameDetail)
  ),
  // Parallel: shared-game catalog data
  http.get(`*/api/v1/shared-games/${FIXTURE_GAME_ID}`, () => HttpResponse.json(mockSharedGame)),
  // Gamebooks endpoint (catch-all — returns empty array, game is not a gamebook)
  http.get('*/api/v1/gamebooks', () => HttpResponse.json([])),
  // Session contributors strip (Issue #2036 / M5)
  http.get(`*/api/v1/games/${FIXTURE_GAME_ID}/session-contributors`, () =>
    HttpResponse.json(mockContributors)
  ),
];

// ─── Meta ─────────────────────────────────────────────────────────────────────

const meta: Meta<typeof GameDetailPage> = {
  title: 'Authenticated / sp4-game-detail',
  component: GameDetailPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/games/${FIXTURE_GAME_ID}`,
        // useParams reads from the segment — storybook/nextjs resolves [id] from pathname
        params: { id: FIXTURE_GAME_ID },
      },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Authenticated game detail (POST-EPIC #2096 M1-M7 deliverables). Default = Info tab loaded state. Additional stories: loading + not-found states.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof GameDetailPage>;

// ─── Stories ──────────────────────────────────────────────────────────────────

/**
 * Default — loaded state, Info tab (default tab).
 * Mockup parity: sp4-game-detail.html / sp4-game-detail.jsx default frame.
 * Verifies M1 GameHero v2 + M2 Tabs + M3 ConnectionBar + M5 Strip + M6 Info 3-Card + M7 Layout.
 */
export const Default: Story = {
  name: 'Loaded — Info tab (sp4-game-detail default)',
  parameters: {
    msw: {
      handlers: gameDetailHandlers,
    },
  },
};

/**
 * Loading state — all fetches pending.
 * Verifies skeleton/loading UI for GameHero and tabs.
 */
export const Loading: Story = {
  name: 'Loading state',
  parameters: {
    msw: {
      handlers: [
        http.get(`*/api/v1/library/games/${FIXTURE_GAME_ID}`, async () => {
          await new Promise(resolve => setTimeout(resolve, 60_000));
          return HttpResponse.json(mockLibraryGameDetail);
        }),
        http.get(`*/api/v1/shared-games/${FIXTURE_GAME_ID}`, async () => {
          await new Promise(resolve => setTimeout(resolve, 60_000));
          return HttpResponse.json(mockSharedGame);
        }),
        http.get('*/api/v1/gamebooks', () => HttpResponse.json([])),
        http.get(`*/api/v1/games/${FIXTURE_GAME_ID}/session-contributors`, () =>
          HttpResponse.json([])
        ),
      ],
    },
  },
};

/**
 * Not-found state — game not in library and not in shared catalog.
 * Verifies the FSM "not-found" cell (Cell 4 in deriveGameDetailUiState).
 */
export const NotFound: Story = {
  name: 'Not found state',
  parameters: {
    msw: {
      handlers: [
        http.get(`*/api/v1/library/games/${FIXTURE_GAME_ID}`, () =>
          HttpResponse.json({ error: 'Not found' }, { status: 404 })
        ),
        http.get(`*/api/v1/shared-games/${FIXTURE_GAME_ID}`, () =>
          HttpResponse.json(null, { status: 404 })
        ),
        http.get('*/api/v1/gamebooks', () => HttpResponse.json([])),
        http.get(`*/api/v1/games/${FIXTURE_GAME_ID}/session-contributors`, () =>
          HttpResponse.json([])
        ),
      ],
    },
  },
};
