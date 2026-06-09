/**
 * Library page-mock fixtures (DS-17-6-v2).
 *
 * Consumed by `_content.stories.tsx` (LibraryContent). Covers:
 *   - User-owned games (10 entries cross-domain)
 *   - Empty state (zero games)
 *
 * Refs: spec, umbrella #2063.
 */

import type { Game } from '@/lib/api/schemas/games.schemas';

import { MOCK_DASHBOARD_GAMES } from './dashboard';

const extraGame = (overrides: Partial<Game>): Game => ({
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

// Extend dashboard games to 10 for grid coverage
export const MOCK_LIBRARY_GAMES: Game[] = [
  ...MOCK_DASHBOARD_GAMES,
  extraGame({
    id: '66666666-6666-6666-6666-666666666666',
    title: 'Power Grid',
    publisher: 'Rio Grande',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 6,
    averageRating: 4.1,
  }),
  extraGame({
    id: '77777777-7777-7777-7777-777777777777',
    title: 'Puerto Rico',
    publisher: 'Rio Grande',
    yearPublished: 2002,
    minPlayers: 3,
    maxPlayers: 5,
    averageRating: 4.3,
  }),
  extraGame({
    id: '88888888-8888-8888-8888-888888888888',
    title: 'Zombicide',
    publisher: 'CMON',
    yearPublished: 2012,
    minPlayers: 1,
    maxPlayers: 6,
    averageRating: 3.9,
  }),
  extraGame({
    id: '99999999-9999-9999-9999-999999999999',
    title: 'Carcassonne',
    publisher: 'Hans im Glück',
    yearPublished: 2000,
    minPlayers: 2,
    maxPlayers: 5,
    averageRating: 3.8,
  }),
  extraGame({
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    title: 'Splendor',
    publisher: 'Space Cowboys',
    yearPublished: 2014,
    minPlayers: 2,
    maxPlayers: 4,
    averageRating: 3.9,
  }),
];

export const MOCK_LIBRARY_GAMES_EMPTY: Game[] = [];
