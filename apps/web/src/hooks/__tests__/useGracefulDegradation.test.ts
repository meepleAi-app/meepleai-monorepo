import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useGracefulDegradation } from '../useGracefulDegradation';

// ============================================================================
// Mock rules-cache and faq-search
// ============================================================================

const mockGetCachedAnalyses = vi.fn();
const mockSearchFaqs = vi.fn();
const mockSearchCommonQuestions = vi.fn();
const mockSuggestSection = vi.fn();

vi.mock('@/lib/game-night/rules-cache', () => ({
  getCachedAnalyses: (...args: unknown[]) => mockGetCachedAnalyses(...args),
}));

vi.mock('@/lib/game-night/faq-search', () => ({
  searchFaqs: (...args: unknown[]) => mockSearchFaqs(...args),
  searchCommonQuestions: (...args: unknown[]) => mockSearchCommonQuestions(...args),
  suggestSection: (...args: unknown[]) => mockSuggestSection(...args),
}));

beforeEach(() => {
  vi.clearAllMocks();
  mockSearchFaqs.mockReturnValue([]);
  mockSearchCommonQuestions.mockReturnValue([]);
  mockSuggestSection.mockReturnValue(null);
});

// ============================================================================
// Tests
// ============================================================================

describe('useGracefulDegradation', () => {
  const GAME_ID = 'test-game-id';

  describe('initial state', () => {
    it('starts with LLM available', () => {
      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      expect(result.current.isLlmAvailable).toBe(true);
      expect(result.current.consecutiveErrors).toBe(0);
      expect(result.current.lastResponseSource).toBe('live');
      expect(result.current.isCircuitBroken).toBe(false);
    });
  });

  describe('error tracking', () => {
    it('records errors and updates availability', () => {
      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      act(() => result.current.recordError());
      expect(result.current.consecutiveErrors).toBe(1);
      expect(result.current.isLlmAvailable).toBe(true);

      act(() => result.current.recordError());
      expect(result.current.consecutiveErrors).toBe(2);
      expect(result.current.isLlmAvailable).toBe(false);
    });

    it('trips circuit breaker after 3 errors', () => {
      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      act(() => result.current.recordError());
      act(() => result.current.recordError());
      act(() => result.current.recordError());

      expect(result.current.isCircuitBroken).toBe(true);
    });

    it('resets errors on success', () => {
      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      act(() => result.current.recordError());
      act(() => result.current.recordError());
      expect(result.current.isLlmAvailable).toBe(false);

      act(() => result.current.recordSuccess());
      expect(result.current.consecutiveErrors).toBe(0);
      expect(result.current.isLlmAvailable).toBe(true);
      expect(result.current.lastResponseSource).toBe('live');
    });
  });

  describe('tryFallback', () => {
    it('returns null when no gameId', () => {
      const { result } = renderHook(() => useGracefulDegradation(null));

      let fallback: ReturnType<typeof result.current.tryFallback>;
      act(() => {
        fallback = result.current.tryFallback('test query');
      });
      expect(fallback!).toBeNull();
    });

    it('returns offline response when no cache exists', () => {
      mockGetCachedAnalyses.mockReturnValue(null);

      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      let fallback: ReturnType<typeof result.current.tryFallback>;
      act(() => {
        fallback = result.current.tryFallback('how do I win?');
      });

      expect(fallback!).not.toBeNull();
      expect(fallback!.source).toBe('offline');
      expect(fallback!.confidence).toBe(0);
    });

    it('returns cached FAQ answer when match found', () => {
      mockGetCachedAnalyses.mockReturnValue({
        analyses: [
          {
            generatedFaqs: [
              {
                question: 'How do I win?',
                answer: 'Get 10 victory points.',
                sourceSection: 'Victory',
                confidence: 0.95,
                tags: ['victory'],
              },
            ],
            commonQuestions: [],
            gamePhases: [],
          },
        ],
      });

      mockSearchFaqs.mockReturnValue([
        {
          faq: {
            question: 'How do I win?',
            answer: 'Get 10 victory points.',
            sourceSection: 'Victory',
            confidence: 0.95,
            tags: ['victory'],
          },
          score: 0.8,
          matchType: 'exact',
        },
      ]);

      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      let fallback: ReturnType<typeof result.current.tryFallback>;
      act(() => {
        fallback = result.current.tryFallback('how do I win?');
      });

      expect(fallback!.source).toBe('cached');
      expect(fallback!.answer).toBe('Get 10 victory points.');
      expect(fallback!.confidence).toBe(0.8);
      expect(result.current.lastResponseSource).toBe('cached');
    });

    it('falls back to common questions when no FAQ match', () => {
      mockGetCachedAnalyses.mockReturnValue({
        analyses: [
          {
            generatedFaqs: [],
            commonQuestions: ['How many roads can I build?'],
            gamePhases: [],
          },
        ],
      });

      mockSearchFaqs.mockReturnValue([]);
      mockSearchCommonQuestions.mockReturnValue([
        { question: 'How many roads can I build?', score: 0.6 },
      ]);

      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      let fallback: ReturnType<typeof result.current.tryFallback>;
      act(() => {
        fallback = result.current.tryFallback('roads per turn');
      });

      expect(fallback!.source).toBe('cached');
      expect(fallback!.answer).toContain('How many roads can I build?');
    });

    it('suggests section when no FAQ or common question matches', () => {
      mockGetCachedAnalyses.mockReturnValue({
        analyses: [
          {
            generatedFaqs: [],
            commonQuestions: [],
            gamePhases: [{ name: 'Trading Phase', description: 'Trade resources', order: 2 }],
          },
        ],
      });

      mockSearchFaqs.mockReturnValue([]);
      mockSearchCommonQuestions.mockReturnValue([]);
      mockSuggestSection.mockReturnValue('Trading Phase');

      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      let fallback: ReturnType<typeof result.current.tryFallback>;
      act(() => {
        fallback = result.current.tryFallback('how to trade');
      });

      expect(fallback!.source).toBe('offline');
      expect(fallback!.suggestedSection).toBe('Trading Phase');
      expect(fallback!.answer).toContain('Trading Phase');
    });

    it('returns generic offline message when no section suggestion', () => {
      mockGetCachedAnalyses.mockReturnValue({
        analyses: [
          {
            generatedFaqs: [],
            commonQuestions: [],
            gamePhases: [],
          },
        ],
      });

      mockSearchFaqs.mockReturnValue([]);
      mockSearchCommonQuestions.mockReturnValue([]);
      mockSuggestSection.mockReturnValue(null);

      const { result } = renderHook(() => useGracefulDegradation(GAME_ID));

      let fallback: ReturnType<typeof result.current.tryFallback>;
      act(() => {
        fallback = result.current.tryFallback('something obscure');
      });

      expect(fallback!.source).toBe('offline');
      expect(fallback!.answer).toContain('search manually');
    });
  });
});
