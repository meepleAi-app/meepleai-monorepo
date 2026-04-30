import { describe, it, expect } from 'vitest';

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import {
  type LibraryEntityKey,
  type LibrarySortKey,
  deriveHeroStats,
  deriveLibraryUiState,
  filterByEntity,
  isKbEntry,
  matchQuery,
  sortLibraryEntries,
} from '../library-filters';

const NOW = '2026-04-30T10:00:00.000Z';

const baseEntry = (
  overrides: Partial<UserLibraryEntry> & Pick<UserLibraryEntry, 'id' | 'gameId' | 'gameTitle'>
): UserLibraryEntry => ({
  userId: '00000000-0000-4000-8000-000000000aaa',
  gamePublisher: null,
  gameYearPublished: null,
  gameIconUrl: null,
  gameImageUrl: null,
  addedAt: NOW,
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
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
  complexityRating: null,
  averageRating: null,
  privateGameId: null,
  isPrivateGame: false,
  canProposeToCatalog: false,
  ...overrides,
});

describe('isKbEntry', () => {
  it('returns true when hasKb is true', () => {
    const entry = baseEntry({
      id: 'a',
      gameId: 'g-a',
      gameTitle: 'A',
      hasKb: true,
      kbCardCount: 0,
    });
    expect(isKbEntry(entry)).toBe(true);
  });

  it('returns true when kbCardCount > 0 even if hasKb is false (PDFs in flight)', () => {
    const entry = baseEntry({
      id: 'a',
      gameId: 'g-a',
      gameTitle: 'A',
      hasKb: false,
      kbCardCount: 2,
      kbProcessingCount: 2,
    });
    expect(isKbEntry(entry)).toBe(true);
  });

  it('returns false when both hasKb is false and kbCardCount is 0', () => {
    const entry = baseEntry({ id: 'a', gameId: 'g-a', gameTitle: 'A' });
    expect(isKbEntry(entry)).toBe(false);
  });
});

describe('filterByEntity', () => {
  const entries: readonly UserLibraryEntry[] = [
    baseEntry({ id: '1', gameId: 'g-1', gameTitle: 'Catan', hasKb: true, kbCardCount: 2 }),
    baseEntry({
      id: '2',
      gameId: 'g-2',
      gameTitle: 'Wingspan',
      currentState: 'Wishlist',
    }),
    baseEntry({
      id: '3',
      gameId: 'g-3',
      gameTitle: 'Azul',
      currentState: 'InPrestito',
    }),
    baseEntry({
      id: '4',
      gameId: 'g-4',
      gameTitle: 'Carcassonne',
      kbCardCount: 1,
      kbProcessingCount: 1, // PDF in flight, hasKb still false
    }),
  ];

  it('returns all entries when entity is "all"', () => {
    expect(filterByEntity(entries, 'all')).toHaveLength(4);
  });

  it('filters to KB entries when entity is "kb" (hasKb OR kbCardCount > 0)', () => {
    const result = filterByEntity(entries, 'kb');
    expect(result.map(e => e.gameTitle)).toEqual(['Catan', 'Carcassonne']);
  });

  it('filters to loaned entries when entity is "loaned"', () => {
    const result = filterByEntity(entries, 'loaned');
    expect(result.map(e => e.gameTitle)).toEqual(['Azul']);
  });

  it('returns a copy (never mutates input) for "all"', () => {
    const result = filterByEntity(entries, 'all');
    expect(result).not.toBe(entries);
  });

  it('falls back to "all" semantics for unknown entity keys', () => {
    const result = filterByEntity(entries, 'foo' as unknown as LibraryEntityKey);
    expect(result).toHaveLength(4);
  });
});

describe('matchQuery', () => {
  const entry = baseEntry({
    id: '1',
    gameId: 'g-1',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
  });

  it('returns true for empty query (whitespace only)', () => {
    expect(matchQuery(entry, '   ')).toBe(true);
  });

  it('matches game title case-insensitively', () => {
    expect(matchQuery(entry, 'CATAN')).toBe(true);
  });

  it('matches publisher substring', () => {
    expect(matchQuery(entry, 'kos')).toBe(true);
  });

  it('matches year as substring', () => {
    expect(matchQuery(entry, '1995')).toBe(true);
  });

  it('returns false when no field matches', () => {
    expect(matchQuery(entry, 'monopoly')).toBe(false);
  });

  it('handles entries with null publisher', () => {
    const noPub = baseEntry({ id: '2', gameId: 'g-2', gameTitle: 'X', gamePublisher: null });
    expect(matchQuery(noPub, 'kos')).toBe(false);
  });
});

