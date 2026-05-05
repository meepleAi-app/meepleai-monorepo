/**
 * Tests for /games library v2 pure helpers (Issue #633, Wave B.1).
 *
 * Caso B confirmed (UserLibraryEntry lacks playCount/lastPlayedAt/totalSessions).
 * - 'played' filter is a no-op fallback (returns all entries).
 * - 'last-played' sort falls back to `addedAt` DESC.
 *
 * Helpers tested:
 *   - filterByStatus(entries, status)
 *   - matchQuery(entry, query)
 *   - sortLibraryEntries(entries, sortKey)
 *   - deriveStats(entries)
 */

import { describe, it, expect } from 'vitest';

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import {
  type GamesStatusKey,
  type GamesSortKey,
  filterByStatus,
  matchQuery,
  sortLibraryEntries,
  deriveStats,
} from '../library-filters';

const createEntry = (overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry => ({
  id: '00000000-0000-0000-0000-000000000001',
  userId: '00000000-0000-0000-0000-0000000000aa',
  gameId: '00000000-0000-0000-0000-0000000000bb',
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: null,
  addedAt: '2026-04-01T10:00:00.000Z',
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
  averageRating: 7.5,
  privateGameId: null,
  isPrivateGame: false,
  canProposeToCatalog: false,
  ...overrides,
});

describe('filterByStatus', () => {
  const entries: UserLibraryEntry[] = [
    createEntry({ id: 'e1', currentState: 'Owned' }),
    createEntry({ id: 'e2', currentState: 'Wishlist' }),
    createEntry({ id: 'e3', currentState: 'Nuovo' }),
    createEntry({ id: 'e4', currentState: 'InPrestito' }),
  ];

  it("'all' returns every entry", () => {
    expect(filterByStatus(entries, 'all')).toHaveLength(4);
  });

  it("'owned' returns only entries with currentState === 'Owned'", () => {
    const result = filterByStatus(entries, 'owned');
    expect(result).toHaveLength(1);
    expect(result[0]?.id).toBe('e1');
  });

  it("'wishlist' returns only entries with currentState === 'Wishlist'", () => {
    const result = filterByStatus(entries, 'wishlist');
    expect(result).toHaveLength(1);
    expect(result[0]?.id).toBe('e2');
  });

  it("'played' is a no-op fallback under Caso B (returns all entries)", () => {
    // Backend does not expose playCount → "Giocati" filter has no source data.
    // Helper returns all entries; surface UI hides the tab. Behavior must be deterministic
    // so future Caso A migration upgrades this branch without breaking existing tests.
    expect(filterByStatus(entries, 'played')).toHaveLength(4);
  });

  it('returns a new array (no in-place mutation)', () => {
    const result = filterByStatus(entries, 'all');
    expect(result).not.toBe(entries);
  });
});

describe('matchQuery', () => {
  const entry = createEntry({
    gameTitle: 'Terraforming Mars',
    gamePublisher: 'Stronghold Games',
    gameYearPublished: 2016,
  });

  it('matches on title (case-insensitive)', () => {
    expect(matchQuery(entry, 'terraform')).toBe(true);
    expect(matchQuery(entry, 'TERRAFORM')).toBe(true);
  });

  it('matches on publisher (case-insensitive)', () => {
    expect(matchQuery(entry, 'stronghold')).toBe(true);
  });

  it('matches on year as string', () => {
    expect(matchQuery(entry, '2016')).toBe(true);
  });

  it('returns false on miss', () => {
    expect(matchQuery(entry, 'gloomhaven')).toBe(false);
  });

  it('returns true for empty query (no filter)', () => {
    expect(matchQuery(entry, '')).toBe(true);
    expect(matchQuery(entry, '   ')).toBe(true);
  });

  it('handles null publisher and missing year gracefully', () => {
    const minimal = createEntry({
      gameTitle: 'Mystery Game',
      gamePublisher: null,
      gameYearPublished: null,
    });
    expect(matchQuery(minimal, 'mystery')).toBe(true);
    expect(matchQuery(minimal, '1999')).toBe(false);
  });
});

describe('sortLibraryEntries', () => {
  it("'title' sorts ASC, case-insensitive", () => {
    const entries = [
      createEntry({ id: 'a', gameTitle: 'zoo' }),
      createEntry({ id: 'b', gameTitle: 'Apple' }),
      createEntry({ id: 'c', gameTitle: 'banana' }),
    ];
    const result = sortLibraryEntries(entries, 'title');
    expect(result.map(e => e.id)).toEqual(['b', 'c', 'a']);
  });

  it("'rating' sorts DESC, undefined/null averageRating last", () => {
    const entries = [
      createEntry({ id: 'a', averageRating: null }),
      createEntry({ id: 'b', averageRating: 9.1 }),
      createEntry({ id: 'c', averageRating: 5.5 }),
      createEntry({ id: 'd', averageRating: undefined }),
    ];
    const result = sortLibraryEntries(entries, 'rating');
    expect(result.map(e => e.id)).toEqual(['b', 'c', 'a', 'd']);
  });

  it("'year' sorts DESC, undefined/null gameYearPublished last", () => {
    const entries = [
      createEntry({ id: 'a', gameYearPublished: null }),
      createEntry({ id: 'b', gameYearPublished: 2020 }),
      createEntry({ id: 'c', gameYearPublished: 1995 }),
    ];
    const result = sortLibraryEntries(entries, 'year');
    expect(result.map(e => e.id)).toEqual(['b', 'c', 'a']);
  });

  it("'last-played' falls back to addedAt DESC under Caso B", () => {
    const entries = [
      createEntry({ id: 'old', addedAt: '2025-01-01T00:00:00.000Z' }),
      createEntry({ id: 'new', addedAt: '2026-04-15T00:00:00.000Z' }),
      createEntry({ id: 'mid', addedAt: '2026-01-10T00:00:00.000Z' }),
    ];
    const result = sortLibraryEntries(entries, 'last-played');
    expect(result.map(e => e.id)).toEqual(['new', 'mid', 'old']);
  });

  it('returns a new array (no in-place mutation)', () => {
    const entries = [createEntry({ id: 'a' })];
    const result = sortLibraryEntries(entries, 'title');
    expect(result).not.toBe(entries);
  });
});

describe('deriveStats', () => {
  it('returns zero counts on empty input', () => {
    expect(deriveStats([])).toEqual({
      owned: 0,
      wishlist: 0,
      totalEntries: 0,
      kbDocs: 0,
    });
  });

  it('counts owned, wishlist, totalEntries and aggregates kbCardCount into kbDocs', () => {
    const entries = [
      createEntry({ currentState: 'Owned', kbCardCount: 2 }),
      createEntry({ currentState: 'Owned', kbCardCount: 1 }),
      createEntry({ currentState: 'Wishlist', kbCardCount: 0 }),
      createEntry({ currentState: 'Nuovo', kbCardCount: 4 }),
      createEntry({ currentState: 'InPrestito', kbCardCount: 0 }),
    ];
    expect(deriveStats(entries)).toEqual({
      owned: 2,
      wishlist: 1,
      totalEntries: 5,
      kbDocs: 7,
    });
  });
});

describe('type contracts (compile-time)', () => {
  it('GamesStatusKey accepts the four documented values', () => {
    const all: GamesStatusKey = 'all';
    const owned: GamesStatusKey = 'owned';
    const wishlist: GamesStatusKey = 'wishlist';
    const played: GamesStatusKey = 'played';
    expect([all, owned, wishlist, played]).toHaveLength(4);
  });

  it('GamesSortKey accepts the four documented values', () => {
    const lp: GamesSortKey = 'last-played';
    const rating: GamesSortKey = 'rating';
    const title: GamesSortKey = 'title';
    const year: GamesSortKey = 'year';
    expect([lp, rating, title, year]).toHaveLength(4);
  });
});
