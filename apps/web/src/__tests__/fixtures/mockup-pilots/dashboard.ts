/**
 * Dashboard page-mock fixtures (DS-17-6-v2).
 *
 * Consumed by `DashboardClient.stories.tsx`. Shape derived from API response
 * contracts (Zod schemas under `src/lib/api/schemas/`); some derived shapes
 * are inlined ad-hoc when the upstream type is not directly exportable.
 *
 * Refs: spec, umbrella #2063.
 */

import type { Game } from '@/lib/api/schemas/games.schemas';

// Reusable shape for GameNight card-tile data (derived from /game-nights/{upcoming,completed}
// response shape; full schema lives in src/lib/api/schemas/game-nights.schemas.ts).
type GameNightCard = {
  id: string;
  title: string;
  scheduledFor: string;
  status: 'Draft' | 'Published' | 'InProgress' | 'Completed' | 'Cancelled';
  gameId: string;
  gameName?: string;
  hostUserId: string;
};

// Active session summary (derived from /sessions/active response shape).
type ActiveSessionSummary = {
  id: string;
  gameId: string;
  gameName: string;
  startedAt: string;
  players: string[];
};

// Friends activity feed entry (derived from /dashboard/friends-activity response).
type FriendActivity = {
  friendId: string;
  displayName: string;
  verb: 'completed' | 'created' | 'joined';
  gameName: string;
  timestamp: string;
};

// Library stats summary (derived from /library/stats response).
type LibraryStats = {
  totalGames: number;
  totalSessions: number;
  hoursPlayed: number;
  winRate: number;
};

const sampleGame = (overrides: Partial<Game>): Game => ({
  id: '00000000-0000-0000-0000-000000000000',
  title: 'Sample Game',
  publisher: null,
  yearPublished: null,
  minPlayers: 1,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 90,
  bggId: 0,
  createdAt: '2026-06-09T00:00:00.000Z',
  ...overrides,
});

export const MOCK_DASHBOARD_GAMES: Game[] = [
  sampleGame({
    id: '11111111-1111-1111-1111-111111111111',
    title: 'Wingspan',
    publisher: 'Stonemaier',
    yearPublished: 2019,
    minPlayers: 1,
    maxPlayers: 5,
    averageRating: 4.2,
  }),
  sampleGame({
    id: '22222222-2222-2222-2222-222222222222',
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    averageRating: 3.8,
  }),
  sampleGame({
    id: '33333333-3333-3333-3333-333333333333',
    title: 'Paleo',
    publisher: 'Hans im Glück',
    yearPublished: 2020,
    minPlayers: 1,
    maxPlayers: 4,
    averageRating: 4.1,
  }),
  sampleGame({
    id: '44444444-4444-4444-4444-444444444444',
    title: 'Codenames',
    publisher: 'CGE',
    yearPublished: 2015,
    minPlayers: 2,
    maxPlayers: 8,
    averageRating: 4.0,
  }),
  sampleGame({
    id: '55555555-5555-5555-5555-555555555555',
    title: 'Azul',
    publisher: 'Plan B',
    yearPublished: 2017,
    minPlayers: 2,
    maxPlayers: 4,
    averageRating: 4.0,
  }),
];

export const MOCK_DASHBOARD_ACTIVE_SESSIONS: ActiveSessionSummary[] = [
  {
    id: 'session-1',
    gameId: '11111111-1111-1111-1111-111111111111',
    gameName: 'Wingspan',
    startedAt: '2026-06-09T10:00:00.000Z',
    players: ['Alice', 'Bob'],
  },
];

export const MOCK_DASHBOARD_UPCOMING_GAMENIGHTS: GameNightCard[] = [
  {
    id: 'gn-upcoming-1',
    title: 'Wingspan Sunday',
    scheduledFor: '2026-06-14T18:00:00.000Z',
    status: 'Published',
    gameId: '11111111-1111-1111-1111-111111111111',
    gameName: 'Wingspan',
    hostUserId: 'storybook-user',
  },
  {
    id: 'gn-upcoming-2',
    title: 'Catan Marathon',
    scheduledFor: '2026-06-20T14:00:00.000Z',
    status: 'Published',
    gameId: '22222222-2222-2222-2222-222222222222',
    gameName: 'Catan',
    hostUserId: 'storybook-user',
  },
];

export const MOCK_DASHBOARD_COMPLETED_GAMENIGHTS: GameNightCard[] = [
  {
    id: 'gn-completed-1',
    title: 'Last Wingspan',
    scheduledFor: '2026-06-01T18:00:00.000Z',
    status: 'Completed',
    gameId: '11111111-1111-1111-1111-111111111111',
    gameName: 'Wingspan',
    hostUserId: 'storybook-user',
  },
  {
    id: 'gn-completed-2',
    title: 'Paleo Coop',
    scheduledFor: '2026-05-25T18:00:00.000Z',
    status: 'Completed',
    gameId: '33333333-3333-3333-3333-333333333333',
    gameName: 'Paleo',
    hostUserId: 'storybook-user',
  },
];

export const MOCK_DASHBOARD_FRIENDS_ACTIVITY: FriendActivity[] = [
  {
    friendId: 'friend-1',
    displayName: 'Alice',
    verb: 'completed',
    gameName: 'Wingspan',
    timestamp: '2026-06-08T20:00:00.000Z',
  },
  {
    friendId: 'friend-2',
    displayName: 'Bob',
    verb: 'created',
    gameName: 'Catan',
    timestamp: '2026-06-08T15:00:00.000Z',
  },
  {
    friendId: 'friend-3',
    displayName: 'Carol',
    verb: 'joined',
    gameName: 'Paleo',
    timestamp: '2026-06-07T19:00:00.000Z',
  },
];

export const MOCK_DASHBOARD_LIBRARY_STATS: LibraryStats = {
  totalGames: 28,
  totalSessions: 142,
  hoursPlayed: 87,
  winRate: 0.62,
};

// ── Empty states for `Empty` story ──────────────────────────────────────────

export const MOCK_DASHBOARD_GAMES_EMPTY: Game[] = [];
export const MOCK_DASHBOARD_ACTIVE_SESSIONS_EMPTY: ActiveSessionSummary[] = [];
export const MOCK_DASHBOARD_UPCOMING_GAMENIGHTS_EMPTY: GameNightCard[] = [];
export const MOCK_DASHBOARD_COMPLETED_GAMENIGHTS_EMPTY: GameNightCard[] = [];
export const MOCK_DASHBOARD_FRIENDS_ACTIVITY_EMPTY: FriendActivity[] = [];
export const MOCK_DASHBOARD_LIBRARY_STATS_EMPTY: LibraryStats = {
  totalGames: 0,
  totalSessions: 0,
  hoursPlayed: 0,
  winRate: 0,
};
