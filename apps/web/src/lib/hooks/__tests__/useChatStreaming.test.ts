import { renderHook, act, waitFor } from '@testing-library/react';
import { useChatStreaming } from '../useChatStreaming';
import { TextEncoder, TextDecoder } from 'util';

// Polyfill TextEncoder/TextDecoder for jsdom
global.TextEncoder = TextEncoder as any;
global.TextDecoder = TextDecoder as any;

// Mock fetch globally
const mockFetch = jest.fn();
global.fetch = mockFetch;

describe('useChatStreaming', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockFetch.mockReset();
  });

  describe('Initial State', () => {
    it('should have correct initial state', () => {
      const { result } = renderHook(() => useChatStreaming());
      const [state] = result.current;

      expect(state.state).toBeNull();
      expect(state.currentAnswer).toBe('');
      expect(state.snippets).toEqual([]);
      expect(state.totalTokens).toBe(0);
      expect(state.confidence).toBeNull();
      expect(state.isStreaming).toBe(false);
      expect(state.error).toBeNull();
    });

    it('should provide control functions', () => {
      const { result } = renderHook(() => useChatStreaming());
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
      // Mock a never-resolving stream
      mockFetch.mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });
    });

    it('should reset state when starting new stream', async () => {
      mockFetch.mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => useChatStreaming());

      // Set some initial state
      act(() => {
        const [, controls] = result.current;
        controls.startStreaming('game-1', 'query 1');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });

      // Start new stream
      act(() => {
        const [, controls] = result.current;
        controls.startStreaming('game-2', 'query 2');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('');
        expect(state.snippets).toEqual([]);
      });
    });

    it('should call fetch with correct parameters', async () => {
      mockFetch.mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-456', 'How do I play?', 'chat-789');
      });

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledTimes(1);
      });

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/agents/qa/stream'),
        expect.objectContaining({
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            gameId: 'game-456',
            query: 'How do I play?',
            chatId: 'chat-789',
          }),
          credentials: 'include',
          signal: expect.any(AbortSignal),
        })
      );
    });
  });

  describe('Receiving Events', () => {
    const createMockStream = (events: string[]) => {
      const encoder = new TextEncoder();
      const stream = new ReadableStream({
        start(controller) {
          events.forEach((event) => {
            controller.enqueue(encoder.encode(event));
          });
          controller.close();
        },
      });

      return {
        ok: true,
        body: stream,
      };
    };

    it('should handle stateUpdate events', async () => {
      const sseEvents = [
        'event: stateUpdate\ndata: {"state":"Generating embeddings..."}\n\n',
        'event: complete\ndata: {"totalTokens":10,"confidence":0.95,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.state).toBe('Generating embeddings...');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });
    });

    it('should handle citations events', async () => {
      const testSnippets = [
        { text: 'Rule 1', source: 'rules.pdf', page: 1, line: null },
        { text: 'Rule 2', source: 'rules.pdf', page: 2, line: null },
      ];

      const sseEvents = [
        `event: citations\ndata: ${JSON.stringify({ snippets: testSnippets })}\n\n`,
        'event: complete\ndata: {"totalTokens":10,"confidence":0.95,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.snippets).toEqual(testSnippets);
      });
    });

    it('should accumulate tokens', async () => {
      const sseEvents = [
        'event: token\ndata: {"token":"Hello"}\n\n',
        'event: token\ndata: {"token":" "}\n\n',
        'event: token\ndata: {"token":"world"}\n\n',
        'event: complete\ndata: {"totalTokens":3,"confidence":0.95,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('Hello world');
      });
    });

    it('should handle complete event', async () => {
      const sseEvents = [
        'event: token\ndata: {"token":"Answer"}\n\n',
        'event: complete\ndata: {"totalTokens":50,"confidence":0.87,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
        expect(state.totalTokens).toBe(50);
        expect(state.confidence).toBe(0.87);
      });
    });

    it('should handle error events', async () => {
      const sseEvents = [
        'event: error\ndata: {"message":"Something went wrong","code":"INTERNAL_ERROR"}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
        expect(state.error).toBe('Something went wrong');
      });
    });

    it('should ignore heartbeat events', async () => {
      const sseEvents = [
        'event: heartbeat\ndata: null\n\n',
        'event: token\ndata: {"token":"test"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('test');
      });
    });
  });

  describe('Callbacks', () => {
    const createMockStream = (events: string[]) => {
      const encoder = new TextEncoder();
      const stream = new ReadableStream({
        start(controller) {
          events.forEach((event) => {
            controller.enqueue(encoder.encode(event));
          });
          controller.close();
        },
      });

      return {
        ok: true,
        body: stream,
      };
    };

    it('should call onComplete callback', async () => {
      const onComplete = jest.fn();
      const testSnippets = [{ text: 'Test', source: 'test.pdf', page: 1, line: null }];

      const sseEvents = [
        'event: token\ndata: {"token":"Final answer"}\n\n',
        `event: complete\ndata: ${JSON.stringify({ totalTokens: 25, confidence: 0.95, snippets: testSnippets })}\n\n`,
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming({ onComplete }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalledTimes(1);
      });

      expect(onComplete).toHaveBeenCalledWith(
        'Final answer',
        testSnippets,
        { totalTokens: 25, confidence: 0.95 }
      );
    });

    it('should call onError callback', async () => {
      const onError = jest.fn();

      const sseEvents = [
        'event: error\ndata: {"message":"Test error"}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming({ onError }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        expect(onError).toHaveBeenCalledTimes(1);
      });

      expect(onError).toHaveBeenCalledWith('Test error');
    });

    it('should call onError callback on fetch failure', async () => {
      const onError = jest.fn();

      mockFetch.mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useChatStreaming({ onError }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        expect(onError).toHaveBeenCalledTimes(1);
      });

      expect(onError).toHaveBeenCalledWith('Network error');
    });
  });

  describe('Stop Streaming', () => {
    it('should stop streaming and set isStreaming to false', async () => {
      // Mock a never-resolving stream
      mockFetch.mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });

      act(() => {
        controls.stopStreaming();
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });
    });

    it('should not call callbacks when stopped', async () => {
      const onComplete = jest.fn();
      const onError = jest.fn();

      // Mock a stream that would eventually complete
      const createSlowStream = () => {
        const encoder = new TextEncoder();
        const stream = new ReadableStream({
          async start(controller) {
            await new Promise((resolve) => setTimeout(resolve, 100));
            controller.enqueue(encoder.encode('event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n'));
            controller.close();
          },
        });

        return {
          ok: true,
          body: stream,
        };
      };

      mockFetch.mockResolvedValue(createSlowStream());

      const { result } = renderHook(() => useChatStreaming({ onComplete, onError }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });

      act(() => {
        controls.stopStreaming();
      });

      // Wait a bit to ensure the stream would have completed
      await new Promise((resolve) => setTimeout(resolve, 150));

      // Callbacks should not have been called since we stopped early
      expect(onComplete).not.toHaveBeenCalled();
    });
  });

  describe('Reset', () => {
    const createMockStream = (events: string[]) => {
      const encoder = new TextEncoder();
      const stream = new ReadableStream({
        start(controller) {
          events.forEach((event) => {
            controller.enqueue(encoder.encode(event));
          });
          controller.close();
        },
      });

      return {
        ok: true,
        body: stream,
      };
    };

    it('should reset all state to initial values', async () => {
      const sseEvents = [
        'event: token\ndata: {"token":"Some answer"}\n\n',
        'event: complete\ndata: {"totalTokens":10,"confidence":0.8,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      // Start and complete a stream
      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
        expect(state.currentAnswer).toBe('Some answer');
      });

      // Reset
      act(() => {
        controls.reset();
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.state).toBeNull();
        expect(state.currentAnswer).toBe('');
        expect(state.snippets).toEqual([]);
        expect(state.totalTokens).toBe(0);
        expect(state.confidence).toBeNull();
        expect(state.isStreaming).toBe(false);
        expect(state.error).toBeNull();
      });
    });
  });

  describe('HTTP Error Handling', () => {
    it('should handle HTTP 401 error', async () => {
      const onError = jest.fn();

      mockFetch.mockResolvedValue({
        ok: false,
        status: 401,
      });

      const { result } = renderHook(() => useChatStreaming({ onError }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        expect(onError).toHaveBeenCalled();
      });

      expect(onError).toHaveBeenCalledWith(expect.stringContaining('HTTP error'));
    });

    it('should handle missing response body', async () => {
      const onError = jest.fn();

      mockFetch.mockResolvedValue({
        ok: true,
        body: null,
      });

      const { result } = renderHook(() => useChatStreaming({ onError }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        expect(onError).toHaveBeenCalled();
      });

      expect(onError).toHaveBeenCalledWith('No response body');
    });
  });

  describe('Event Format Edge Cases', () => {
    const createMockStream = (events: string[]) => {
      const encoder = new TextEncoder();
      const stream = new ReadableStream({
        start(controller) {
          events.forEach((event) => {
            controller.enqueue(encoder.encode(event));
          });
          controller.close();
        },
      });

      return {
        ok: true,
        body: stream,
      };
    };

    it('should handle events with whitespace', async () => {
      const sseEvents = [
        '  event: token  \n  data: {"token":"test"}  \n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('test');
      });
    });

    it('should handle malformed JSON gracefully', async () => {
      const sseEvents = [
        'event: token\ndata: {invalid json}\n\n',
        'event: token\ndata: {"token":"valid"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        // Should process valid token despite malformed JSON in previous event
        expect(state.currentAnswer).toBe('valid');
      });
    });

    it('should handle empty lines', async () => {
      const sseEvents = [
        '\n\n',
        'event: token\ndata: {"token":"test"}\n\n',
        '\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('test');
      });
    });

    it('should handle unknown event types with warning', async () => {
      const consoleWarnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});

      const sseEvents = [
        'event: unknownType\ndata: {"test":"data"}\n\n',
        'event: token\ndata: {"token":"hello"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('hello');
      });

      expect(consoleWarnSpy).toHaveBeenCalledWith('Unknown event type:', 'unknownType');
      consoleWarnSpy.mockRestore();
    });

    it('should handle events without data field', async () => {
      const sseEvents = [
        'event: token\n\n',
        'event: token\ndata: {"token":"world"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('world');
      });
    });

    it('should handle partial chunks across stream reads', async () => {
      const encoder = new TextEncoder();

      // Split an event across multiple chunks
      const chunk1 = 'event: token\ndata: {"to';
      const chunk2 = 'ken":"test"}\n\n';
      const chunk3 = 'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n';

      const stream = new ReadableStream({
        start(controller) {
          controller.enqueue(encoder.encode(chunk1));
          controller.enqueue(encoder.encode(chunk2));
          controller.enqueue(encoder.encode(chunk3));
          controller.close();
        },
      });

      mockFetch.mockResolvedValue({
        ok: true,
        body: stream,
      });

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('test');
      });
    });

    it('should handle complete event with snippets overriding citations event', async () => {
      const citationsSnippets = [
        { text: 'Early snippet', source: 'doc1.pdf', page: 1, line: null }
      ];
      const completeSnippets = [
        { text: 'Final snippet', source: 'doc2.pdf', page: 2, line: null }
      ];

      const sseEvents = [
        `event: citations\ndata: ${JSON.stringify({ snippets: citationsSnippets })}\n\n`,
        'event: token\ndata: {"token":"answer"}\n\n',
        `event: complete\ndata: ${JSON.stringify({ totalTokens: 10, confidence: 0.95, snippets: completeSnippets })}\n\n`,
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });

      const [state] = result.current;
      expect(state.snippets).toEqual(completeSnippets);
      expect(state.snippets).not.toEqual(citationsSnippets);
    });

    it('should handle null/undefined data in events gracefully', async () => {
      const sseEvents = [
        'event: stateUpdate\ndata: null\n\n',
        'event: citations\ndata: null\n\n',
        'event: token\ndata: null\n\n',
        'event: token\ndata: {"token":"test"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('test');
      });
    });
  });

  describe('Reset Functionality', () => {
    const createMockStream = (events: string[]) => {
      const encoder = new TextEncoder();
      const stream = new ReadableStream({
        start(controller) {
          events.forEach((event) => {
            controller.enqueue(encoder.encode(event));
          });
          controller.close();
        },
      });

      return {
        ok: true,
        body: stream,
      };
    };

    it('should call stopStreaming when reset is invoked', async () => {
      const sseEvents = [
        'event: token\ndata: {"token":"test"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });

      act(() => {
        controls.reset();
      });

      const [state] = result.current;
      expect(state.currentAnswer).toBe('');
      expect(state.state).toBeNull();
      expect(state.isStreaming).toBe(false);
    });

    it('should stop active streaming when reset is called', async () => {
      // Create a never-ending stream
      mockFetch.mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });

      act(() => {
        controls.reset();
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });
    });
  });

  describe('AbortController Handling', () => {
    it('should handle AbortError without calling onError callback', async () => {
      const onError = jest.fn();
      const abortError = new Error('User aborted');
      abortError.name = 'AbortError';

      mockFetch.mockRejectedValue(abortError);

      const { result } = renderHook(() => useChatStreaming({ onError }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });

      expect(onError).not.toHaveBeenCalled();

      const [state] = result.current;
      expect(state.error).toBeNull();
      expect(state.state).toBeNull();
    });

    it('should set isStreaming to false and clear state on AbortError', async () => {
      const abortError = new Error('User cancelled');
      abortError.name = 'AbortError';

      mockFetch.mockRejectedValue(abortError);

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
        expect(state.state).toBeNull();
      });
    });

    it('should abort ongoing request when stopStreaming is called', async () => {
      const encoder = new TextEncoder();
      let abortSignal: AbortSignal | null = null;

      mockFetch.mockImplementation((_url, options) => {
        abortSignal = options?.signal as AbortSignal;

        return new Promise((resolve) => {
          const stream = new ReadableStream({
            start(controller) {
              // Simulate slow stream
              setTimeout(() => {
                controller.enqueue(encoder.encode('event: token\ndata: {"token":"test"}\n\n'));
              }, 100);
            },
          });

          resolve({
            ok: true,
            body: stream,
          });
        });
      });

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        expect(abortSignal).not.toBeNull();
      });

      expect(abortSignal?.aborted).toBe(false);

      act(() => {
        controls.stopStreaming();
      });

      // Note: AbortController.abort() is called synchronously
      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });
    });
  });

  describe('Stream Completion with Callbacks', () => {
    const createMockStream = (events: string[]) => {
      const encoder = new TextEncoder();
      const stream = new ReadableStream({
        start(controller) {
          events.forEach((event) => {
            controller.enqueue(encoder.encode(event));
          });
          controller.close();
        },
      });

      return {
        ok: true,
        body: stream,
      };
    };

    it('should call onComplete with all metadata when stream completes', async () => {
      const onComplete = jest.fn();
      const snippets = [
        { text: 'Snippet 1', source: 'doc.pdf', page: 1, line: null },
        { text: 'Snippet 2', source: 'doc.pdf', page: 2, line: null }
      ];

      const sseEvents = [
        'event: token\ndata: {"token":"Complete "}\n\n',
        'event: token\ndata: {"token":"answer"}\n\n',
        `event: complete\ndata: ${JSON.stringify({ totalTokens: 50, confidence: 0.92, snippets })}\n\n`,
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming({ onComplete }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalledTimes(1);
      });

      expect(onComplete).toHaveBeenCalledWith(
        'Complete answer',
        snippets,
        { totalTokens: 50, confidence: 0.92 }
      );
    });

    it('should update state to not streaming after completion', async () => {
      const onComplete = jest.fn();

      const sseEvents = [
        'event: token\ndata: {"token":"test"}\n\n',
        'event: complete\ndata: {"totalTokens":5,"confidence":0.88,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming({ onComplete }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });

      const [state] = result.current;
      expect(state.state).toBeNull();
      expect(state.totalTokens).toBe(5);
      expect(state.confidence).toBe(0.88);
    });
  });

  describe('Multiple Concurrent Streams', () => {
    const createMockStream = (events: string[]) => {
      const encoder = new TextEncoder();
      const stream = new ReadableStream({
        start(controller) {
          events.forEach((event) => {
            controller.enqueue(encoder.encode(event));
          });
          controller.close();
        },
      });

      return {
        ok: true,
        body: stream,
      };
    };

    it('should cancel previous stream when starting new stream', async () => {
      const onComplete = jest.fn();
      const onError = jest.fn();

      // First stream
      const stream1Events = [
        'event: token\ndata: {"token":"first"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      // Second stream
      const stream2Events = [
        'event: token\ndata: {"token":"second"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValueOnce(createMockStream(stream1Events));
      mockFetch.mockResolvedValueOnce(createMockStream(stream2Events));

      const { result } = renderHook(() => useChatStreaming({ onComplete, onError }));
      const [, controls] = result.current;

      // Start first stream
      act(() => {
        controls.startStreaming('game-1', 'query 1');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });

      // Immediately start second stream
      act(() => {
        controls.startStreaming('game-2', 'query 2');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('second');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });

      // Should complete second stream, not first
      expect(onComplete).toHaveBeenCalledTimes(1);
      expect(onComplete).toHaveBeenCalledWith('second', [], { totalTokens: 1, confidence: 0.9 });
    });

    it('should reset state when starting new stream', async () => {
      const sseEvents1 = [
        'event: token\ndata: {"token":"old content"}\n\n',
        'event: complete\ndata: {"totalTokens":10,"confidence":0.5,"snippets":[]}\n\n',
      ];

      const sseEvents2 = [
        'event: token\ndata: {"token":"new"}\n\n',
        'event: complete\ndata: {"totalTokens":1,"confidence":0.9,"snippets":[]}\n\n',
      ];

      mockFetch.mockResolvedValueOnce(createMockStream(sseEvents1));
      mockFetch.mockResolvedValueOnce(createMockStream(sseEvents2));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      // First stream
      act(() => {
        controls.startStreaming('game-1', 'query 1');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('old content');
      });

      // Second stream should reset
      act(() => {
        controls.startStreaming('game-2', 'query 2');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('new');
      });
    });
  });

  describe('Streaming State Transitions', () => {
    const createMockStream = (events: string[]) => {
      const encoder = new TextEncoder();
      const stream = new ReadableStream({
        start(controller) {
          events.forEach((event) => {
            controller.enqueue(encoder.encode(event));
          });
          controller.close();
        },
      });

      return {
        ok: true,
        body: stream,
      };
    };

    it('should transition through all stream states correctly', async () => {
      const sseEvents = [
        'event: stateUpdate\ndata: {"state":"Generating embeddings..."}\n\n',
        'event: stateUpdate\ndata: {"state":"Searching documents..."}\n\n',
        'event: citations\ndata: {"snippets":[{"text":"Source","source":"doc.pdf","page":1}]}\n\n',
        'event: token\ndata: {"token":"Answer "}\n\n',
        'event: token\ndata: {"token":"text"}\n\n',
        'event: complete\ndata: {"totalTokens":10,"confidence":0.9,"snippets":[{"text":"Source","source":"doc.pdf","page":1}]}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming());
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      // Initial state
      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(true);
      });

      // Check state updates
      await waitFor(() => {
        const [state] = result.current;
        expect(state.state).toBe('Searching documents...');
      });

      // Check snippets received
      await waitFor(() => {
        const [state] = result.current;
        expect(state.snippets.length).toBeGreaterThan(0);
      });

      // Check answer accumulated
      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('Answer text');
      });

      // Final state
      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
        expect(state.state).toBeNull();
        expect(state.totalTokens).toBe(10);
        expect(state.confidence).toBe(0.9);
      });
    });

    it('should handle error state transition', async () => {
      const onError = jest.fn();

      const sseEvents = [
        'event: stateUpdate\ndata: {"state":"Processing..."}\n\n',
        'event: token\ndata: {"token":"Partial"}\n\n',
        'event: error\ndata: {"message":"Something failed","code":"ERR_500"}\n\n',
      ];

      mockFetch.mockResolvedValue(createMockStream(sseEvents));

      const { result } = renderHook(() => useChatStreaming({ onError }));
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.error).toBe('Something failed');
        expect(state.isStreaming).toBe(false);
        expect(state.state).toBeNull();
      });

      expect(onError).toHaveBeenCalledWith('Something failed');
    });
  });
});
