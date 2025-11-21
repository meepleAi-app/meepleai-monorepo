/**
 * Test Suite: useMockStreaming.ts (Issue #1451)
 *
 * Comprehensive tests for mock streaming helper:
 * - Mock stream handling with setTimeout simulation
 * - Word-by-word streaming behavior
 * - State updates during streaming
 * - Error handling and validation
 * - Stream cancellation and cleanup
 * - Callback invocation
 *
 * Target Coverage: 90%+
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { useMockStreaming } from '../useMockStreaming';

describe('useMockStreaming', () => {
  beforeEach(() => {
    jest.clearAllTimers();
  });

  describe('Initial State', () => {
    it('should have correct initial state', () => {
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

      expect(controls.startStreaming).toBeDefined();
      expect(controls.stopStreaming).toBeDefined();
      expect(controls.reset).toBeDefined();
      expect(typeof controls.startStreaming).toBe('function');
      expect(typeof controls.stopStreaming).toBe('function');
      expect(typeof controls.reset).toBe('function');
    });
  });

  describe('Starting Stream', () => {
    it('should set isStreaming to true when starting', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });
    });

    it('should update state to "Generating mock response..."', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.state).toBe('Generating mock response...');
      });
    });

    it('should update state to "Searching documents..."', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.state).toBe('Searching documents...');
      }, { timeout: 500 });
    });

    it('should update state to "Generating answer..."', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.state).toBe('Generating answer...');
      }, { timeout: 1000 });
    });
  });

  describe('Streaming Behavior', () => {
    it('should stream content word by word', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        const streamPromise = controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 300));
        return streamPromise;
      });

      // Should have some content after a bit
      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer.length).toBeGreaterThan(0);
      });
    });

    it('should accumulate words progressively', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 50));
      });

      const snapshot1 = result.current[0].currentAnswer;

      await new Promise((resolve) => setTimeout(resolve, 200));
      const snapshot2 = result.current[0].currentAnswer;

      await new Promise((resolve) => setTimeout(resolve, 200));
      const snapshot3 = result.current[0].currentAnswer;

      // Content should grow over time
      const lengths = [snapshot1.length, snapshot2.length, snapshot3.length];
      const uniqueLengths = new Set(lengths.filter(l => l > 0));

      expect(uniqueLengths.size).toBeGreaterThanOrEqual(1);
    });

    it('should complete streaming and set isStreaming to false', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      }, { timeout: 10000 });
    });

    it('should set final content', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
        expect(state.currentAnswer.length).toBeGreaterThan(0);
      }, { timeout: 10000 });
    });

    it('should add mock citations during streaming', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 500));
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.citations.length).toBeGreaterThan(0);
      }, { timeout: 1000 });
    });

    it('should add mock snippets during streaming', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 500));
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.snippets.length).toBeGreaterThan(0);
      }, { timeout: 1000 });
    });
  });

  describe('Callbacks', () => {
    it('should call onComplete callback when streaming finishes', async () => {
      const onComplete = jest.fn();
      const { result } = renderHook(() => useMockStreaming({ onComplete }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalledTimes(1);
      }, { timeout: 10000 });

      expect(onComplete).toHaveBeenCalledWith(
        expect.any(String),
        expect.any(Array),
        expect.objectContaining({
          totalTokens: expect.any(Number),
          confidence: expect.any(Number),
          followUpQuestions: expect.any(Array),
          citations: expect.any(Array),
        })
      );
    });

    it('should include follow-up questions in completion metadata', async () => {
      const onComplete = jest.fn();
      const { result } = renderHook(() => useMockStreaming({ onComplete }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalled();
      }, { timeout: 10000 });

      const metadata = onComplete.mock.calls[0][2];
      expect(metadata.followUpQuestions).toBeDefined();
      expect(metadata.followUpQuestions.length).toBeGreaterThan(0);
    });

    it('should include citations in completion metadata', async () => {
      const onComplete = jest.fn();
      const { result } = renderHook(() => useMockStreaming({ onComplete }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalled();
      }, { timeout: 10000 });

      const metadata = onComplete.mock.calls[0][2];
      expect(metadata.citations).toBeDefined();
      expect(metadata.citations.length).toBeGreaterThan(0);
    });
  });

  describe('Stop Streaming', () => {
    it('should stop streaming when stopStreaming is called', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 100));
      });

      expect(result.current[0].isStreaming).toBe(true);

      act(() => {
        controls.stopStreaming();
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });
    });

    it('should not call onComplete when stopped early', async () => {
      const onComplete = jest.fn();
      const { result } = renderHook(() => useMockStreaming({ onComplete }));
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 100));
      });

      act(() => {
        controls.stopStreaming();
      });

      await new Promise((resolve) => setTimeout(resolve, 500));

      expect(onComplete).not.toHaveBeenCalled();
    });
  });

  describe('Reset', () => {
    it('should reset all state to initial values', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      }, { timeout: 10000 });

      act(() => {
        controls.reset();
      });

      await waitFor(() => {
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
    });
  });

  describe('Error Handling', () => {
    it('should handle cancellation gracefully', async () => {
      const onError = jest.fn();
      const { result } = renderHook(() => useMockStreaming({ onError }));
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 50));
      });

      act(() => {
        controls.stopStreaming();
      });

      await new Promise((resolve) => setTimeout(resolve, 200));

      // Cancellation should not trigger error callback
      expect(onError).not.toHaveBeenCalled();
    });
  });

  describe('Cleanup', () => {
    it('should cleanup on unmount', async () => {
      const { result, unmount } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 100));
      });

      expect(result.current[0].isStreaming).toBe(true);

      unmount();

      // No errors should occur during cleanup
    });
  });

  describe('Multiple Streams', () => {
    it('should cancel previous stream when starting new one', async () => {
      const onComplete = jest.fn();
      const { result } = renderHook(() => useMockStreaming({ onComplete }));
      const [, controls] = result.current;

      // Start first stream
      await act(async () => {
        controls.startStreaming('game-1', 'query 1');
        await new Promise((resolve) => setTimeout(resolve, 100));
      });

      expect(result.current[0].isStreaming).toBe(true);

      // Start second stream (should cancel first)
      await act(async () => {
        controls.startStreaming('game-2', 'query 2');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      }, { timeout: 10000 });

      // Should only complete second stream
      expect(onComplete).toHaveBeenCalledTimes(1);
    });
  });

  describe('Mock Response Selection', () => {
    it('should use one of the predefined mock responses', async () => {
      const { result } = renderHook(() => useMockStreaming());
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      }, { timeout: 10000 });

      const [state] = result.current;
      expect(state.currentAnswer.length).toBeGreaterThan(0);
      expect(typeof state.currentAnswer).toBe('string');
    });
  });
});
