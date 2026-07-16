/**
 * GameIdString Schema Tests
 *
 * Verifies that GAME id/gameId fields accept non-RFC-4122 UUIDs (the root
 * cause of the /discover validation bug: real backend game ids can have a
 * version nibble outside 1-5, e.g. seed data like
 * `81cc97e4-f148-fb7b-db36-bf700d1f4561`), while non-game entity ids
 * (session, user, etc.) remain strictly validated as RFC UUIDs.
 */

import { describe, it, expect } from 'vitest';
import { GameSchema, GameSessionDtoSchema } from '../games.schemas';
import { NewGameSchema, RecentKbDocSchema } from '../discover.schemas';

// Real non-RFC-4122 ids observed from the backend (version nibble outside 1-5).
const NON_RFC_GAME_ID = '81cc97e4-f148-fb7b-db36-bf700d1f4561'; // version nibble 'f'
const NON_RFC_KB_GAME_ID = '0f5e0d2f-4281-d76c-ceea-51c076a149b6'; // version nibble 'd'

describe('GameIdString — accepts non-RFC-UUID game ids', () => {
  it('GameSchema.parse accepts a game with a non-RFC-UUID id', () => {
    const game = {
      id: NON_RFC_GAME_ID,
      title: 'Azul',
      publisher: null,
      yearPublished: null,
      minPlayers: null,
      maxPlayers: null,
      minPlayTimeMinutes: null,
      maxPlayTimeMinutes: null,
      bggId: null,
      createdAt: new Date().toISOString(),
    };

    expect(() => GameSchema.parse(game)).not.toThrow();
  });

  it('NewGameSchema.parse accepts a newGame with a non-RFC-UUID id', () => {
    const newGame = {
      id: NON_RFC_GAME_ID,
      name: 'Azul',
      publisher: null,
      year: null,
      imageUrl: null,
      createdAt: new Date().toISOString(),
    };

    expect(() => NewGameSchema.parse(newGame)).not.toThrow();
  });

  it('RecentKbDocSchema.parse accepts a kbDoc with a non-RFC-UUID gameId', () => {
    const kbDoc = {
      id: '550e8400-e29b-41d4-a716-446655440000', // doc's own id — a real RFC UUID here, untouched
      title: 'Azul Rulebook',
      gameId: NON_RFC_KB_GAME_ID,
      gameName: 'Azul',
      docType: 'rulebook' as const,
      lastIngestedAt: new Date().toISOString(),
      chunkCount: 3,
    };

    expect(() => RecentKbDocSchema.parse(kbDoc)).not.toThrow();
  });

  it('(regression) GameSessionDtoSchema still rejects a malformed session id while accepting a non-RFC-UUID gameId', () => {
    const session = {
      id: 'not-a-valid-uuid', // session id — left strict on purpose
      gameId: NON_RFC_GAME_ID, // gameId — relaxed
      status: 'completed',
      startedAt: new Date().toISOString(),
      completedAt: null,
      playerCount: 2,
      players: [],
      winnerName: null,
      notes: null,
      durationMinutes: 30,
    };

    expect(() => GameSessionDtoSchema.parse(session)).toThrow();
  });
});
