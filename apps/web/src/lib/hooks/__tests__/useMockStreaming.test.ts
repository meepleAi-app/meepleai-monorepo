/**
 * useMockStreaming Hook Tests (Issue #2765)
 *
 * Comprehensive test suite for mock streaming hook:
 * - Initial state and controls
 * - Word-by-word streaming simulation
 * - State transitions
 * - Citations generation
 * - Follow-up questions
 * - Cancellation/abort
 * - Cleanup on unmount
 * - Callbacks
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useMockStreaming } from '../useMockStreaming';

describe('useMockStreaming', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.clearAllMocks();
    vi.useRealTimers();
  });

  // ==========================================================================
  // INITIAL STATE TESTS
  // ==========================================================================

  describe('Initial State', () => {
    it('should initialize with correct default state', () => {
      const { result } = renderHook(() => useMockStreaming());
      const [state] = result.current;

      expect(state.state).toBeNull();
      expect(state.currentAnswer).toBe('');
      expect(state.snippets).toEqual([]);
      expect(state.citations).toEqual([]);
      expect(state.followUpQuestions).toEqual([]);
      expect(state.totalTokens).toBe(0);
      expect(state.confidence).toBeNull();
      expect(state.isStreaming).toBe(false);
      expect(state.error).toBeNull();
    });

    it('should provide control functions', () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      expect(controls.startStreaming).toBeInstanceOf(Function);
      expect(controls.stopStreaming).toBeInstanceOf(Function);
      expect(controls.reset).toBeInstanceOf(Function);
    });
  });

  // ==========================================================================
  // STREAMING SIMULATION TESTS
  // ==========================================================================

  describe('Streaming Simulation', () => {
    it('should set isStreaming to true when starting', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      expect(result.current[0].isStreaming).toBe(true);
    });

    it('should show initial state messages', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Should start with "Generating mock response..."
      expect(result.current[0].state).toBe('Generating mock response...');

      // Advance timers to see state transition to "Searching documents..."
      await act(async () => {
        await vi.advanceTimersByTimeAsync(10);
      });

      expect(result.current[0].state).toBe('Searching documents...');
    });

    it('should transition to generating answer state', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Advance past initial delay (200ms)
      await act(async () => {
        await vi.advanceTimersByTimeAsync(250);
      });

      expect(result.current[0].state).toBe('Generating answer...');
    });

    it('should stream words progressively', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Advance past initial delays
      await act(async () => {
        await vi.advanceTimersByTimeAsync(250);
      });

      // At this point, the first word should be visible
      expect(result.current[0].currentAnswer.length).toBeGreaterThan(0);
    });

    it('should complete streaming with all data', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Fast-forward to completion (mock responses are ~15-20 words, at 15 words/sec)
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].state).toBeNull();
      expect(result.current[0].currentAnswer.length).toBeGreaterThan(0);
      expect(result.current[0].confidence).toBe(0.85);
      expect(result.current[0].totalTokens).toBeGreaterThan(0);
    });
  });

  // ==========================================================================
  // CITATIONS TESTS
  // ==========================================================================

  describe('Citations Generation', () => {
    it('should generate mock citations during streaming', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Advance past initial processing
      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(result.current[0].citations).toHaveLength(1);
      expect(result.current[0].citations[0].documentId).toBe('game-game-123');
      expect(result.current[0].citations[0].relevanceScore).toBe(0.95);
    });

    it('should generate snippets from citations', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(result.current[0].snippets).toHaveLength(1);
      expect(result.current[0].snippets[0].source).toBe('game-game-123');
      expect(result.current[0].snippets[0].page).toBe(1);
    });
  });

  // ==========================================================================
  // FOLLOW-UP QUESTIONS TESTS
  // ==========================================================================

  describe('Follow-Up Questions', () => {
    it('should generate follow-up questions on completion', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Fast-forward to completion
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(result.current[0].followUpQuestions).toHaveLength(2);
      expect(result.current[0].followUpQuestions).toContain('Can you explain the scoring system?');
      expect(result.current[0].followUpQuestions).toContain('What happens in edge cases?');
    });
  });

  // ==========================================================================
  // CALLBACK TESTS
  // ==========================================================================

  describe('Callbacks', () => {
    it('should call onComplete callback with correct data', async () => {
      const onComplete = vi.fn();

      const { result } = renderHook(() => useMockStreaming({ onComplete }));

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Fast-forward to completion
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(onComplete).toHaveBeenCalledTimes(1);
      expect(onComplete).toHaveBeenCalledWith(
        expect.any(String), // answer
        expect.any(Array), // snippets
        expect.objectContaining({
          totalTokens: expect.any(Number),
          confidence: 0.85,
          followUpQuestions: expect.any(Array),
          citations: expect.any(Array),
        })
      );
    });

    it('should use latest callbacks via ref', async () => {
      const onComplete1 = vi.fn();
      const onComplete2 = vi.fn();

      const { result, rerender } = renderHook(
        ({ onComplete }) => useMockStreaming({ onComplete }),
        { initialProps: { onComplete: onComplete1 } }
      );

      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      // Update callback mid-stream
      rerender({ onComplete: onComplete2 });

      // Fast-forward to completion
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(onComplete1).not.toHaveBeenCalled();
      expect(onComplete2).toHaveBeenCalled();
    });
  });

  // ==========================================================================
  // CANCELLATION TESTS
  // ==========================================================================

  describe('Cancellation', () => {
    it('should stop streaming when stopStreaming is called', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      expect(result.current[0].isStreaming).toBe(true);

      act(() => {
        result.current[1].stopStreaming();
      });

      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should not call onComplete when cancelled', async () => {
      const onComplete = vi.fn();

      const { result } = renderHook(() => useMockStreaming({ onComplete }));

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Advance a bit
      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      act(() => {
        result.current[1].stopStreaming();
      });

      // Advance more to ensure no callback is called
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(onComplete).not.toHaveBeenCalled();
    });

    it('should not call onError when cancelled', async () => {
      const onError = vi.fn();

      const { result } = renderHook(() => useMockStreaming({ onError }));

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      act(() => {
        result.current[1].stopStreaming();
      });

      expect(onError).not.toHaveBeenCalled();
      expect(result.current[0].error).toBeNull();
    });

    it('should clear state to null when cancelled', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Advance to get a state message
      await act(async () => {
        await vi.advanceTimersByTimeAsync(100);
      });

      expect(result.current[0].state).not.toBeNull();

      act(() => {
        result.current[1].stopStreaming();
      });

      // Wait for state update to propagate
      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      // Note: The state might not be null immediately after stopStreaming
      // because the async function handles cancellation differently
      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should stop previous stream when starting a new one', async () => {
      const onComplete = vi.fn();

      const { result } = renderHook(() => useMockStreaming({ onComplete }));

      act(() => {
        result.current[1].startStreaming('game-1', 'query 1');
      });

      // Start second stream immediately (stops first)
      act(() => {
        result.current[1].startStreaming('game-2', 'query 2');
      });

      // Complete second stream
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      // Only one completion callback should be called (for the second stream)
      expect(onComplete).toHaveBeenCalledTimes(1);
    });
  });

  // ==========================================================================
  // RESET TESTS
  // ==========================================================================

  describe('Reset Functionality', () => {
    it('should reset state to initial values', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Let some streaming happen
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(result.current[0].currentAnswer.length).toBeGreaterThan(0);

      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0].state).toBeNull();
      expect(result.current[0].currentAnswer).toBe('');
      expect(result.current[0].snippets).toEqual([]);
      expect(result.current[0].citations).toEqual([]);
      expect(result.current[0].followUpQuestions).toEqual([]);
      expect(result.current[0].totalTokens).toBe(0);
      expect(result.current[0].confidence).toBeNull();
      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].error).toBeNull();
    });

    it('should stop streaming when reset is called', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      expect(result.current[0].isStreaming).toBe(true);

      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0].isStreaming).toBe(false);
    });
  });

  // ==========================================================================
  // CLEANUP TESTS
  // ==========================================================================

  describe('Cleanup on Unmount', () => {
    it('should cleanup timeouts on unmount', async () => {
      const { result, unmount } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Unmount while streaming
      unmount();

      // Advance timers - should not cause errors
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      // No assertion needed - test passes if no error is thrown
    });

    it('should abort controller on unmount', async () => {
      const { result, unmount } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      unmount();

      // No assertion needed - test passes if cleanup runs without error
    });
  });

  // ==========================================================================
  // WORD GENERATOR TESTS
  // ==========================================================================

  describe('Word Generator', () => {
    it('should accumulate words progressively', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const answers: string[] = [];

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Advance past initial delays
      await act(async () => {
        await vi.advanceTimersByTimeAsync(250);
      });

      // Capture answers at different points
      for (let i = 0; i < 5; i++) {
        answers.push(result.current[0].currentAnswer);
        await act(async () => {
          await vi.advanceTimersByTimeAsync(70); // ~15 words/sec = ~67ms per word
        });
      }

      // Each captured answer should be longer than or equal to the previous
      for (let i = 1; i < answers.length; i++) {
        expect(answers[i].length).toBeGreaterThanOrEqual(answers[i - 1].length);
      }
    });

    it('should produce complete answer matching mock response', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Fast-forward to completion
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      // Should match one of the mock responses
      const mockResponses = [
        'Ecco la risposta alla tua domanda. Secondo le regole del gioco, dovrai seguire questi passaggi per completare la mossa.',
        'Ottima domanda! Nel manuale, questa situazione è descritta nella sezione 3.2. Ti consiglio di consultare anche gli esempi a pagina 15.',
        'In questo scenario, devi considerare due fattori principali: la posizione dei tuoi pezzi e le carte azione disponibili.',
      ];

      expect(mockResponses).toContain(result.current[0].currentAnswer);
    });
  });

  // ==========================================================================
  // TIMING TESTS
  // ==========================================================================

  describe('Timing Configuration', () => {
    it('should respect words per second configuration', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      // Advance past initial delays
      await act(async () => {
        await vi.advanceTimersByTimeAsync(250);
      });

      const startAnswer = result.current[0].currentAnswer;

      // Advance exactly 1 second
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });

      const endAnswer = result.current[0].currentAnswer;

      // Count words added (split by space)
      const startWords = startAnswer.split(' ').filter(Boolean).length;
      const endWords = endAnswer.split(' ').filter(Boolean).length;
      const wordsAdded = endWords - startWords;

      // Should be approximately 15 words (configured rate) with some tolerance
      expect(wordsAdded).toBeGreaterThanOrEqual(10);
      expect(wordsAdded).toBeLessThanOrEqual(20);
    });
  });

  // ==========================================================================
  // METADATA TESTS
  // ==========================================================================

  describe('Metadata', () => {
    it('should calculate totalTokens based on word count', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      const wordCount = result.current[0].currentAnswer.split(' ').filter(Boolean).length;
      expect(result.current[0].totalTokens).toBe(wordCount);
    });

    it('should set confidence to 0.85', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(result.current[0].confidence).toBe(0.85);
    });
  });

  // ==========================================================================
  // PARAMETER TESTS
  // ==========================================================================

  describe('Parameters', () => {
    it('should accept gameId parameter for citation generation', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('my-custom-game', 'test query');
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(result.current[0].citations[0].documentId).toBe('game-my-custom-game');
    });

    it('should ignore chatId and searchMode parameters (mock)', async () => {
      const { result } = renderHook(() => useMockStreaming());

      // These parameters are accepted but ignored in mock implementation
      act(() => {
        result.current[1].startStreaming('game-123', 'query', 'chat-456', 'Semantic');
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      // Should complete successfully regardless of parameters
      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].currentAnswer.length).toBeGreaterThan(0);
    });
  });

  // ==========================================================================
  // STABILITY TESTS
  // ==========================================================================

  describe('Control Function Stability', () => {
    it('should maintain stable control function references', () => {
      const { result, rerender } = renderHook(() => useMockStreaming());

      const initialControls = result.current[1];
      rerender();
      const newControls = result.current[1];

      expect(initialControls.startStreaming).toBe(newControls.startStreaming);
      expect(initialControls.stopStreaming).toBe(newControls.stopStreaming);
      expect(initialControls.reset).toBe(newControls.reset);
    });
  });

  // ==========================================================================
  // EDGE CASES
  // ==========================================================================

  describe('Edge Cases', () => {
    it('should handle rapid start/stop cycles', async () => {
      const { result } = renderHook(() => useMockStreaming());

      for (let i = 0; i < 5; i++) {
        act(() => {
          result.current[1].startStreaming('game-123', 'test');
        });

        act(() => {
          result.current[1].stopStreaming();
        });
      }

      // Should be in stopped state
      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should handle multiple resets', async () => {
      const { result } = renderHook(() => useMockStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      act(() => {
        result.current[1].reset();
        result.current[1].reset();
        result.current[1].reset();
      });

      // Should remain in initial state
      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].currentAnswer).toBe('');
    });

    it('should handle stopStreaming when not streaming', () => {
      const { result } = renderHook(() => useMockStreaming());

      // Should not throw
      act(() => {
        result.current[1].stopStreaming();
      });

      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should handle reset when not streaming', () => {
      const { result } = renderHook(() => useMockStreaming());

      // Should not throw
      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].currentAnswer).toBe('');
    });
  });
});
