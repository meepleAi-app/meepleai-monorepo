/**
 * GameDetailDtoSchema — designers field (#2035 FE)
 *
 * Smoke tests for the Zod schema after Task 2 added `Designers` to the BE
 * GameDetailDto (apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/
 * GameDetailDto.cs commit 09ea5eae7). The BE emits a `string[]` of designer
 * names (handler projects `sharedGame.Designers.Select(d => d.Name)`), so the
 * FE schema must accept that shape without rejecting the response.
 *
 * The FE consumer surface (LibraryGameDetail.designers in useLibrary.ts:815)
 * is `Array<{ id: string; name: string }>` because it's populated from
 * `sharedGame.designers` via a parallel fetch (useLibrary.ts:1027), NOT from
 * `gameDetail.designers`. The new wire field is currently received-but-unused
 * by the LibraryGameDetail mapper; this test only locks the schema contract
 * so a future mapper change can safely consume it.
 */

import { describe, expect, it } from 'vitest';

import { GameDetailDtoSchema } from '../library.schemas';

describe('GameDetailDtoSchema #2035 designers', () => {
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

  it('accepts designers as a string array', () => {
    const parsed = GameDetailDtoSchema.parse({
      ...validBase,
      designers: ['Klaus Teuber'],
    });
    expect(parsed.designers).toEqual(['Klaus Teuber']);
  });

  it('accepts designers omitted (backward compat)', () => {
    const parsed = GameDetailDtoSchema.parse(validBase);
    expect(parsed.designers).toBeFalsy();
  });

  it('accepts designers null (BE may emit null when empty)', () => {
    const parsed = GameDetailDtoSchema.parse({ ...validBase, designers: null });
    expect(parsed.designers).toBeNull();
  });

  it('rejects designers when it is not an array', () => {
    expect(() =>
      GameDetailDtoSchema.parse({ ...validBase, designers: 'Klaus Teuber' as unknown }),
    ).toThrow();
  });
});
