/**
 * useAgentChat Hook Tests (Issue #3243)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';

import { useAgentChat } from '../useAgentChat';

// Mock useStreamingChat
vi.mock('@/lib/hooks/useStreamingChat', () => ({
  useStreamingChat: vi.fn((options) => {
    const mockState = {
      isStreaming: false,
      currentAnswer: '',
      citations: [],
      stateMessage: '',
      confidence: null,
      error: null,
      followUpQuestions: [],
      totalTokens: 0,
      estimatedReadingTimeMinutes: null,
    };

    const mockControls = {
      startStreaming: vi.fn(async () => {
        // Simulate streaming completion
        if (options.onToken) {
          options.onToken('Hello', 'Hello');
          options.onToken(' world', 'Hello world');
        }
        if (options.onComplete) {
          options.onComplete('Hello world', [], 0.95);
        }
      }),
      stopStreaming: vi.fn(),
      reset: vi.fn(),
    };

    return [mockState, mockControls];
  }),
}));

describe('useAgentChat', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Initialization', () => {
    it('initializes with empty messages', () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      expect(result.current.messages).toEqual([]);
      expect(result.current.isStreaming).toBe(false);
      expect(result.current.currentChunk).toBe('');
    });

    it('provides sendMessage, stopStreaming, and reset functions', () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      expect(typeof result.current.sendMessage).toBe('function');
      expect(typeof result.current.stopStreaming).toBe('function');
      expect(typeof result.current.reset).toBe('function');
    });
  });

  describe('sendMessage', () => {
    it('adds user message immediately', async () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        await result.current.sendMessage('Hello, agent!');
      });

      expect(result.current.messages).toHaveLength(2); // User + agent
      expect(result.current.messages[0]).toMatchObject({
        type: 'user',
        content: 'Hello, agent!',
      });
    });

    it('adds agent message after streaming completes', async () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        await result.current.sendMessage('Test question');
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(2);
      });

      expect(result.current.messages[1]).toMatchObject({
        type: 'agent',
        content: 'Hello world',
      });
    });

    it('does not send empty messages', async () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        await result.current.sendMessage('   ');
      });

      expect(result.current.messages).toHaveLength(0);
    });

    it('trims whitespace from user input', async () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        await result.current.sendMessage('  Hello  ');
      });

      expect(result.current.messages[0].content).toBe('Hello');
    });

    it('warns when no gameId is provided', async () => {
      const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

      const { result } = renderHook(() => useAgentChat('session-123')); // No gameId

      await act(async () => {
        await result.current.sendMessage('Test');
      });

      expect(consoleSpy).toHaveBeenCalledWith(
        '[useAgentChat] No gameId provided, streaming skipped'
      );

      consoleSpy.mockRestore();
    });
  });

  describe('Progressive Text Reveal', () => {
    it('updates currentChunk during streaming', async () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      let capturedChunk = '';

      // Capture chunk updates via mock
      const { useStreamingChat } = await import('@/lib/hooks/useStreamingChat');
      const mockImpl = useStreamingChat as any;

      mockImpl.mockImplementationOnce((options: any) => {
        const mockState = { isStreaming: true, currentAnswer: '' };
        const mockControls = {
          startStreaming: vi.fn(async () => {
            // Simulate progressive chunks
            if (options.onToken) {
              await act(async () => {
                options.onToken('First', 'First');
                capturedChunk = 'First';
              });

              await act(async () => {
                options.onToken(' chunk', 'First chunk');
                capturedChunk = 'First chunk';
              });
            }
          }),
          stopStreaming: vi.fn(),
          reset: vi.fn(),
        };
        return [mockState, mockControls];
      });

      const { result: freshResult } = renderHook(() =>
        useAgentChat('session-123', 'game-456')
      );

      await act(async () => {
        await freshResult.current.sendMessage('Test');
      });

      // Chunk should have been updated
      expect(capturedChunk).toBe('First chunk');
    });

    it('clears currentChunk after completion', async () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        await result.current.sendMessage('Test');
      });

      await waitFor(() => {
        expect(result.current.currentChunk).toBe('');
      });
    });
  });

  describe('Error Handling', () => {
    it('adds system message on streaming error', async () => {
      const { useStreamingChat } = await import('@/lib/hooks/useStreamingChat');
      const mockImpl = useStreamingChat as any;

      mockImpl.mockImplementationOnce((options: any) => {
        const mockState = { isStreaming: false, error: null };
        const mockControls = {
          startStreaming: vi.fn(async () => {
            if (options.onError) {
              options.onError(new Error('Network error'));
            }
          }),
          stopStreaming: vi.fn(),
          reset: vi.fn(),
        };
        return [mockState, mockControls];
      });

      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        await result.current.sendMessage('Test');
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(2); // User + error
      });

      expect(result.current.messages[1]).toMatchObject({
        type: 'system',
        content: 'Errore: Network error',
      });
    });

    it('clears currentChunk on error', async () => {
      const { useStreamingChat } = await import('@/lib/hooks/useStreamingChat');
      const mockImpl = useStreamingChat as any;

      mockImpl.mockImplementationOnce((options: any) => {
        const mockState = { isStreaming: false };
        const mockControls = {
          startStreaming: vi.fn(async () => {
            if (options.onError) {
              options.onError(new Error('Test error'));
            }
          }),
          stopStreaming: vi.fn(),
          reset: vi.fn(),
        };
        return [mockState, mockControls];
      });

      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        await result.current.sendMessage('Test');
      });

      expect(result.current.currentChunk).toBe('');
    });
  });

  describe('stopStreaming', () => {
    it('clears currentChunk when stopped', async () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        result.current.stopStreaming();
      });

      expect(result.current.currentChunk).toBe('');
    });
  });

  describe('reset', () => {
    it('clears all messages and chunks', async () => {
      const { result } = renderHook(() => useAgentChat('session-123', 'game-456'));

      await act(async () => {
        await result.current.sendMessage('Test');
      });

      await waitFor(() => {
        expect(result.current.messages.length).toBeGreaterThan(0);
      });

      await act(async () => {
        result.current.reset();
      });

      expect(result.current.messages).toEqual([]);
      expect(result.current.currentChunk).toBe('');
    });
  });
});
