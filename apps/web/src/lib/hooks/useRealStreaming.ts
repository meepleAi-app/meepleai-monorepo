/**
 * useRealStreaming Helper (Issue #1451)
 *
 * Real SSE streaming implementation using fetch with ReadableStream.
 * Extracted from useChatStreaming for consolidation into unified hook.
 *
 * Features:
 * - Real Server-Sent Events (SSE) via fetch + ReadableStream
 * - Event-driven state updates (token, stateUpdate, citations, complete, error, heartbeat, followUpQuestions)
 * - Proper cancellation with AbortController
 * - Citation and snippet support
 * - Compatible with unified streaming interface
 */

import { useState, useCallback, useRef } from 'react';

import type { Citation } from '@/types';

// Event types that match the backend
type StreamingEventType =
  | 'token'
  | 'stateUpdate'
  | 'citations'
  | 'complete'
  | 'error'
  | 'heartbeat'
  | 'followUpQuestions';

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
  snippets?: Snippet[];
  citations?: Citation[];
};

type TokenData = {
  token: string;
};

type CompleteData = {
  totalTokens: number;
  confidence?: number;
  snippets?: Snippet[];
  citations?: Citation[];
};

type ErrorData = {
  message: string;
  code?: string;
};

type FollowUpQuestionsData = {
  questions: string[];
};

type _StreamingEvent = {
  type: StreamingEventType;
  data:
    | StateUpdateData
    | CitationsData
    | TokenData
    | CompleteData
    | ErrorData
    | FollowUpQuestionsData
    | null;
};

/**
 * Real streaming state
 */
export interface RealStreamingState {
  state: string | null;
  currentAnswer: string;
  snippets: Snippet[];
  citations: Citation[];
  followUpQuestions: string[];
  totalTokens: number;
  confidence: number | null;
  isStreaming: boolean;
  error: string | null;
}

/**
 * Real streaming controls
 */
export interface RealStreamingControls {
  startStreaming: (gameId: string, query: string, chatId?: string, searchMode?: string) => void;
  stopStreaming: () => void;
  reset: () => void;
}

/**
 * Real streaming callbacks
 */
export interface RealStreamingCallbacks {
  onComplete?: (
    answer: string,
    snippets: Snippet[],
    metadata: {
      totalTokens: number;
      confidence: number | null;
      followUpQuestions?: string[];
      citations?: Citation[];
    }
  ) => void;
  onError?: (error: string) => void;
}

const INITIAL_STATE: RealStreamingState = {
  state: null,
  currentAnswer: '',
  snippets: [],
  citations: [],
  followUpQuestions: [],
  totalTokens: 0,
  confidence: null,
  isStreaming: false,
  error: null,
};

/**
 * Real SSE streaming implementation hook
 *
 * @param callbacks - Optional completion and error callbacks
 * @returns [state, controls] tuple
 *
 * @example
 * ```tsx
 * const [state, controls] = useRealStreaming({
 *   onComplete: (answer, snippets, metadata) => {
 *     console.log('Streaming complete:', answer);
 *   }
 * });
 *
 * controls.startStreaming(gameId, query);
 * ```
 */
export function useRealStreaming(
  callbacks?: RealStreamingCallbacks
): [RealStreamingState, RealStreamingControls] {
  const [state, setState] = useState<RealStreamingState>(INITIAL_STATE);
  const abortControllerRef = useRef<AbortController | null>(null);

  /**
   * Stable ref to callbacks to prevent startStreaming recreation
   */
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  const stopStreaming = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
    setState(prev => ({ ...prev, isStreaming: false }));
  }, []);

  const reset = useCallback(() => {
    stopStreaming();
    setState(INITIAL_STATE);
  }, [stopStreaming]);

  const startStreaming = useCallback(
    (gameId: string, query: string, chatId?: string, searchMode: string = 'Hybrid') => {
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

      // EventSource doesn't support POST, so we use fetch with ReadableStream
      const requestBody = JSON.stringify({ gameId, query, chatId, searchMode });

      fetch(url.toString(), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: requestBody,
        credentials: 'include', // Include cookies for authentication
        signal: abortController.signal,
      })
        .then(async response => {
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
                let eventData: unknown = null;

                try {
                  eventData = JSON.parse(dataMatch[1]);
                } catch {
                  // Ignore parse errors for non-JSON data
                }

                // Handle different event types
                switch (eventType) {
                  case 'stateUpdate':
                    setState(prev => ({
                      ...prev,
                      state: (eventData as StateUpdateData)?.state || null,
                    }));
                    break;

                  case 'citations': {
                    const citationsData = eventData as CitationsData;
                    setState(prev => ({
                      ...prev,
                      snippets: citationsData?.snippets || prev.snippets,
                      citations: citationsData?.citations || prev.citations,
                    }));
                    break;
                  }

                  case 'token':
                    setState(prev => ({
                      ...prev,
                      currentAnswer: prev.currentAnswer + ((eventData as TokenData)?.token || ''),
                    }));
                    break;

                  case 'followUpQuestions': {
                    const followUpData = eventData as FollowUpQuestionsData;
                    setState(prev => ({
                      ...prev,
                      followUpQuestions: followUpData?.questions || [],
                    }));
                    break;
                  }

                  case 'complete': {
                    const completeData = eventData as CompleteData;
                    setState(prev => {
                      const finalState = {
                        ...prev,
                        totalTokens: completeData?.totalTokens || 0,
                        confidence: completeData?.confidence || null,
                        snippets: completeData?.snippets || prev.snippets,
                        citations: completeData?.citations || prev.citations,
                        isStreaming: false,
                        state: null,
                      };

                      // Call completion callback
                      if (callbacksRef.current?.onComplete) {
                        callbacksRef.current.onComplete(
                          finalState.currentAnswer,
                          finalState.snippets,
                          {
                            totalTokens: finalState.totalTokens,
                            confidence: finalState.confidence,
                            followUpQuestions:
                              prev.followUpQuestions.length > 0
                                ? prev.followUpQuestions
                                : undefined,
                            citations:
                              finalState.citations.length > 0 ? finalState.citations : undefined,
                          }
                        );
                      }

                      return finalState;
                    });
                    break;
                  }

                  case 'error': {
                    const errorData = eventData as ErrorData;
                    const errorMessage = errorData?.message || 'Unknown error occurred';
                    setState(prev => ({
                      ...prev,
                      error: errorMessage,
                      isStreaming: false,
                      state: null,
                    }));
                    if (callbacksRef.current?.onError) {
                      callbacksRef.current.onError(errorMessage);
                    }
                    break;
                  }

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
        .catch(error => {
          // Don't treat abort as an error
          if (error.name === 'AbortError') {
            setState(prev => ({
              ...prev,
              isStreaming: false,
              state: null,
            }));
            return;
          }

          const errorMessage = error instanceof Error ? error.message : 'Failed to stream response';
          setState(prev => ({
            ...prev,
            error: errorMessage,
            isStreaming: false,
            state: null,
          }));
          if (callbacksRef.current?.onError) {
            callbacksRef.current.onError(errorMessage);
          }
        });
    },
    [stopStreaming]
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
