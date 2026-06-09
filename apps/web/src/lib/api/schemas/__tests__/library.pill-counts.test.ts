/**
 * GameDetailDtoSchema — ConnectionBar pill counts (#2034 FE)
 *
 * Smoke tests for the Zod schema after the BE was extended with
 * `AgentCount` + `ChatThreadCount` on GameDetailDto (commit on
 * feature/issue-2034-connection-bar-pill-counts). These fields replace the
 * FE-side hardcoded zeros in `GameDetailDesktop.tsx`. They default to 0 so
 * legacy responses that don't carry the fields parse cleanly.
 */

import { describe, expect, it } from 'vitest';

import { GameDetailDtoSchema } from '../library.schemas';

describe('GameDetailDtoSchema #2034 ConnectionBar pill counts', () => {
  const validBase = {
    id: 'a1b2c3d4-1111-4111-8111-111111111111',
    userId: 'a1b2c3d4-2222-4222-8222-222222222222',
    gameId: 'a1b2c3d4-3333-4333-8333-333333333333',
    gameTitle: 'Catan',
    gamePublisher: '',
    gameYearPublished: 1995,
    gameDescription: 'Settlers',
    gameIconUrl: null,
    gameImageUrl: null,
    minPlayers: 3,
    maxPlayers: 4,
    playTimeMinutes: 120,
    complexityRating: 2.28,
    averageRating: 7.09,
    addedAt: '2026-06-08T14:37:26.056526Z',
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: '2026-06-08T18:03:48.813Z',
    stateNotes: null,
    isAvailableForPlay: true,
    timesPlayed: 0,
    lastPlayed: null,
    winRate: 'N/A',
    avgDuration: 'N/A',
  };

  it('accepts both pill counts as non-negative integers', () => {
    const parsed = GameDetailDtoSchema.parse({
      ...validBase,
      agentCount: 3,
      chatThreadCount: 5,
    });
    expect(parsed.agentCount).toBe(3);
    expect(parsed.chatThreadCount).toBe(5);
  });

  it('defaults missing pill counts to 0 (backward compat with legacy BE)', () => {
    const parsed = GameDetailDtoSchema.parse(validBase);
    expect(parsed.agentCount).toBe(0);
    expect(parsed.chatThreadCount).toBe(0);
  });

  it('rejects negative pill counts', () => {
    expect(() =>
      GameDetailDtoSchema.parse({
        ...validBase,
        agentCount: -1,
        chatThreadCount: 0,
      }),
    ).toThrow();
    expect(() =>
      GameDetailDtoSchema.parse({
        ...validBase,
        agentCount: 0,
        chatThreadCount: -2,
      }),
    ).toThrow();
  });

  it('rejects non-integer pill counts', () => {
    expect(() =>
      GameDetailDtoSchema.parse({
        ...validBase,
        agentCount: 1.5,
        chatThreadCount: 0,
      }),
    ).toThrow();
  });
});
