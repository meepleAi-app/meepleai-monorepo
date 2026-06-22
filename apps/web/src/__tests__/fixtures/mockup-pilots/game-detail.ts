/**
 * Game-detail page-mock fixtures (DS-17 Phase 2.5 — argTypes matrix pattern).
 *
 * Refs: spec docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md, umbrella #2063.
 */

import type { Game } from '@/lib/api/schemas/games.schemas';

export const MOCK_GAME_DETAIL: Game = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Wingspan',
  publisher: 'Stonemaier',
  yearPublished: 2019,
  minPlayers: 1,
  maxPlayers: 5,
  minPlayTimeMinutes: 40,
  maxPlayTimeMinutes: 70,
  bggId: 0,
  createdAt: '2026-06-09T00:00:00.000Z',
  description:
    'A bird-collection game where players are bird enthusiasts seeking to discover and attract the best birds to their network of wildlife preserves.',
  averageRating: 4.2,
};
