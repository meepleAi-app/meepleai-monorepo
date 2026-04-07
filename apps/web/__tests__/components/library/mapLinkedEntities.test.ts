import { describe, expect, it } from 'vitest';

import { mapLibraryEntryToLinkedEntities } from '@/components/library/mapLinkedEntities';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

function makeEntry(overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry {
  return {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    userId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    gameId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    gameTitle: 'Test Game',
    addedAt: '2026-01-01T00:00:00Z',
    isFavorite: false,
    currentState: 'Owned' as const,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    hasRagAccess: false,
    agentIsOwned: true,
    isPrivateGame: false,
    canProposeToCatalog: false,
    ...overrides,
  } as UserLibraryEntry;
}

describe('mapLibraryEntryToLinkedEntities', () => {
  it('should always include game pip', () => {
    const result = mapLibraryEntryToLinkedEntities(makeEntry());
    expect(result.find(e => e.entityType === 'game')).toEqual({ entityType: 'game', count: 1 });
  });

  it('should NOT include kb pip when kbIndexedCount is 0', () => {
    const result = mapLibraryEntryToLinkedEntities(makeEntry({ kbIndexedCount: 0 }));
    expect(result.find(e => e.entityType === 'kb')).toBeUndefined();
  });

  it('should include kb pip with count when kbIndexedCount > 0', () => {
    const result = mapLibraryEntryToLinkedEntities(makeEntry({ hasKb: true, kbIndexedCount: 3 }));
    expect(result.find(e => e.entityType === 'kb')).toEqual({ entityType: 'kb', count: 3 });
  });

  it('should return game + kb when KB exists (2 elements)', () => {
    const result = mapLibraryEntryToLinkedEntities(makeEntry({ hasKb: true, kbIndexedCount: 1 }));
    expect(result).toHaveLength(2);
  });

  it('should return only game when no KB (1 element)', () => {
    const result = mapLibraryEntryToLinkedEntities(makeEntry());
    expect(result).toHaveLength(1);
  });
});
