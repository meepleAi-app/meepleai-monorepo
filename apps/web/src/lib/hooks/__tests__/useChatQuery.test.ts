import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useChatQuery } from '../useChatQuery';
import { server } from '@/__tests__/mocks/server';
import { http, HttpResponse } from 'msw';

describe('useChatQuery', () => {
  let fetchSpy: any;

  beforeEach(() => {
    vi.clearAllMocks();
    // Spy on fetch to track calls (MSW handles responses)
    fetchSpy = vi.spyOn(global, 'fetch');
  });

  afterEach(() => {
    fetchSpy?.mockRestore();
  });

  describe('Initial State', () => {
    it('should have correct initial state', () => {
      const { result } = renderHook(() => useChatQuery());
      const [state] = result.current;

      expect(state.state).toBeNull();
      expect(state.currentAnswer).toBe('');
      expect(state.snippets).toEqual([]);
      expect(state.citations).toEqual([]);
      expect(state.confidence).toBeNull();
      expect(state.isLoading).toBe(false);
      expect(state.error).toBeNull();
    });

    it('should provide control functions', () => {
      const { result } = renderHook(() => useChatQuery());
      const [, controls] = result.current;

      expect(controls.askQuestion).toBeDefined();
      expect(controls.reset).toBeDefined();
      expect(typeof controls.askQuestion).toBe('function');
      expect(typeof controls.reset).toBe('function');
    });
  });

  describe('Asking Question', () => {
    it('should set isLoading to true when asking question', async () => {
      // Mock a pending response
      fetchSpy.mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => useChatQuery());
      const [, controls] = result.current;

      act(() => {
        void controls.askQuestion('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isLoading).toBe(true);
      });
    });

    it('should call fetch with correct parameters', async () => {
      fetchSpy.mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          answer: 'Test answer',
          citations: [],
          overallConfidence: 0.85,
        }),
      });

      const { result } = renderHook(() => useChatQuery());
      const [, controls] = result.current;

      await act(async () => {
        await controls.askQuestion('game-456', 'How do I play?');
      });

      expect(fetchSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/knowledge-base/ask'),
        expect.objectContaining({
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            gameId: 'game-456',
            query: 'How do I play?',
            language: 'en',
            bypassCache: false,
          }),
          credentials: 'include',
        })
      );
    });

    it('should handle successful response', async () => {
      const mockResponse = {
        success: true,
        answer: 'You roll the dice to move forward.',
        sources: [{ text: 'Rule page 5', source: 'rulebook', page: 5 }],
        citations: [
          {
            documentId: 'doc-1',
            pageNumber: 5,
            snippet: 'Players roll dice...',
            relevanceScore: 0.92,
          },
        ],
        overallConfidence: 0.88,
      };

      fetchSpy.mockResolvedValue({
        ok: true,
        json: async () => mockResponse,
      });

      const { result } = renderHook(() => useChatQuery());
      const [, controls] = result.current;

      await act(async () => {
        await controls.askQuestion('game-789', 'How do I win?');
      });

      const [state] = result.current;
      expect(state.isLoading).toBe(false);
      expect(state.currentAnswer).toBe('You roll the dice to move forward.');
      expect(state.confidence).toBe(0.88);
      expect(state.citations).toHaveLength(1);
      expect(state.citations[0].documentId).toBe('doc-1');
      expect(state.error).toBeNull();
    });

    it('should handle HTTP error', async () => {
      fetchSpy.mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
      });

      const { result } = renderHook(() => useChatQuery());
      const [, controls] = result.current;

      await act(async () => {
        await controls.askQuestion('game-123', 'test');
      });

      const [state] = result.current;
      expect(state.isLoading).toBe(false);
      expect(state.error).toBeTruthy();
      expect(state.error).toContain('Server error');
    });

    it('should handle network error', async () => {
      fetchSpy.mockRejectedValue(new Error('Network failure'));

      const { result } = renderHook(() => useChatQuery());
      const [, controls] = result.current;

      await act(async () => {
        await controls.askQuestion('game-123', 'test');
      });

      const [state] = result.current;
      expect(state.isLoading).toBe(false);
      expect(state.error).toBe('Network failure');
    });

    it('should call onComplete callback when successful', async () => {
      const onComplete = vi.fn();
      const mockResponse = {
        success: true,
        answer: 'Answer text',
        citations: [{ documentId: 'doc-1', pageNumber: 1, snippet: 'snippet' }],
        overallConfidence: 0.85,
      };

      fetchSpy.mockResolvedValue({
        ok: true,
        json: async () => mockResponse,
      });

      const { result } = renderHook(() => useChatQuery({ onComplete }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.askQuestion('game-123', 'test');
      });

      expect(onComplete).toHaveBeenCalledWith(
        'Answer text',
        expect.arrayContaining([
          expect.objectContaining({ documentId: 'doc-1' }),
        ]),
        expect.objectContaining({ confidence: 0.85 })
      );
    });

    it('should call onError callback when error occurs', async () => {
      const onError = vi.fn();
      fetchSpy.mockRejectedValue(new Error('Test error'));

      const { result } = renderHook(() => useChatQuery({ onError }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.askQuestion('game-123', 'test');
      });

      expect(onError).toHaveBeenCalledWith('Test error');
    });
  });

  describe('Reset', () => {
    it('should reset state to initial values', async () => {
      fetchSpy.mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          answer: 'Answer',
          citations: [],
          overallConfidence: 0.8,
        }),
      });

      const { result } = renderHook(() => useChatQuery());

      // Ask a question
      await act(async () => {
        const [, controls] = result.current;
        await controls.askQuestion('game-123', 'test');
      });

      // Verify state is not initial
      expect(result.current[0].currentAnswer).toBe('Answer');

      // Reset
      act(() => {
        const [, controls] = result.current;
        controls.reset();
      });

      // Verify state is reset
      const [state] = result.current;
      expect(state.state).toBeNull();
      expect(state.currentAnswer).toBe('');
      expect(state.snippets).toEqual([]);
      expect(state.citations).toEqual([]);
      expect(state.confidence).toBeNull();
      expect(state.isLoading).toBe(false);
      expect(state.error).toBeNull();
    });
  });

  describe('Citation Mapping', () => {
    it('should map backend citations correctly', async () => {
      const mockResponse = {
        success: true,
        answer: 'Test answer',
        citations: [
          {
            source: 'Rulebook',
            page: 10,
            text: 'Original snippet',
            score: 0.95,
          },
          {
            documentId: 'doc-2',
            pageNumber: 5,
            snippet: 'Already formatted',
            relevanceScore: 0.88,
          },
        ],
        overallConfidence: 0.9,
      };

      fetchSpy.mockResolvedValue({
        ok: true,
        json: async () => mockResponse,
      });

      const { result } = renderHook(() => useChatQuery());
      const [, controls] = result.current;

      await act(async () => {
        await controls.askQuestion('game-123', 'test');
      });

      const [state] = result.current;
      expect(state.citations).toHaveLength(2);

      // First citation (mapped from old format)
      expect(state.citations[0]).toEqual({
        documentId: 'Rulebook',
        pageNumber: 10,
        snippet: 'Original snippet',
        relevanceScore: 0.95,
      });

      // Second citation (already in new format)
      expect(state.citations[1]).toEqual({
        documentId: 'doc-2',
        pageNumber: 5,
        snippet: 'Already formatted',
        relevanceScore: 0.88,
      });
    });
  });
});
