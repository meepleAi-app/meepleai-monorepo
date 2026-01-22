/**
 * useStatefulStreaming Hook Tests (Issue #2765)
 *
 * Tests for state machine-based streaming with pause/resume capability.
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import {
  useStatefulStreaming,
  type StreamFunction,
  type StreamHandlers,
  type StatefulStreamingOptions,
} from '../useStatefulStreaming';

// ============================================================================
// Test Setup
// ============================================================================

describe('useStatefulStreaming', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  /**
   * Creates a mock stream function that can be controlled in tests
   */
  function createMockStreamFn() {
    let handlers: StreamHandlers | null = null;
    const cleanup = vi.fn();

    const streamFn: StreamFunction = (h) => {
      handlers = h;
      return cleanup;
    };

    return {
      streamFn,
      cleanup,
      sendData: (data: string) => handlers?.onData(data),
      complete: (metadata?: Record<string, unknown>) => handlers?.onComplete(metadata),
      error: (err: string) => handlers?.onError(err),
    };
  }

  // ==========================================================================
  // Initial State Tests
  // ==========================================================================

  describe('Initial State', () => {
    it('should return initial state', () => {
      const { result } = renderHook(() => useStatefulStreaming());

      expect(result.current[0]).toMatchObject({
        currentState: 'idle',
        previousState: null,
        content: '',
        buffer: '',
        error: null,
        isActive: false,
        canPause: false,
        canResume: false,
      });
    });

    it('should return controls object', () => {
      const { result } = renderHook(() => useStatefulStreaming());

      const [, controls] = result.current;
      expect(controls).toHaveProperty('start');
      expect(controls).toHaveProperty('pause');
      expect(controls).toHaveProperty('resume');
      expect(controls).toHaveProperty('stop');
      expect(controls).toHaveProperty('reset');
      expect(controls).toHaveProperty('receiveData');
      expect(controls).toHaveProperty('complete');
      expect(controls).toHaveProperty('triggerError');
    });

    it('should accept initial metadata', () => {
      const { result } = renderHook(() =>
        useStatefulStreaming({ initialMetadata: { game: 'chess', version: 1 } })
      );

      expect(result.current[0].metadata).toEqual({ game: 'chess', version: 1 });
    });
  });

  // ==========================================================================
  // State Transition Tests
  // ==========================================================================

  describe('State Transitions', () => {
    it('should transition from idle to preparing to streaming on start', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      expect(result.current[0].currentState).toBe('streaming');
      expect(result.current[0].previousState).toBe('preparing');
      expect(result.current[0].isActive).toBe(true);
    });

    it('should transition from streaming to paused on pause', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      act(() => {
        result.current[1].pause();
      });

      expect(result.current[0].currentState).toBe('paused');
      expect(result.current[0].canResume).toBe(true);
      expect(result.current[0].canPause).toBe(false);
    });

    it('should transition from paused to streaming on resume', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        result.current[1].pause();
      });

      act(() => {
        result.current[1].resume();
      });

      expect(result.current[0].currentState).toBe('streaming');
      expect(result.current[0].isActive).toBe(true);
    });

    it('should transition to complete state on completion', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Hello');
        mock.complete({ status: 'success' });
      });

      expect(result.current[0].currentState).toBe('complete');
      expect(result.current[0].isActive).toBe(false);
    });

    it('should transition to error state on error', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.error('Connection failed');
      });

      expect(result.current[0].currentState).toBe('error');
      expect(result.current[0].error).toBe('Connection failed');
    });

    it('should transition from error to idle on reset', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.error('Error occurred');
      });

      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0].currentState).toBe('idle');
      expect(result.current[0].error).toBe(null);
    });

    it('should transition from complete to idle on reset', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.complete();
      });

      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0].currentState).toBe('idle');
    });

    it('should call onStateChange callback on transitions', () => {
      const onStateChange = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onStateChange }));

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      expect(onStateChange).toHaveBeenCalledWith('preparing', 'idle');
      expect(onStateChange).toHaveBeenCalledWith('streaming', 'preparing');
    });

    it('should ignore invalid state transitions', () => {
      const { result } = renderHook(() => useStatefulStreaming());

      // Try to pause while idle (invalid)
      act(() => {
        result.current[1].pause();
      });

      expect(result.current[0].currentState).toBe('idle');

      // Try to resume while idle (invalid)
      act(() => {
        result.current[1].resume();
      });

      expect(result.current[0].currentState).toBe('idle');
    });
  });

  // ==========================================================================
  // Data Handling Tests
  // ==========================================================================

  describe('Data Handling', () => {
    it('should accumulate content from data events', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Hello ');
        mock.sendData('World');
      });

      expect(result.current[0].content).toBe('Hello World');
    });

    it('should call onData callback with data and accumulated content', () => {
      const onData = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onData }));

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Hello');
      });

      expect(onData).toHaveBeenCalledWith('Hello', 'Hello');

      act(() => {
        mock.sendData(' World');
      });

      expect(onData).toHaveBeenCalledWith(' World', 'Hello World');
    });

    it('should buffer data when paused', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Before pause');
      });

      expect(result.current[0].content).toBe('Before pause');

      act(() => {
        result.current[1].pause();
        mock.sendData(' During pause');
      });

      expect(result.current[0].content).toBe('Before pause');
      expect(result.current[0].buffer).toBe(' During pause');
    });

    it('should flush buffer on resume', () => {
      const onResume = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onResume }));

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Start');
        result.current[1].pause();
        mock.sendData(' Buffered');
      });

      act(() => {
        result.current[1].resume();
      });

      expect(result.current[0].content).toBe('Start Buffered');
      expect(result.current[0].buffer).toBe('');
      expect(onResume).toHaveBeenCalledWith(' Buffered');
    });

    it('should include buffer in final content on completion', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Content');
        result.current[1].pause();
        mock.sendData(' More');
        mock.complete();
      });

      expect(result.current[0].content).toBe('Content More');
    });
  });

  // ==========================================================================
  // Progress Tracking Tests
  // ==========================================================================

  describe('Progress Tracking', () => {
    it('should track bytes received', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('12345'); // 5 bytes
        mock.sendData('67890'); // 5 bytes
      });

      expect(result.current[0].progress.bytesReceived).toBe(10);
    });

    it('should track packets received', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('packet1');
        mock.sendData('packet2');
        mock.sendData('packet3');
      });

      expect(result.current[0].progress.packetsReceived).toBe(3);
    });

    it('should track elapsed time', async () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
        mock.sendData('data');
      });

      expect(result.current[0].progress.elapsedMs).toBeGreaterThanOrEqual(1000);
    });

    it('should calculate bytes per second', async () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
        mock.sendData('1234567890'); // 10 bytes after 1 second
      });

      // Should be approximately 10 bytes/second
      expect(result.current[0].progress.bytesPerSecond).toBeGreaterThan(0);
    });

    it('should reset progress on start', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Some data');
        mock.complete();
      });

      const mock2 = createMockStreamFn();

      act(() => {
        result.current[1].reset();
        result.current[1].start(mock2.streamFn);
      });

      expect(result.current[0].progress.bytesReceived).toBe(0);
      expect(result.current[0].progress.packetsReceived).toBe(0);
    });
  });

  // ==========================================================================
  // Metadata Tests
  // ==========================================================================

  describe('Metadata', () => {
    it('should preserve initial metadata through streaming', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() =>
        useStatefulStreaming({ initialMetadata: { source: 'api' } })
      );

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('data');
        mock.complete();
      });

      expect(result.current[0].metadata).toEqual({ source: 'api' });
    });

    it('should merge completion metadata with existing', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() =>
        useStatefulStreaming({ initialMetadata: { source: 'api' } })
      );

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.complete({ tokens: 100, model: 'gpt-4' });
      });

      expect(result.current[0].metadata).toEqual({
        source: 'api',
        tokens: 100,
        model: 'gpt-4',
      });
    });

    it('should call onComplete with final metadata', () => {
      const onComplete = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() =>
        useStatefulStreaming({
          onComplete,
          initialMetadata: { game: 'chess' },
        })
      );

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Answer');
        mock.complete({ confidence: 0.95 });
      });

      expect(onComplete).toHaveBeenCalledWith('Answer', {
        game: 'chess',
        confidence: 0.95,
      });
    });
  });

  // ==========================================================================
  // Callback Tests
  // ==========================================================================

  describe('Callbacks', () => {
    it('should call onPause when paused', () => {
      const onPause = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onPause }));

      act(() => {
        result.current[1].start(mock.streamFn);
        result.current[1].pause();
      });

      expect(onPause).toHaveBeenCalledTimes(1);
    });

    it('should call onError when error occurs', () => {
      const onError = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onError }));

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.error('Network error');
      });

      expect(onError).toHaveBeenCalledWith('Network error');
    });
  });

  // ==========================================================================
  // Manual Control Tests
  // ==========================================================================

  describe('Manual Controls', () => {
    it('should allow manual receiveData', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      act(() => {
        result.current[1].receiveData('Manual data');
      });

      expect(result.current[0].content).toBe('Manual data');
    });

    it('should allow manual complete', () => {
      const onComplete = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onComplete }));

      act(() => {
        result.current[1].start(mock.streamFn);
        result.current[1].receiveData('Content');
      });

      act(() => {
        result.current[1].complete({ manual: true });
      });

      expect(result.current[0].currentState).toBe('complete');
      expect(onComplete).toHaveBeenCalled();
    });

    it('should allow manual triggerError', () => {
      const onError = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onError }));

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      act(() => {
        result.current[1].triggerError('Manual error');
      });

      expect(result.current[0].currentState).toBe('error');
      expect(result.current[0].error).toBe('Manual error');
      expect(onError).toHaveBeenCalledWith('Manual error');
    });
  });

  // ==========================================================================
  // Cleanup Tests
  // ==========================================================================

  describe('Cleanup', () => {
    it('should call cleanup function on stop', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      act(() => {
        result.current[1].stop();
      });

      expect(mock.cleanup).toHaveBeenCalledTimes(1);
    });

    it('should call cleanup function on reset', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      act(() => {
        result.current[1].reset();
      });

      expect(mock.cleanup).toHaveBeenCalledTimes(1);
    });

    it('should call cleanup function on unmount', () => {
      const mock = createMockStreamFn();
      const { result, unmount } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
      });

      unmount();

      expect(mock.cleanup).toHaveBeenCalledTimes(1);
    });
  });

  // ==========================================================================
  // Error Handling Tests
  // ==========================================================================

  describe('Error Handling', () => {
    it('should handle stream function throwing error', () => {
      const onError = vi.fn();
      const { result } = renderHook(() => useStatefulStreaming({ onError }));

      const badStreamFn: StreamFunction = () => {
        throw new Error('Initialization failed');
      };

      act(() => {
        result.current[1].start(badStreamFn);
      });

      expect(result.current[0].currentState).toBe('error');
      expect(result.current[0].error).toBe('Initialization failed');
      expect(onError).toHaveBeenCalled();
    });

    it('should handle non-Error throws', () => {
      const { result } = renderHook(() => useStatefulStreaming());

      const badStreamFn: StreamFunction = () => {
        throw 'String error';
      };

      act(() => {
        result.current[1].start(badStreamFn);
      });

      expect(result.current[0].currentState).toBe('error');
      expect(result.current[0].error).toBe('Stream initialization failed');
    });

    it('should allow recovery from error state', () => {
      const mock1 = createMockStreamFn();
      const mock2 = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock1.streamFn);
        mock1.error('First error');
      });

      expect(result.current[0].currentState).toBe('error');

      act(() => {
        result.current[1].reset();
        result.current[1].start(mock2.streamFn);
      });

      expect(result.current[0].currentState).toBe('streaming');
      expect(result.current[0].error).toBe(null);
    });
  });

  // ==========================================================================
  // Integration Tests
  // ==========================================================================

  describe('Integration Scenarios', () => {
    it('should handle complete streaming workflow', () => {
      const onStateChange = vi.fn();
      const onData = vi.fn();
      const onComplete = vi.fn();

      const mock = createMockStreamFn();
      const { result } = renderHook(() =>
        useStatefulStreaming({ onStateChange, onData, onComplete })
      );

      // Start streaming
      act(() => {
        result.current[1].start(mock.streamFn);
      });

      expect(result.current[0].currentState).toBe('streaming');

      // Receive data
      act(() => {
        mock.sendData('Hello ');
        mock.sendData('World!');
      });

      expect(result.current[0].content).toBe('Hello World!');
      expect(onData).toHaveBeenCalledTimes(2);

      // Complete
      act(() => {
        mock.complete({ status: 'success' });
      });

      expect(result.current[0].currentState).toBe('complete');
      expect(onComplete).toHaveBeenCalledWith('Hello World!', { status: 'success' });
    });

    it('should handle pause/resume/continue workflow', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Start ');
      });

      act(() => {
        result.current[1].pause();
        mock.sendData('Buffered ');
      });

      expect(result.current[0].content).toBe('Start ');
      expect(result.current[0].buffer).toBe('Buffered ');

      act(() => {
        result.current[1].resume();
        mock.sendData('End');
      });

      expect(result.current[0].content).toBe('Start Buffered End');
      expect(result.current[0].buffer).toBe('');

      act(() => {
        mock.complete();
      });

      expect(result.current[0].content).toBe('Start Buffered End');
    });

    it('should handle multiple pause/resume cycles', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('A');
      });

      // First pause/resume
      act(() => {
        result.current[1].pause();
        mock.sendData('B');
        result.current[1].resume();
      });

      expect(result.current[0].content).toBe('AB');

      // Second pause/resume
      act(() => {
        result.current[1].pause();
        mock.sendData('C');
        result.current[1].resume();
      });

      expect(result.current[0].content).toBe('ABC');
    });

    it('should handle error during streaming', () => {
      const onError = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onError }));

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('Partial ');
      });

      act(() => {
        mock.error('Connection lost');
      });

      expect(result.current[0].currentState).toBe('error');
      expect(result.current[0].content).toBe('Partial ');
      expect(onError).toHaveBeenCalledWith('Connection lost');
    });
  });

  // ==========================================================================
  // Options Stability Tests
  // ==========================================================================

  describe('Options Stability', () => {
    it('should use updated callbacks without restarting', () => {
      const onData1 = vi.fn();
      const onData2 = vi.fn();

      const mock = createMockStreamFn();
      const { result, rerender } = renderHook(
        ({ onData }: StatefulStreamingOptions) => useStatefulStreaming({ onData }),
        { initialProps: { onData: onData1 } }
      );

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('First');
      });

      expect(onData1).toHaveBeenCalledTimes(1);

      // Update callback
      rerender({ onData: onData2 });

      act(() => {
        mock.sendData('Second');
      });

      expect(onData1).toHaveBeenCalledTimes(1);
      expect(onData2).toHaveBeenCalledTimes(1);
    });

    it('should maintain stable controls reference', () => {
      const { result, rerender } = renderHook(() => useStatefulStreaming());

      const controls1 = result.current[1];
      rerender();
      const controls2 = result.current[1];

      expect(controls1).toBe(controls2);
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('should handle empty data events', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.sendData('');
        mock.sendData('Real data');
      });

      expect(result.current[0].content).toBe('Real data');
      expect(result.current[0].progress.packetsReceived).toBe(2);
    });

    it('should handle rapid data events', () => {
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock.streamFn);
        for (let i = 0; i < 100; i++) {
          mock.sendData(`${i} `);
        }
      });

      expect(result.current[0].progress.packetsReceived).toBe(100);
      expect(result.current[0].content).toContain('99');
    });

    it('should handle start called while already streaming', () => {
      const mock1 = createMockStreamFn();
      const mock2 = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming());

      act(() => {
        result.current[1].start(mock1.streamFn);
        mock1.sendData('First stream');
      });

      // Start new stream without stopping - content is reset
      act(() => {
        result.current[1].start(mock2.streamFn);
      });

      expect(result.current[0].content).toBe('');
      expect(result.current[0].currentState).toBe('streaming');
    });

    it('should handle completion with empty content', () => {
      const onComplete = vi.fn();
      const mock = createMockStreamFn();
      const { result } = renderHook(() => useStatefulStreaming({ onComplete }));

      act(() => {
        result.current[1].start(mock.streamFn);
        mock.complete();
      });

      expect(result.current[0].currentState).toBe('complete');
      expect(result.current[0].content).toBe('');
      expect(onComplete).toHaveBeenCalledWith('', {});
    });
  });
});
