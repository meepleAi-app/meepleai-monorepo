import { describe, expect, it } from 'vitest';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// Logica di filtering per segmenti mobile (post-fix)
function filterBySegment(items: UserLibraryEntry[], segment: string): UserLibraryEntry[] {
  switch (segment) {
    case 'collection':
      return items.filter(g => !g.isPrivateGame && g.currentState !== 'Wishlist');
    case 'private':
      return items.filter(g => g.isPrivateGame);
    case 'wishlist':
      return items.filter(g => g.currentState === 'Wishlist');
    default:
      return items;
  }
}

const makeEntry = (overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry => ({
  id: 'e1',
  userId: 'u1',
  gameId: 'g1',
  gameTitle: 'Test',
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

describe('filterBySegment', () => {
  const items: UserLibraryEntry[] = [
    makeEntry({ id: 'catalog-owned', isPrivateGame: false, currentState: 'Owned' }),
    makeEntry({ id: 'catalog-nuovo', isPrivateGame: false, currentState: 'Nuovo' }),
    makeEntry({ id: 'catalog-wishlist', isPrivateGame: false, currentState: 'Wishlist' }),
    makeEntry({ id: 'private-owned', isPrivateGame: true, currentState: 'Owned' }),
    makeEntry({ id: 'private-nuovo', isPrivateGame: true, currentState: 'Nuovo' }),
  ];

  it('collection: giochi catalogo (non privati, non wishlist)', () => {
    const result = filterBySegment(items, 'collection');
    expect(result.map(g => g.id)).toEqual(['catalog-owned', 'catalog-nuovo']);
  });

  it('private: solo giochi privati (isPrivateGame=true), qualsiasi stato', () => {
    const result = filterBySegment(items, 'private');
    expect(result.map(g => g.id)).toEqual(['private-owned', 'private-nuovo']);
  });

  it('wishlist: solo giochi in stato Wishlist', () => {
    const result = filterBySegment(items, 'wishlist');
    expect(result.map(g => g.id)).toEqual(['catalog-wishlist']);
  });
});
