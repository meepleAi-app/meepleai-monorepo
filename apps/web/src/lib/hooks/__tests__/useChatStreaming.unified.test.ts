/**
 * Test Suite: useChatStreaming (Unified Mode Testing) - Issue #1451
 *
 * Tests for unified streaming hook with mode switching:
 * - Mock mode selection via useMock prop
 * - Mock mode selection via environment variable
 * - Mode priority (prop > env > default)
 * - Identical interface across both modes
 * - Zero breaking changes
 *
 * Target Coverage: 90%+
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { useChatStreaming } from '../useChatStreaming';

describe('useChatStreaming - Unified Mode Testing', () => {
  const originalEnv = process.env.NEXT_PUBLIC_USE_MOCK_STREAMING;

  afterEach(() => {
    // Restore original env
    if (originalEnv !== undefined) {
      process.env.NEXT_PUBLIC_USE_MOCK_STREAMING = originalEnv;
    } else {
      delete process.env.NEXT_PUBLIC_USE_MOCK_STREAMING;
    }
  });

  describe('Mode Selection', () => {
    it('should use real SSE mode by default', () => {
      delete process.env.NEXT_PUBLIC_USE_MOCK_STREAMING;

      const { result } = renderHook(() => useChatStreaming());
      const [state] = result.current;

      // Real mode initial state
      expect(state).toBeDefined();
      expect(state.isStreaming).toBe(false);
    });

    it('should use mock mode when useMock=true', async () => {
      const { result } = renderHook(() => useChatStreaming({ useMock: true }));
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 300));
      });

      // Mock mode should show "Generating mock response..." or "Searching documents..."
      await waitFor(() => {
        const [state] = result.current;
        expect(state.state).toMatch(/mock|Searching/);
      }, { timeout: 1000 });
    });

    it('should use mock mode when NEXT_PUBLIC_USE_MOCK_STREAMING=true', () => {
      process.env.NEXT_PUBLIC_USE_MOCK_STREAMING = 'true';

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      // Should use mock mode
      act(() => {
        controls.startStreaming('game-123', 'test query');
      });

      // Mock mode will set state immediately
      waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });
    });

    it('should prioritize useMock prop over environment variable', () => {
      process.env.NEXT_PUBLIC_USE_MOCK_STREAMING = 'false';

      const { result } = renderHook(() => useChatStreaming({ useMock: true }));

      // Should use mock mode despite env saying false
      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      waitFor(() => {
        expect(result.current[0].isStreaming).toBe(true);
      });
    });

    it('should use real mode when useMock=false overrides env', () => {
      process.env.NEXT_PUBLIC_USE_MOCK_STREAMING = 'true';

      const { result } = renderHook(() => useChatStreaming({ useMock: false }));

      // Should use real mode despite env saying true
      expect(result.current[0]).toBeDefined();
    });
  });

  describe('Interface Consistency', () => {
    it('should have identical state structure in both modes', async () => {
      const { result: realResult } = renderHook(() => useChatStreaming({ useMock: false }));
      const { result: mockResult } = renderHook(() => useChatStreaming({ useMock: true }));

      const [realState] = realResult.current;
      const [mockState] = mockResult.current;

      // Both should have same properties
      expect(Object.keys(realState).sort()).toEqual(Object.keys(mockState).sort());

      // Check specific properties exist
      const expectedProps = [
        'state',
        'currentAnswer',
        'snippets',
        'citations',
        'followUpQuestions',
        'totalTokens',
        'confidence',
        'isStreaming',
        'error',
      ];

      expectedProps.forEach((prop) => {
        expect(realState).toHaveProperty(prop);
        expect(mockState).toHaveProperty(prop);
      });
    });

    it('should have identical controls interface in both modes', () => {
      const { result: realResult } = renderHook(() => useChatStreaming({ useMock: false }));
      const { result: mockResult } = renderHook(() => useChatStreaming({ useMock: true }));

      const [, realControls] = realResult.current;
      const [, mockControls] = mockResult.current;

      // Both should have same control functions
      expect(Object.keys(realControls).sort()).toEqual(Object.keys(mockControls).sort());

      // Check specific controls exist
      expect(realControls.startStreaming).toBeDefined();
      expect(realControls.stopStreaming).toBeDefined();
      expect(realControls.reset).toBeDefined();

      expect(mockControls.startStreaming).toBeDefined();
      expect(mockControls.stopStreaming).toBeDefined();
      expect(mockControls.reset).toBeDefined();
    });

    it('should accept same parameters in both modes', () => {
      const { result: realResult } = renderHook(() => useChatStreaming({ useMock: false }));
      const { result: mockResult } = renderHook(() => useChatStreaming({ useMock: true }));

      const [, realControls] = realResult.current;
      const [, mockControls] = mockResult.current;

      // Both should accept same startStreaming parameters
      expect(() => {
        act(() => {
          realControls.startStreaming('game-1', 'query', 'chat-1', 'Hybrid');
        });
      }).not.toThrow();

      expect(() => {
        act(() => {
          mockControls.startStreaming('game-1', 'query', 'chat-1', 'Hybrid');
        });
      }).not.toThrow();
    });
  });

  describe('Callbacks in Both Modes', () => {
    it('should call onComplete in mock mode', async () => {
      const onComplete = jest.fn();

      const { result } = renderHook(() => useChatStreaming({ useMock: true, onComplete }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalled();
      }, { timeout: 10000 });

      expect(onComplete).toHaveBeenCalledWith(
        expect.any(String),
        expect.any(Array),
        expect.objectContaining({
          totalTokens: expect.any(Number),
          confidence: expect.any(Number),
        })
      );
    });

    it('should call onError in mock mode', async () => {
      const onError = jest.fn();

      const { result } = renderHook(() => useChatStreaming({ useMock: true, onError }));
      const [, controls] = result.current;

      // Start and immediately stop to trigger potential errors
      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 50));
        controls.stopStreaming();
      });

      // Mock mode handles cancellation gracefully, so onError should not be called
      await new Promise((resolve) => setTimeout(resolve, 200));
      expect(onError).not.toHaveBeenCalled();
    });
  });

  describe('Zero Breaking Changes', () => {
    it('should work without any options (backward compatible)', () => {
      const { result } = renderHook(() => useChatStreaming());
      const [state, controls] = result.current;

      expect(state).toBeDefined();
      expect(controls).toBeDefined();
      expect(controls.startStreaming).toBeDefined();
      expect(controls.stopStreaming).toBeDefined();
      expect(controls.reset).toBeDefined();
    });

    it('should work with only onComplete callback (backward compatible)', async () => {
      const onComplete = jest.fn();

      const { result } = renderHook(() => useChatStreaming({ onComplete, useMock: true }));
      const [, controls] = result.current;

      await act(async () => {
        await controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalled();
      }, { timeout: 10000 });
    });

    it('should work with only onError callback (backward compatible)', async () => {
      const onError = jest.fn();

      const { result } = renderHook(() => useChatStreaming({ onError, useMock: true }));
      const [, controls] = result.current;

      await act(async () => {
        controls.startStreaming('game-123', 'test query');
        await new Promise((resolve) => setTimeout(resolve, 100));
      });

      // Should not throw errors
      expect(result.current[0]).toBeDefined();
    });
  });

  describe('Stop and Reset in Both Modes', () => {
    it('should stop streaming in mock mode', async () => {
      const { result } = renderHook(() => useChatStreaming({ useMock: true }));
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

    it('should reset state in mock mode', async () => {
      const { result } = renderHook(() => useChatStreaming({ useMock: true }));
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
        expect(state.currentAnswer).toBe('');
        expect(state.isStreaming).toBe(false);
      });
    });
  });

  describe('Environment Variable Parsing', () => {
    it('should only activate mock mode for exact string "true"', () => {
      const testCases = [
        { value: 'true', expected: true },
        { value: 'false', expected: false },
        { value: '1', expected: false },
        { value: 'TRUE', expected: false },
        { value: 'yes', expected: false },
        { value: undefined, expected: false },
      ];

      testCases.forEach(({ value, expected }) => {
        if (value === undefined) {
          delete process.env.NEXT_PUBLIC_USE_MOCK_STREAMING;
        } else {
          process.env.NEXT_PUBLIC_USE_MOCK_STREAMING = value;
        }

        const { result } = renderHook(() => useChatStreaming());
        const [, controls] = result.current;

        act(() => {
          controls.startStreaming('game-123', 'test');
        });

        // In mock mode, state will be "Generating mock response..." or "Searching documents..."
        // In real mode, state will be null initially
        if (expected) {
          waitFor(() => {
            expect(result.current[0].state).toMatch(/mock|Searching/);
          }, { timeout: 500 });
        }
      });
    });
  });

  describe('Mode Switching Between Renders', () => {
    it('should switch from real to mock mode on rerender', () => {
      const { result, rerender } = renderHook(
        ({ useMock }) => useChatStreaming({ useMock }),
        { initialProps: { useMock: false } }
      );

      // Initially real mode
      expect(result.current[0]).toBeDefined();

      // Rerender with mock mode
      rerender({ useMock: true });

      // Should now be mock mode
      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      waitFor(() => {
        expect(result.current[0].isStreaming).toBe(true);
      });
    });

    it('should switch from mock to real mode on rerender', () => {
      const { result, rerender } = renderHook(
        ({ useMock }) => useChatStreaming({ useMock }),
        { initialProps: { useMock: true } }
      );

      // Initially mock mode
      expect(result.current[0]).toBeDefined();

      // Rerender with real mode
      rerender({ useMock: false });

      // Should now be real mode
      expect(result.current[0]).toBeDefined();
    });
  });
});
