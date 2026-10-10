/**
 * GameDetailDtoSchema — ConnectionBar pill counts (#2034 FE)
 *
 * Smoke tests for the Zod schema after the BE was extended with
 * `AgentCount` + `ChatThreadCount` on GameDetailDto (commit on
 * feature/issue-2034-connection-bar-pill-counts). These fields replace the
 * FE-side hardcoded zeros in `GameDetailDesktop.tsx`. They default to 0 so
 * legacy responses that don't carry the fields parse cleanly.
 *
 * Issue #4138: `agentCount` left the schema with the agent pip. The BE still
 * sends it until its own slice retires the field, so the schema must keep
 * accepting a payload that carries it — and drop it.
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

  it('accepts the chat thread count as a non-negative integer', () => {
    const parsed = GameDetailDtoSchema.parse({
      ...validBase,
      chatThreadCount: 5,
    });
    expect(parsed.chatThreadCount).toBe(5);
  });

  it('defaults a missing chat thread count to 0 (backward compat with legacy BE)', () => {
    const parsed = GameDetailDtoSchema.parse(validBase);
    expect(parsed.chatThreadCount).toBe(0);
  });

  it('still parses a BE payload that carries agentCount, and drops the field', () => {
    const parsed = GameDetailDtoSchema.parse({
      ...validBase,
      agentCount: 3,
      chatThreadCount: 5,
    });
    expect(parsed).not.toHaveProperty('agentCount');
    expect(parsed.chatThreadCount).toBe(5);
  });

  it('rejects a negative chat thread count', () => {
    expect(() =>
      GameDetailDtoSchema.parse({
        ...validBase,
        chatThreadCount: -2,
      })
    ).toThrow();
  });

  it('rejects a non-integer chat thread count', () => {
    expect(() =>
      GameDetailDtoSchema.parse({
        ...validBase,
        chatThreadCount: 1.5,
      })
    ).toThrow();
  });
});
