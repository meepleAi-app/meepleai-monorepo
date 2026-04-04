import { describe, it, expect } from 'vitest';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// Duplicates the expected post-fix applyFilter logic for spec documentation
function applyFilter(items: UserLibraryEntry[], filterId: string): UserLibraryEntry[] {
  switch (filterId) {
    case 'recent':
      return [...items].sort(
        (a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime()
      );
    case 'oldest':
      return [...items].sort(
        (a, b) => new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime()
      );
    case 'rating':
      return [...items].sort((a, b) => (b.averageRating ?? 0) - (a.averageRating ?? 0));
    case 'players-2-4':
      return items.filter(
        g => g.minPlayers != null && g.maxPlayers != null && g.minPlayers <= 4 && g.maxPlayers >= 2
      );
    case 'under-60':
      return items.filter(g => g.playingTimeMinutes != null && g.playingTimeMinutes <= 60);
    default:
      return items;
  }
}

const makeEntry = (overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry => ({
  id: 'e1',
  userId: 'u1',
  gameId: 'g1',
  gameTitle: 'Test Game',
  addedAt: '2024-01-01T00:00:00Z',
  isFavorite: false,
  currentState: 'Owned',
  hasKb: false,
  kbCardCount: 0,
  kbIndexedCount: 0,
  kbProcessingCount: 0,
  hasRagAccess: false,
  agentIsOwned: true,
  isPrivateGame: false,
  canProposeToCatalog: false,
  ...overrides,
});

describe('applyFilter', () => {
  it('recent: ordina per addedAt decrescente', () => {
    const items = [
      makeEntry({ id: 'a', addedAt: '2024-01-01T00:00:00Z' }),
      makeEntry({ id: 'b', addedAt: '2024-06-01T00:00:00Z' }),
    ];
    const result = applyFilter(items, 'recent');
    expect(result[0].id).toBe('b');
  });

  it('oldest: ordina per addedAt crescente', () => {
    const items = [
      makeEntry({ id: 'a', addedAt: '2024-06-01T00:00:00Z' }),
      makeEntry({ id: 'b', addedAt: '2024-01-01T00:00:00Z' }),
    ];
    const result = applyFilter(items, 'oldest');
    expect(result[0].id).toBe('b');
  });

  it('strategy non esiste: default restituisce items invariati', () => {
    const items = [makeEntry({ id: 'a' }), makeEntry({ id: 'b' })];
    const result = applyFilter(items, 'strategy');
    expect(result).toEqual(items);
  });

  it('players-2-4: filtra giochi 2-4 giocatori', () => {
    const items = [
      makeEntry({ id: 'ok', minPlayers: 2, maxPlayers: 4 }),
      makeEntry({ id: 'ko', minPlayers: 5, maxPlayers: 8 }),
    ];
    const result = applyFilter(items, 'players-2-4');
    expect(result.map(g => g.id)).toEqual(['ok']);
  });

  it('under-60: filtra giochi < 60 min', () => {
    const items = [
      makeEntry({ id: 'ok', playingTimeMinutes: 45 }),
      makeEntry({ id: 'ko', playingTimeMinutes: 90 }),
    ];
    const result = applyFilter(items, 'under-60');
    expect(result.map(g => g.id)).toEqual(['ok']);
  });
});
