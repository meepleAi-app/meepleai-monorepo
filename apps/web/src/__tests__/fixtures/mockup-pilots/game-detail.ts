/**
 * Game-detail page-mock fixtures (DS-17-6-v2).
 *
 * Consumed by `GameDetailView.stories.tsx`. Shape derives from /library/games/{id}
 * response shape (extended Game with description + designer + rating).
 *
 * Refs: spec, umbrella #2063.
 */

import type { Game } from '@/lib/api/schemas/games.schemas';

import { MOCK_DASHBOARD_GAMES } from './dashboard';

// Game detail returns the same Game shape with description + averageRating already populated.
export const MOCK_GAME_DETAIL: Game = {
  ...MOCK_DASHBOARD_GAMES[0],
  description:
    'A bird-collection game where players are bird enthusiasts seeking to discover and attract the best birds to their network of wildlife preserves.',
  averageRating: 4.2,
};

// Empty = "game not found" — the story uses MSW 404 to drive this state instead
// of mutating gameId, so this export is mainly for documentation parity.
export const MOCK_GAME_DETAIL_EMPTY: Game | null = null;
