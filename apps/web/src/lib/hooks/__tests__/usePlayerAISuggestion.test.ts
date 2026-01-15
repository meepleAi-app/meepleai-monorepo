/**
 * Tests for usePlayerAISuggestion hook
 * Issue #2421: Player Mode UI Controls
 */

import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';

import { usePlayerAISuggestion } from '../usePlayerAISuggestion';

import type { PlayerModeSuggestionResponse, SuggestedMove } from '@/lib/api/schemas';

// Mock createApiClient
vi.mock('@/lib/api', () => ({
  createApiClient: vi.fn(),
}));

// Import after mock to get mocked version
import { createApiClient } from '@/lib/api';

describe('usePlayerAISuggestion', () => {
  const mockSuggestPlayerMove = vi.fn();
  const mockGameState = { players: [{ id: '1', name: 'Alice' }] };

  beforeEach(() => {
    vi.clearAllMocks();

    // Setup mock API client
    (createApiClient as any).mockReturnValue({
      agents: {
        suggestPlayerMove: mockSuggestPlayerMove,
      },
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Initial State', () => {
    it('should have correct initial state', () => {
      const { result } = renderHook(() => usePlayerAISuggestion());
      const [state] = result.current;

      expect(state.isLoading).toBe(false);
      expect(state.error).toBeNull();
      expect(state.suggestion).toBeNull();
      expect(state.alternatives).toEqual([]);
      expect(state.confidence).toBeNull();
      expect(state.strategicContext).toBeNull();
      expect(state.processingTimeMs).toBeNull();
    });

    it('should provide control functions', () => {
      const { result } = renderHook(() => usePlayerAISuggestion());
      const [, controls] = result.current;

      expect(controls.suggestMove).toBeDefined();
      expect(controls.applySuggestion).toBeDefined();
      expect(controls.ignoreSuggestion).toBeDefined();
      expect(controls.reset).toBeDefined();
      expect(typeof controls.suggestMove).toBe('function');
      expect(typeof controls.applySuggestion).toBe('function');
      expect(typeof controls.ignoreSuggestion).toBe('function');
      expect(typeof controls.reset).toBe('function');
    });
  });

  describe('Suggest Move', () => {
    const mockResponse: PlayerModeSuggestionResponse = {
      primarySuggestion: {
        action: 'Place resource token on space 5',
        rationale: 'This maximizes your resource generation',
        expectedOutcome: 'Gain 2 wood per turn',
        confidence: 0.85,
      },
      alternativeMoves: [
        {
          action: 'Build settlement',
          rationale: 'Secure territory',
          confidence: 0.7,
        },
      ],
      overallConfidence: 0.85,
      strategicContext: 'Focus on resource generation early game',
      sources: [],
      promptTokens: 100,
      completionTokens: 50,
      totalTokens: 150,
      processingTimeMs: 1200,
      metadata: null,
    };

    it('should set isLoading to true when requesting suggestion', async () => {
      // Mock pending promise
      mockSuggestPlayerMove.mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => usePlayerAISuggestion());
      const [, controls] = result.current;

      act(() => {
        void controls.suggestMove('game-123', mockGameState);
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isLoading).toBe(true);
      });
    });

    it('should call API with correct parameters', async () => {
      mockSuggestPlayerMove.mockResolvedValue(mockResponse);

      const { result } = renderHook(() => usePlayerAISuggestion());
      const [, controls] = result.current;

      await act(async () => {
        await controls.suggestMove('game-456', mockGameState, 'Should I focus on wood?');
      });

      expect(mockSuggestPlayerMove).toHaveBeenCalledWith({
        gameId: 'game-456',
        gameState: mockGameState,
        query: 'Should I focus on wood?',
      });
    });

    it('should handle successful response', async () => {
      mockSuggestPlayerMove.mockResolvedValue(mockResponse);

      const { result } = renderHook(() => usePlayerAISuggestion());
      const [, controls] = result.current;

      await act(async () => {
        await controls.suggestMove('game-123', mockGameState);
      });

      const [state] = result.current;
      expect(state.isLoading).toBe(false);
      expect(state.error).toBeNull();
      expect(state.suggestion).toEqual(mockResponse.primarySuggestion);
      expect(state.alternatives).toEqual(mockResponse.alternativeMoves);
      expect(state.confidence).toBe(0.85);
      expect(state.strategicContext).toBe('Focus on resource generation early game');
      expect(state.processingTimeMs).toBe(1200);
    });

    it('should call onSuggestionReceived callback on success', async () => {
      mockSuggestPlayerMove.mockResolvedValue(mockResponse);
      const onSuggestionReceived = vi.fn();

      const { result } = renderHook(() => usePlayerAISuggestion({ onSuggestionReceived }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.suggestMove('game-123', mockGameState);
      });

      expect(onSuggestionReceived).toHaveBeenCalledWith(mockResponse.primarySuggestion, 0.85);
    });

    it('should handle API error', async () => {
      const errorMessage = 'Backend endpoint not implemented';
      mockSuggestPlayerMove.mockRejectedValue(new Error(errorMessage));

      const { result } = renderHook(() => usePlayerAISuggestion());
      const [, controls] = result.current;

      await act(async () => {
        await controls.suggestMove('game-123', mockGameState);
      });

      const [state] = result.current;
      expect(state.isLoading).toBe(false);
      expect(state.error).toBe(errorMessage);
      expect(state.suggestion).toBeNull();
    });

    it('should call onError callback on failure', async () => {
      const errorMessage = 'Network error';
      mockSuggestPlayerMove.mockRejectedValue(new Error(errorMessage));
      const onError = vi.fn();

      const { result } = renderHook(() => usePlayerAISuggestion({ onError }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.suggestMove('game-123', mockGameState);
      });

      expect(onError).toHaveBeenCalledWith(errorMessage);
    });

    it('should handle non-Error exceptions', async () => {
      mockSuggestPlayerMove.mockRejectedValue('String error');

      const { result } = renderHook(() => usePlayerAISuggestion());
      const [, controls] = result.current;

      await act(async () => {
        await controls.suggestMove('game-123', mockGameState);
      });

      const [state] = result.current;
      expect(state.error).toContain('Failed to get AI suggestion');
    });
  });

  describe('Apply Suggestion', () => {
    const mockResponse: PlayerModeSuggestionResponse = {
      primarySuggestion: {
        action: 'Place token',
        rationale: 'Best move',
        confidence: 0.9,
      },
      overallConfidence: 0.9,
      promptTokens: 50,
      completionTokens: 25,
      totalTokens: 75,
      processingTimeMs: 500,
      metadata: null,
    };

    it('should call onSuggestionApplied callback', async () => {
      mockSuggestPlayerMove.mockResolvedValue(mockResponse);
      const onSuggestionApplied = vi.fn();

      const { result } = renderHook(() => usePlayerAISuggestion({ onSuggestionApplied }));

      // First get suggestion
      await act(async () => {
        await result.current[1].suggestMove('game-123', mockGameState);
      });

      // Then apply it
      act(() => {
        result.current[1].applySuggestion();
      });

      expect(onSuggestionApplied).toHaveBeenCalledWith(mockResponse.primarySuggestion);
    });

    it('should reset state after applying', async () => {
      mockSuggestPlayerMove.mockResolvedValue(mockResponse);

      const { result } = renderHook(() => usePlayerAISuggestion());

      // Get suggestion
      await act(async () => {
        await result.current[1].suggestMove('game-123', mockGameState);
      });

      // Apply
      act(() => {
        result.current[1].applySuggestion();
      });

      // Verify reset
      await waitFor(() => {
        const [state] = result.current;
        expect(state.suggestion).toBeNull();
      });
    });
  });

  describe('Ignore Suggestion', () => {
    const mockResponse: PlayerModeSuggestionResponse = {
      primarySuggestion: {
        action: 'Test action',
        rationale: 'Test',
        confidence: 0.8,
      },
      overallConfidence: 0.8,
      promptTokens: 50,
      completionTokens: 25,
      totalTokens: 75,
      processingTimeMs: 500,
      metadata: null,
    };

    it('should call onSuggestionIgnored callback', async () => {
      mockSuggestPlayerMove.mockResolvedValue(mockResponse);
      const onSuggestionIgnored = vi.fn();

      const { result } = renderHook(() => usePlayerAISuggestion({ onSuggestionIgnored }));

      // First get suggestion
      await act(async () => {
        await result.current[1].suggestMove('game-123', mockGameState);
      });

      // Then ignore it
      act(() => {
        result.current[1].ignoreSuggestion();
      });

      expect(onSuggestionIgnored).toHaveBeenCalled();
    });

    it('should reset state after ignoring', async () => {
      mockSuggestPlayerMove.mockResolvedValue(mockResponse);

      const { result } = renderHook(() => usePlayerAISuggestion());

      // Get suggestion
      await act(async () => {
        await result.current[1].suggestMove('game-123', mockGameState);
      });

      // Ignore
      act(() => {
        result.current[1].ignoreSuggestion();
      });

      // Verify reset
      await waitFor(() => {
        const [state] = result.current;
        expect(state.suggestion).toBeNull();
      });
    });
  });

  describe('Reset', () => {
    const mockResponse: PlayerModeSuggestionResponse = {
      primarySuggestion: {
        action: 'Test',
        rationale: 'Test',
        confidence: 0.8,
      },
      overallConfidence: 0.8,
      strategicContext: 'Context',
      promptTokens: 50,
      completionTokens: 25,
      totalTokens: 75,
      processingTimeMs: 1000,
      metadata: null,
    };

    it('should reset state to initial values', async () => {
      mockSuggestPlayerMove.mockResolvedValue(mockResponse);

      const { result } = renderHook(() => usePlayerAISuggestion());

      // Get suggestion first to have state
      await act(async () => {
        await result.current[1].suggestMove('game-123', mockGameState);
      });

      // Verify state has data
      expect(result.current[0].suggestion).not.toBeNull();
      expect(result.current[0].confidence).toBe(0.8);

      // Reset
      act(() => {
        result.current[1].reset();
      });

      // Verify reset
      await waitFor(() => {
        const [state] = result.current;
        expect(state.isLoading).toBe(false);
        expect(state.error).toBeNull();
        expect(state.suggestion).toBeNull();
        expect(state.alternatives).toEqual([]);
        expect(state.confidence).toBeNull();
        expect(state.strategicContext).toBeNull();
        expect(state.processingTimeMs).toBeNull();
      });
    });
  });

  describe('Edge Cases', () => {
    it('should handle response without alternatives', async () => {
      const responseWithoutAlternatives: PlayerModeSuggestionResponse = {
        primarySuggestion: {
          action: 'Move piece',
          rationale: 'Only option',
          confidence: 0.9,
        },
        overallConfidence: 0.9,
        promptTokens: 50,
        completionTokens: 25,
        totalTokens: 75,
        processingTimeMs: 500,
        metadata: null,
      };

      mockSuggestPlayerMove.mockResolvedValue(responseWithoutAlternatives);

      const { result } = renderHook(() => usePlayerAISuggestion());
      const [, controls] = result.current;

      await act(async () => {
        await controls.suggestMove('game-123', mockGameState);
      });

      const [state] = result.current;
      expect(state.alternatives).toEqual([]);
      expect(state.strategicContext).toBeNull();
    });

    it('should handle concurrent suggest requests', async () => {
      mockSuggestPlayerMove.mockImplementation(
        () =>
          new Promise(resolve =>
            setTimeout(
              () =>
                resolve({
                  primarySuggestion: {
                    action: 'Test',
                    rationale: 'Test',
                    confidence: 0.8,
                  },
                  overallConfidence: 0.8,
                  promptTokens: 10,
                  completionTokens: 10,
                  totalTokens: 20,
                  processingTimeMs: 100,
                  metadata: null,
                } as PlayerModeSuggestionResponse),
              100
            )
          )
      );

      const { result } = renderHook(() => usePlayerAISuggestion());
      const [, controls] = result.current;

      // Fire two requests in quick succession
      act(() => {
        void controls.suggestMove('game-1', mockGameState);
        void controls.suggestMove('game-2', mockGameState);
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isLoading).toBe(false);
      });

      // Should have been called twice
      expect(mockSuggestPlayerMove).toHaveBeenCalledTimes(2);
    });
  });
});
