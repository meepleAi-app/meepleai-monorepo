import { describe, expect, it } from 'vitest';

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import {
  agentToHubItem,
  kbDocToHubItem,
  libraryEntryToHubItem,
  sessionToHubItem,
  type KbDoc,
} from '../hybrid-hub.mappers';

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

const baseAgent: AgentDto = {
  id: '00000000-0000-0000-0000-0000000000b1',
  name: 'Catan Tutor',
  type: 'Tutor',
  strategyName: 'HybridSearch',
  strategyParameters: {},
  isActive: true,
  createdAt: '2026-01-15T09:00:00Z',
  lastInvokedAt: '2026-03-12T18:30:00Z',
  invocationCount: 47,
  isRecentlyUsed: true,
  isIdle: false,
  gameId: '00000000-0000-0000-0000-0000000000aa',
  gameName: 'Catan',
  createdByUserId: '00000000-0000-0000-0000-000000000099',
};

describe('agentToHubItem', () => {
  it('maps an AgentDto to an AgentHubItem with entity="agent"', () => {
    const result = agentToHubItem(baseAgent);
    expect(result.entity).toBe('agent');
    expect(result.id).toBe(baseAgent.id);
    expect(result.title).toBe('Catan Tutor');
    expect(result.subtitle).toBe('Catan');
    expect(result.gameName).toBe('Catan');
    expect(result.agentType).toBe('Tutor');
    expect(result.isActive).toBe(true);
    expect(result.href).toBe(`/agents/${baseAgent.id}`);
  });

  it('prefers lastInvokedAt over createdAt for updatedAt', () => {
    const result = agentToHubItem(baseAgent);
    expect(result.updatedAt).toBe('2026-03-12T18:30:00Z');
  });

  it('falls back to createdAt when lastInvokedAt is null', () => {
    const result = agentToHubItem({ ...baseAgent, lastInvokedAt: null });
    expect(result.updatedAt).toBe('2026-01-15T09:00:00Z');
  });

  it('returns undefined gameName/subtitle when the agent is not game-bound', () => {
    const result = agentToHubItem({ ...baseAgent, gameName: null, gameId: null });
    expect(result.gameName).toBeUndefined();
    expect(result.subtitle).toBeUndefined();
  });
});

const baseKbDoc: KbDoc = {
  id: '00000000-0000-0000-0000-0000000000c1',
  gameId: '00000000-0000-0000-0000-0000000000aa',
  gameName: 'Catan',
  fileName: 'catan-rulebook-en.pdf',
  processingState: 'Ready',
  pageCount: 24,
  processedAt: '2026-02-15T11:20:00Z',
  updatedAt: '2026-02-15T11:20:00Z',
};

describe('kbDocToHubItem', () => {
  it('maps a KbDoc to a KbHubItem with entity="kb"', () => {
    const result = kbDocToHubItem(baseKbDoc);
    expect(result.entity).toBe('kb');
    expect(result.id).toBe(baseKbDoc.id);
    expect(result.title).toBe('catan-rulebook-en.pdf');
    expect(result.subtitle).toBe('Catan');
    expect(result.gameName).toBe('Catan');
    expect(result.processingState).toBe('Ready');
    expect(result.pageCount).toBe(24);
    expect(result.updatedAt).toBe('2026-02-15T11:20:00Z');
    expect(result.href).toBe(`/knowledge-base/${baseKbDoc.id}`);
  });

  it('returns undefined gameName/subtitle when the doc is not game-attached', () => {
    const result = kbDocToHubItem({ ...baseKbDoc, gameName: null, gameId: null });
    expect(result.gameName).toBeUndefined();
    expect(result.subtitle).toBeUndefined();
  });

  it('returns undefined pageCount when the field is absent', () => {
    const result = kbDocToHubItem({ ...baseKbDoc, pageCount: null });
    expect(result.pageCount).toBeUndefined();
  });
});

const baseSession: GameSessionDto = {
  id: '00000000-0000-0000-0000-0000000000d1',
  gameId: '00000000-0000-0000-0000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-04-20T19:00:00Z',
  completedAt: '2026-04-20T21:30:00Z',
  playerCount: 4,
  players: [],
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 150,
};

describe('sessionToHubItem', () => {
  it('maps a GameSessionDto to a SessionHubItem with entity="session"', () => {
    const result = sessionToHubItem(baseSession);
    expect(result.entity).toBe('session');
    expect(result.id).toBe(baseSession.id);
    expect(result.status).toBe('Completed');
    expect(result.playerCount).toBe(4);
    expect(result.subtitle).toBe('Alice');
    expect(result.href).toBe(`/sessions/${baseSession.id}`);
  });

  it('leaves gameName undefined (DTO does not include it; Phase 2 enrich)', () => {
    const result = sessionToHubItem(baseSession);
    expect(result.gameName).toBeUndefined();
  });

  it('prefers completedAt over startedAt for updatedAt', () => {
    const result = sessionToHubItem(baseSession);
    expect(result.updatedAt).toBe('2026-04-20T21:30:00Z');
  });

  it('falls back to startedAt when completedAt is null', () => {
    const result = sessionToHubItem({ ...baseSession, completedAt: null });
    expect(result.updatedAt).toBe('2026-04-20T19:00:00Z');
  });

  it('uses a short-id fallback title (Phase 2 will enrich with game name)', () => {
    const result = sessionToHubItem(baseSession);
    expect(result.title).toBe('Session 00000000');
  });

  it('returns undefined subtitle when there is no winner', () => {
    const result = sessionToHubItem({ ...baseSession, winnerName: null });
    expect(result.subtitle).toBeUndefined();
  });
});
