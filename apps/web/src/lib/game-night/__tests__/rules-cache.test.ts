import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  cacheRulebookAnalyses,
  getCachedAnalyses,
  isGameCached,
  removeCachedGame,
  clearAllCachedRules,
  pruneExpiredEntries,
  getCacheStats,
} from '../rules-cache';

import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';

// ============================================================================
// localStorage Mock
// ============================================================================

const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
    get length() {
      return Object.keys(store).length;
    },
    key: vi.fn((i: number) => Object.keys(store)[i] ?? null),
  };
})();

Object.defineProperty(window, 'localStorage', { value: localStorageMock });

beforeEach(() => {
  localStorageMock.clear();
  vi.clearAllMocks();
});

// ============================================================================
// Test Data
// ============================================================================

function createMockAnalysis(overrides: Partial<RulebookAnalysisDto> = {}): RulebookAnalysisDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    sharedGameId: 'game-001',
    pdfDocumentId: '00000000-0000-0000-0000-000000000002',
    gameTitle: 'Catan',
    summary: 'A game about trading and building settlements.',
    keyMechanics: ['Trading', 'Dice Rolling', 'Resource Management'],
    victoryConditions: {
      primary: 'First to 10 victory points',
      alternatives: ['Longest Road', 'Largest Army'],
      isPointBased: true,
      targetPoints: 10,
    },
    resources: [
      { name: 'Brick', type: 'basic', usage: 'Building roads and settlements', isLimited: false },
    ],
    gamePhases: [
      { name: 'Setup', description: 'Place initial settlements', order: 1, isOptional: false },
      { name: 'Main Game', description: 'Roll, trade, build', order: 2, isOptional: false },
    ],
    commonQuestions: ['How many roads can I build per turn?', 'Can I trade with the bank?'],
    confidenceScore: 0.92,
    version: 1,
    isActive: true,
    source: '00000000-0000-0000-0000-000000000003',
    analyzedAt: '2026-01-15T10:00:00Z',
    createdBy: '00000000-0000-0000-0000-000000000004',
    keyConcepts: [
      { term: 'Settlement', definition: 'A building worth 1 VP', category: 'structures' },
    ],
    generatedFaqs: [
      {
        question: 'How many resources do I get when I build a settlement?',
        answer:
          'A settlement produces one resource card for each adjacent terrain hex when that hex number is rolled.',
        sourceSection: 'Setup Phase',
        confidence: 0.88,
        tags: ['settlement', 'resources'],
      },
    ],
    gameStateSchemaJson: null,
    completionStatus: 'Complete',
    missingSections: null,
    ...overrides,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('rules-cache', () => {
  const GAME_ID = '550e8400-e29b-41d4-a716-446655440000';

  describe('cacheRulebookAnalyses', () => {
    it('stores analyses in localStorage', () => {
      const analyses = [createMockAnalysis()];
      const result = cacheRulebookAnalyses(GAME_ID, analyses);

      expect(result).toBe(true);
      expect(localStorageMock.setItem).toHaveBeenCalled();
    });

    it('returns false for empty analyses', () => {
      const result = cacheRulebookAnalyses(GAME_ID, []);
      expect(result).toBe(false);
    });

    it('stores entry with correct structure', () => {
      const analyses = [createMockAnalysis({ gameTitle: 'Test Game' })];
      cacheRulebookAnalyses(GAME_ID, analyses);

      const raw = localStorageMock.getItem(`rules-cache:${GAME_ID}`);
      expect(raw).not.toBeNull();

      const parsed = JSON.parse(raw!);
      expect(parsed.analyses).toHaveLength(1);
      expect(parsed.gameTitle).toBe('Test Game');
      expect(parsed.cachedAt).toBeDefined();
      expect(parsed.expiresAt).toBeDefined();
    });

    it('updates the cache index', () => {
      const analyses = [createMockAnalysis()];
      cacheRulebookAnalyses(GAME_ID, analyses);

      const indexRaw = localStorageMock.getItem('rules-cache:__index__');
      expect(indexRaw).not.toBeNull();

      const index = JSON.parse(indexRaw!);
      expect(index.entries[GAME_ID]).toBeDefined();
      expect(index.entries[GAME_ID].gameTitle).toBe('Catan');
    });

    it('respects custom TTL', () => {
      const oneHour = 60 * 60 * 1000;
      const analyses = [createMockAnalysis()];
      cacheRulebookAnalyses(GAME_ID, analyses, oneHour);

      const raw = localStorageMock.getItem(`rules-cache:${GAME_ID}`);
      const parsed = JSON.parse(raw!);

      const cachedAt = new Date(parsed.cachedAt).getTime();
      const expiresAt = new Date(parsed.expiresAt).getTime();
      expect(expiresAt - cachedAt).toBe(oneHour);
    });

    it('returns false when entry exceeds max size', () => {
      // Create a very large analysis
      const bigFaqs = Array.from({ length: 5000 }, (_, i) => ({
        question: `Q${i}: ${'x'.repeat(500)}`,
        answer: `A${i}: ${'y'.repeat(500)}`,
        sourceSection: 'Section',
        confidence: 0.9,
        tags: ['tag1'],
      }));

      const analyses = [createMockAnalysis({ generatedFaqs: bigFaqs })];
      const result = cacheRulebookAnalyses(GAME_ID, analyses);
      expect(result).toBe(false);
    });
  });

  describe('getCachedAnalyses', () => {
    it('returns cached entry', () => {
      const analyses = [createMockAnalysis()];
      cacheRulebookAnalyses(GAME_ID, analyses);

      const result = getCachedAnalyses(GAME_ID);
      expect(result).not.toBeNull();
      expect(result!.analyses).toHaveLength(1);
      expect(result!.gameTitle).toBe('Catan');
    });

    it('returns null for non-existent game', () => {
      const result = getCachedAnalyses('non-existent');
      expect(result).toBeNull();
    });

    it('returns null and removes expired entries', () => {
      // Store with expired TTL
      const expiredEntry = {
        analyses: [createMockAnalysis()],
        cachedAt: '2020-01-01T00:00:00Z',
        expiresAt: '2020-01-02T00:00:00Z',
        gameTitle: 'Expired Game',
      };
      localStorageMock.setItem(`rules-cache:${GAME_ID}`, JSON.stringify(expiredEntry));

      const result = getCachedAnalyses(GAME_ID);
      expect(result).toBeNull();
      // Entry should be cleaned up
      expect(localStorageMock.removeItem).toHaveBeenCalledWith(`rules-cache:${GAME_ID}`);
    });

    it('handles corrupted JSON gracefully', () => {
      localStorageMock.setItem(`rules-cache:${GAME_ID}`, 'not-valid-json{{{');

      const result = getCachedAnalyses(GAME_ID);
      expect(result).toBeNull();
    });
  });

  describe('isGameCached', () => {
    it('returns true for cached game', () => {
      cacheRulebookAnalyses(GAME_ID, [createMockAnalysis()]);
      expect(isGameCached(GAME_ID)).toBe(true);
    });

    it('returns false for uncached game', () => {
      expect(isGameCached('not-cached')).toBe(false);
    });
  });

  describe('removeCachedGame', () => {
    it('removes game from cache and index', () => {
      cacheRulebookAnalyses(GAME_ID, [createMockAnalysis()]);
      expect(isGameCached(GAME_ID)).toBe(true);

      removeCachedGame(GAME_ID);
      expect(isGameCached(GAME_ID)).toBe(false);
    });
  });

  describe('clearAllCachedRules', () => {
    it('removes all cached games', () => {
      cacheRulebookAnalyses('game-a', [createMockAnalysis()]);
      cacheRulebookAnalyses('game-b', [createMockAnalysis()]);

      clearAllCachedRules();

      expect(isGameCached('game-a')).toBe(false);
      expect(isGameCached('game-b')).toBe(false);
    });
  });

  describe('pruneExpiredEntries', () => {
    it('removes expired entries and returns count', () => {
      // Store two entries: one expired, one valid
      const expiredEntry = {
        analyses: [createMockAnalysis()],
        cachedAt: '2020-01-01T00:00:00Z',
        expiresAt: '2020-01-02T00:00:00Z',
        gameTitle: 'Expired',
      };
      const validEntry = {
        analyses: [createMockAnalysis()],
        cachedAt: new Date().toISOString(),
        expiresAt: new Date(Date.now() + 86400000).toISOString(),
        gameTitle: 'Valid',
      };

      localStorageMock.setItem('rules-cache:expired-game', JSON.stringify(expiredEntry));
      localStorageMock.setItem('rules-cache:valid-game', JSON.stringify(validEntry));
      localStorageMock.setItem(
        'rules-cache:__index__',
        JSON.stringify({
          entries: {
            'expired-game': {
              cachedAt: expiredEntry.cachedAt,
              expiresAt: expiredEntry.expiresAt,
              gameTitle: 'Expired',
            },
            'valid-game': {
              cachedAt: validEntry.cachedAt,
              expiresAt: validEntry.expiresAt,
              gameTitle: 'Valid',
            },
          },
        })
      );

      const pruned = pruneExpiredEntries();
      expect(pruned).toBe(1);
    });
  });

  describe('getCacheStats', () => {
    it('returns stats for cached games', () => {
      cacheRulebookAnalyses('game-a', [createMockAnalysis({ gameTitle: 'Game A' })]);
      cacheRulebookAnalyses('game-b', [createMockAnalysis({ gameTitle: 'Game B' })]);

      const stats = getCacheStats();
      expect(stats.cachedGameCount).toBe(2);
      expect(stats.approximateSizeBytes).toBeGreaterThan(0);
      expect(stats.cachedGames).toHaveLength(2);
    });

    it('returns empty stats when no games cached', () => {
      const stats = getCacheStats();
      expect(stats.cachedGameCount).toBe(0);
      expect(stats.approximateSizeBytes).toBe(0);
      expect(stats.cachedGames).toHaveLength(0);
    });
  });
});