describe('sortLibraryEntries', () => {
  const entries: readonly UserLibraryEntry[] = [
    baseEntry({
      id: '1',
      gameId: 'g-1',
      gameTitle: 'Wingspan',
      addedAt: '2026-04-29T10:00:00.000Z',
      averageRating: 8.1,
      currentState: 'Wishlist',
    }),
    baseEntry({
      id: '2',
      gameId: 'g-2',
      gameTitle: 'Azul',
      addedAt: '2026-04-30T10:00:00.000Z',
      averageRating: 7.8,
      currentState: 'InPrestito',
    }),
    baseEntry({
      id: '3',
      gameId: 'g-3',
      gameTitle: 'Catan',
      addedAt: '2026-04-28T10:00:00.000Z',
      averageRating: 7.2,
      currentState: 'Owned',
    }),
    baseEntry({
      id: '4',
      gameId: 'g-4',
      gameTitle: 'Brass',
      addedAt: '2026-04-27T10:00:00.000Z',
      averageRating: null,
      currentState: 'Owned',
    }),
  ];

  it('sorts by recent (addedAt DESC) by default', () => {
    const result = sortLibraryEntries(entries, 'recent');
    expect(result.map(e => e.gameTitle)).toEqual(['Azul', 'Wingspan', 'Catan', 'Brass']);
  });

  it('sorts by title alphabetically (case-insensitive)', () => {
    const result = sortLibraryEntries(entries, 'title');
    expect(result.map(e => e.gameTitle)).toEqual(['Azul', 'Brass', 'Catan', 'Wingspan']);
  });

  it('sorts by rating DESC, null last', () => {
    const result = sortLibraryEntries(entries, 'rating');
    expect(result.map(e => e.gameTitle)).toEqual(['Wingspan', 'Azul', 'Catan', 'Brass']);
  });

  it('sorts by state semantically (Owned → Nuovo → InPrestito → Wishlist), tie-break by title', () => {
    const result = sortLibraryEntries(entries, 'state');
    // Owned: Brass, Catan (alpha) → InPrestito: Azul → Wishlist: Wingspan
    expect(result.map(e => e.gameTitle)).toEqual(['Brass', 'Catan', 'Azul', 'Wingspan']);
  });

  it('falls back to recent for unknown sort keys', () => {
    const result = sortLibraryEntries(entries, 'bogus' as unknown as LibrarySortKey);
    expect(result.map(e => e.gameTitle)).toEqual(['Azul', 'Wingspan', 'Catan', 'Brass']);
  });

  it('returns a copy (never mutates input)', () => {
    const result = sortLibraryEntries(entries, 'title');
    expect(result).not.toBe(entries);
  });
});

describe('deriveHeroStats', () => {
  it('counts totalGames, kbReady, wishlist, loaned independently', () => {
    const entries: readonly UserLibraryEntry[] = [
      baseEntry({ id: '1', gameId: 'g-1', gameTitle: 'A', hasKb: true, currentState: 'Owned' }),
      baseEntry({
        id: '2',
        gameId: 'g-2',
        gameTitle: 'B',
        hasKb: true,
        currentState: 'InPrestito',
      }),
      baseEntry({ id: '3', gameId: 'g-3', gameTitle: 'C', currentState: 'Wishlist' }),
      baseEntry({ id: '4', gameId: 'g-4', gameTitle: 'D', currentState: 'Owned' }),
    ];
    expect(deriveHeroStats(entries)).toEqual({
      totalGames: 4,
      kbReady: 2,
      wishlist: 1,
      loaned: 1,
    });
  });

  it('returns zeros for an empty list', () => {
    expect(deriveHeroStats([])).toEqual({
      totalGames: 0,
      kbReady: 0,
      wishlist: 0,
      loaned: 0,
    });
  });
});

describe('deriveLibraryUiState', () => {
  it('returns "default" for healthy populated state', () => {
    expect(
      deriveLibraryUiState({
        isLoading: false,
        error: null,
        totalCount: 5,
        filteredCount: 5,
      })
    ).toBe('default');
  });

  it('returns "loading" when query is in flight', () => {
    expect(
      deriveLibraryUiState({
        isLoading: true,
        error: null,
        totalCount: 0,
        filteredCount: 0,
      })
    ).toBe('loading');
  });

  it('returns "empty" when fetch resolved with zero entries', () => {
    expect(
      deriveLibraryUiState({
        isLoading: false,
        error: null,
        totalCount: 0,
        filteredCount: 0,
      })
    ).toBe('empty');
  });

  it('returns "filtered-empty" when entries exist but filter eliminated all', () => {
    expect(
      deriveLibraryUiState({
        isLoading: false,
        error: null,
        totalCount: 5,
        filteredCount: 0,
      })
    ).toBe('filtered-empty');
  });

  it('returns "error" when error is non-null, regardless of loading flag', () => {
    expect(
      deriveLibraryUiState({
        isLoading: true,
        error: new Error('boom'),
        totalCount: 0,
        filteredCount: 0,
      })
    ).toBe('error');
  });

  it('honours override when provided (visual-test / state-preview hatch)', () => {
    expect(
      deriveLibraryUiState({
        isLoading: false,
        error: null,
        totalCount: 5,
        filteredCount: 5,
        override: 'filtered-empty',
      })
    ).toBe('filtered-empty');
  });
});
