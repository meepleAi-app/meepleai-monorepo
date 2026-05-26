import { describe, expect, it } from 'vitest';

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import { libraryEntryToHubItem } from '../hybrid-hub.mappers';

const baseEntry: UserLibraryEntry = {
  id: '00000000-0000-0000-0000-000000000001',
  userId: '00000000-0000-0000-0000-000000000099',
  gameId: '00000000-0000-0000-0000-0000000000aa',
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: 'https://example.test/catan.jpg',
  addedAt: '2026-01-10T12:00:00Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: '2026-02-01T08:00:00Z',
  stateNotes: null,
  hasKb: true,
  kbCardCount: 1,
  kbIndexedCount: 1,
  kbProcessingCount: 0,
  ownershipDeclaredAt: null,
  hasRagAccess: true,
  agentIsOwned: true,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  complexityRating: 2.3,
  averageRating: 7.2,
  privateGameId: null,
  isPrivateGame: false,
  canProposeToCatalog: false,
};

describe('libraryEntryToHubItem', () => {
  it('maps a UserLibraryEntry to a GameHubItem with entity="game"', () => {
    const result = libraryEntryToHubItem(baseEntry);
    expect(result.entity).toBe('game');
    expect(result.id).toBe(baseEntry.id);
    expect(result.gameId).toBe(baseEntry.gameId);
    expect(result.title).toBe('Catan');
    expect(result.subtitle).toBe('Kosmos');
    expect(result.rating).toBe(7.2);
    expect(result.state).toBe('Owned');
    expect(result.imageUrl).toBe('https://example.test/catan.jpg');
    expect(result.href).toBe(`/library/${baseEntry.gameId}`);
  });

  it('prefers stateChangedAt over addedAt for updatedAt', () => {
    const result = libraryEntryToHubItem(baseEntry);
    expect(result.updatedAt).toBe('2026-02-01T08:00:00Z');
  });

  it('falls back to addedAt when stateChangedAt is null', () => {
    const result = libraryEntryToHubItem({ ...baseEntry, stateChangedAt: null });
    expect(result.updatedAt).toBe('2026-01-10T12:00:00Z');
  });

  it('falls back to gameIconUrl when gameImageUrl is null', () => {
    const result = libraryEntryToHubItem({
      ...baseEntry,
      gameImageUrl: null,
      gameIconUrl: 'https://example.test/catan-icon.png',
    });
    expect(result.imageUrl).toBe('https://example.test/catan-icon.png');
  });

  it('returns undefined imageUrl when both image and icon are null', () => {
    const result = libraryEntryToHubItem({
      ...baseEntry,
      gameImageUrl: null,
      gameIconUrl: null,
    });
    expect(result.imageUrl).toBeUndefined();
  });

  it('returns undefined subtitle when gamePublisher is null', () => {
    const result = libraryEntryToHubItem({ ...baseEntry, gamePublisher: null });
    expect(result.subtitle).toBeUndefined();
  });

  it('returns undefined rating when averageRating is null', () => {
    const result = libraryEntryToHubItem({ ...baseEntry, averageRating: null });
    expect(result.rating).toBeUndefined();
  });
});
