import { useCallback, useRef, useState } from 'react';

// Event types that match the backend
type StreamingEventType = 'token' | 'stateUpdate' | 'citations' | 'complete' | 'error' | 'heartbeat';

type Snippet = {
  text: string;
  source: string;
  page?: number | null;
  line?: number | null;
};

type StateUpdateData = {
  state: string;
};

type CitationsData = {
  snippets: Snippet[];
};

type TokenData = {
  token: string;
};

type CompleteData = {
  totalTokens: number;
  confidence?: number;
  snippets: Snippet[];
};

type ErrorData = {
  message: string;
  code?: string;
};

type StreamingEvent = {
  type: StreamingEventType;
  data: StateUpdateData | CitationsData | TokenData | CompleteData | ErrorData | null;
};

export type StreamingState = {
  state: string | null;
  currentAnswer: string;
  snippets: Snippet[];
  totalTokens: number;
  confidence: number | null;
  isStreaming: boolean;
  error: string | null;
};

export type StreamingControls = {
  startStreaming: (gameId: string, query: string, chatId?: string) => void;
  stopStreaming: () => void;
  reset: () => void;
};

const INITIAL_STATE: StreamingState = {
  state: null,
  currentAnswer: '',
  snippets: [],
  totalTokens: 0,
  confidence: null,
  isStreaming: false,
  error: null,
};

/**
 * React hook for streaming QA responses using Server-Sent Events (SSE)
 *
 * Usage:
 * ```tsx
 * const [streamingState, streamingControls] = useChatStreaming({
 *   onComplete: (answer, snippets, metadata) => {
 *     // Handle completed response
 *   },
 *   onError: (error) => {
 *     // Handle error
 *   }
 * });
 *
 * // Start streaming
 * streamingControls.startStreaming(gameId, userQuery, chatId);
 *
 * // Stop streaming
 * streamingControls.stopStreaming();
 * ```
 */
export function useChatStreaming(callbacks?: {
  onComplete?: (answer: string, snippets: Snippet[], metadata: { totalTokens: number; confidence: number | null }) => void;
  onError?: (error: string) => void;
}): [StreamingState, StreamingControls] {
  const [state, setState] = useState<StreamingState>(INITIAL_STATE);
  const eventSourceRef = useRef<EventSource | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const stopStreaming = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
    setState((prev) => ({ ...prev, isStreaming: false }));
  }, []);

  const reset = useCallback(() => {
    stopStreaming();
    setState(INITIAL_STATE);
  }, [stopStreaming]);

  const startStreaming = useCallback(
    (gameId: string, query: string, chatId?: string) => {
      // Stop any existing stream
      stopStreaming();

      // Reset state
      setState({
        ...INITIAL_STATE,
        isStreaming: true,
      });

      // Create AbortController for cancellation
      const abortController = new AbortController();
      abortControllerRef.current = abortController;

      // Build URL with query parameters
      const baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
      const url = new URL('/api/v1/agents/qa/stream', baseUrl);

      // EventSource doesn't support POST, so we need to use fetch with streaming
      // We'll use fetch with ReadableStream instead of EventSource for proper POST support
      const requestBody = JSON.stringify({ gameId, query, chatId });

      fetch(url.toString(), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: requestBody,
        credentials: 'include', // Include cookies for authentication
        signal: abortController.signal,
      })
        .then(async (response) => {
          if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
          }

          const reader = response.body?.getReader();
          if (!reader) {
            throw new Error('No response body');
          }

          const decoder = new TextDecoder();
          let buffer = '';

          // Read the stream
          while (true) {
            const { done, value } = await reader.read();

            if (done) {
              break;
            }

            // Decode chunk and add to buffer
            buffer += decoder.decode(value, { stream: true });

            // Process complete SSE messages (separated by \n\n)
            const lines = buffer.split('\n\n');
            buffer = lines.pop() || ''; // Keep incomplete message in buffer

            for (const line of lines) {
              if (!line.trim()) continue;

              // Parse SSE format: "event: eventType\ndata: jsonData"
              const eventMatch = line.match(/event:\s*(\w+)/);
              const dataMatch = line.match(/data:\s*([\s\S]+)/);

              if (eventMatch && dataMatch) {
                const eventType = eventMatch[1] as StreamingEventType;
                let eventData: any = null;

                try {
                  eventData = JSON.parse(dataMatch[1]);
                } catch {
                  // Ignore parse errors for non-JSON data
                }

                // Handle different event types
                switch (eventType) {
                  case 'stateUpdate':
                    setState((prev) => ({
                      ...prev,
                      state: (eventData as StateUpdateData)?.state || null,
                    }));
                    break;

                  case 'citations':
                    setState((prev) => ({
                      ...prev,
                      snippets: (eventData as CitationsData)?.snippets || [],
                    }));
                    break;

                  case 'token':
                    setState((prev) => ({
                      ...prev,
                      currentAnswer: prev.currentAnswer + ((eventData as TokenData)?.token || ''),
                    }));
                    break;

                  case 'complete':
                    const completeData = eventData as CompleteData;
                    setState((prev) => {
                      const finalState = {
                        ...prev,
                        totalTokens: completeData?.totalTokens || 0,
                        confidence: completeData?.confidence || null,
                        snippets: completeData?.snippets || prev.snippets,
                        isStreaming: false,
                        state: null,
                      };

                      // Call completion callback
                      if (callbacks?.onComplete) {
                        callbacks.onComplete(finalState.currentAnswer, finalState.snippets, {
                          totalTokens: finalState.totalTokens,
                          confidence: finalState.confidence,
                        });
                      }

                      return finalState;
                    });
                    break;

                  case 'error':
                    const errorData = eventData as ErrorData;
                    const errorMessage = errorData?.message || 'Unknown error occurred';
                    setState((prev) => ({
                      ...prev,
                      error: errorMessage,
                      isStreaming: false,
                      state: null,
                    }));
                    if (callbacks?.onError) {
                      callbacks.onError(errorMessage);
                    }
                    break;

                  case 'heartbeat':
                    // Ignore heartbeat events (keep connection alive)
                    break;

                  default:
                    console.warn('Unknown event type:', eventType);
                }
              }
            }
          }
        })
        .catch((error) => {
          // Don't treat abort as an error
          if (error.name === 'AbortError') {
            setState((prev) => ({
              ...prev,
              isStreaming: false,
              state: null,
            }));
            return;
          }

          const errorMessage = error instanceof Error ? error.message : 'Failed to stream response';
          setState((prev) => ({
            ...prev,
            error: errorMessage,
            isStreaming: false,
            state: null,
          }));
          if (callbacks?.onError) {
            callbacks.onError(errorMessage);
          }
        });
    },
    [callbacks, stopStreaming]
  );

  return [
    state,
    {
      startStreaming,
      stopStreaming,
      reset,
    },
  ];
}
