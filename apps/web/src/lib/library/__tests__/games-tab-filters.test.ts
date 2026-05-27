import { describe, expect, it } from 'vitest';

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import {
  deriveGamesTabEntries,
  filterGamesByQuery,
  filterGamesByStatus,
  sortGames,
} from '../games-tab-filters';

function entry(overrides: Partial<UserLibraryEntry>): UserLibraryEntry {
  return {
    id: overrides.id ?? 'e1',
    userId: 'u1',
    gameId: overrides.gameId ?? 'g1',
    gameTitle: overrides.gameTitle ?? 'Catan',
    gamePublisher: overrides.gamePublisher ?? 'Kosmos',
    gameYearPublished: overrides.gameYearPublished ?? 1995,
    gameIconUrl: null,
    gameImageUrl: null,
    complexityRating: null,
    averageRating: overrides.averageRating ?? null,
    addedAt: overrides.addedAt ?? '2026-01-01T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: overrides.currentState ?? 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    ownershipDeclaredAt: null,
    hasRagAccess: false,
    agentIsOwned: true,
    minPlayers: null,
    maxPlayers: null,
    playingTimeMinutes: null,
    timesPlayed: overrides.timesPlayed ?? 0,
    lastPlayed: overrides.lastPlayed ?? null,
    privateGameId: null,
    isPrivateGame: false,
    canProposeToCatalog: false,
  } as UserLibraryEntry;
}

describe('filterGamesByStatus', () => {
  const entries = [
    entry({ id: 'a', currentState: 'Owned', timesPlayed: 3 }),
    entry({ id: 'b', currentState: 'Wishlist', timesPlayed: 0 }),
    entry({ id: 'c', currentState: 'Nuovo', timesPlayed: 0 }),
    entry({ id: 'd', currentState: 'InPrestito', timesPlayed: 1 }),
  ];

  it('all → returns every entry', () => {
    expect(filterGamesByStatus(entries, 'all').map(e => e.id)).toEqual(['a', 'b', 'c', 'd']);
  });

  it('owned → excludes Wishlist', () => {
    expect(filterGamesByStatus(entries, 'owned').map(e => e.id)).toEqual(['a', 'c', 'd']);
  });

  it('wishlist → only Wishlist', () => {
    expect(filterGamesByStatus(entries, 'wishlist').map(e => e.id)).toEqual(['b']);
  });

  it('played → only timesPlayed > 0', () => {
    expect(filterGamesByStatus(entries, 'played').map(e => e.id)).toEqual(['a', 'd']);
  });
});

describe('filterGamesByQuery', () => {
  const entries = [
    entry({ id: 'a', gameTitle: 'Catan' }),
    entry({ id: 'b', gameTitle: 'Wingspan' }),
    entry({ id: 'c', gameTitle: 'Brass: Birmingham' }),
  ];

  it('empty query → unchanged', () => {
    expect(filterGamesByQuery(entries, '   ').map(e => e.id)).toEqual(['a', 'b', 'c']);
  });

  it('case-insensitive substring match on title', () => {
    expect(filterGamesByQuery(entries, 'wing').map(e => e.id)).toEqual(['b']);
  });

  it('no match → empty', () => {
    expect(filterGamesByQuery(entries, 'xyznotfound')).toEqual([]);
  });
});

describe('sortGames', () => {
  it('title → A-Z collated', () => {
    const e = [
      entry({ id: 'w', gameTitle: 'Wingspan' }),
      entry({ id: 'b', gameTitle: 'brass' }),
      entry({ id: 'c', gameTitle: 'Catan' }),
    ];
    expect(sortGames(e, 'title').map(x => x.id)).toEqual(['b', 'c', 'w']);
  });

  it('rating → desc, nulls last', () => {
    const e = [
      entry({ id: 'lo', averageRating: 6 }),
      entry({ id: 'na', averageRating: null }),
      entry({ id: 'hi', averageRating: 9 }),
    ];
    expect(sortGames(e, 'rating').map(x => x.id)).toEqual(['hi', 'lo', 'na']);
  });

  it('year → desc, zeros last', () => {
    const e = [
      entry({ id: 'old', gameYearPublished: 1995 }),
      entry({ id: 'unk', gameYearPublished: 0 }),
      entry({ id: 'new', gameYearPublished: 2020 }),
    ];
    expect(sortGames(e, 'year').map(x => x.id)).toEqual(['new', 'old', 'unk']);
  });

  it('last-played → most recent first, nulls last', () => {
    const e = [
      entry({ id: 'mid', lastPlayed: '2026-03-01T00:00:00Z' }),
      entry({ id: 'never', lastPlayed: null }),
      entry({ id: 'recent', lastPlayed: '2026-05-01T00:00:00Z' }),
    ];
    expect(sortGames(e, 'last-played').map(x => x.id)).toEqual(['recent', 'mid', 'never']);
  });

  it('does not mutate input', () => {
    const e = [entry({ id: 'a', gameTitle: 'B' }), entry({ id: 'b', gameTitle: 'A' })];
    const original = e.map(x => x.id);
    sortGames(e, 'title');
    expect(e.map(x => x.id)).toEqual(original);
  });
});

describe('deriveGamesTabEntries', () => {
  it('composes status → query → sort', () => {
    const e = [
      entry({ id: 'a', gameTitle: 'Catan', currentState: 'Owned', timesPlayed: 5, averageRating: 7 }),
      entry({ id: 'b', gameTitle: 'Wingspan', currentState: 'Wishlist', timesPlayed: 0 }),
      entry({ id: 'c', gameTitle: 'Catan Junior', currentState: 'Owned', timesPlayed: 2, averageRating: 9 }),
    ];
    // status=owned removes b; query='catan' keeps a + c; sort=rating → c (9) before a (7)
    expect(deriveGamesTabEntries(e, 'owned', 'catan', 'rating').map(x => x.id)).toEqual(['c', 'a']);
  });
});
