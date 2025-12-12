/**
 * useStreamingChat Hook (Issue #1007)
 *
 * React hook for Server-Sent Events (SSE) streaming chat responses.
 * Manages connection, state, events, and cancellation.
 *
 * Features:
 * - fetch + ReadableStream for POST support
 * - Progressive token accumulation (token-by-token display)
 * - State updates during processing
 * - Citations and sources tracking
 * - Error handling with reconnection
 * - Cancellation via AbortController
 * - Memory leak prevention via cleanup
 *
 * Backend endpoints (Issue #1186):
 * - POST /api/v1/agents/qa/stream
 * - POST /api/v1/agents/explain/stream
 * - POST /api/v1/agents/setup
 *
 * @example
 * ```typescript
 * const [state, controls] = useStreamingChat({
 *   onComplete: (answer) => console.log('Done:', answer),
 *   onError: (error) => console.error(error),
 * });
 *
 * // Start streaming
 * await controls.startStreaming(gameId, userQuestion);
 *
 * // Stop streaming
 * controls.stopStreaming();
 *
 * // Access state
 * console.log(state.currentAnswer, state.isStreaming);
 * ```
 */

import { useState, useCallback, useRef } from 'react';
import { SSEParser } from '@/lib/utils/sseParser';
import {
  StreamingEventType,
  parseEventData,
  type RagStreamingEvent,
  type Citation,
  type TypedStreamingEvent,
} from '@/lib/api/schemas/streaming.schemas';

/**
 * Streaming chat state
 */
export interface StreamingChatState {
  /** Whether streaming is currently active */
  isStreaming: boolean;

  /** Accumulated answer text (token-by-token) */
  currentAnswer: string;

  /** Citations/sources from vector search */
  citations: Citation[];

  /** Current state message (e.g., "Searching vector database...") */
  stateMessage: string;

  /** Answer confidence score (0.0 - 1.0) */
  confidence: number | null;

  /** Error if streaming failed */
  error: Error | null;

  /** Suggested follow-up questions */
  followUpQuestions: string[];

  /** Total tokens used */
  totalTokens: number;

  /** Estimated reading time in minutes */
  estimatedReadingTimeMinutes: number | null;
}

/**
 * Streaming chat controls
 */
export interface StreamingChatControls {
  /** Start streaming chat response (Issue #2051: supports document filtering) */
  startStreaming: (
    gameId: string,
    query: string,
    chatId?: string,
    documentIds?: string[] | null
  ) => Promise<void>;

  /** Stop streaming (cancel request) */
  stopStreaming: () => void;

  /** Reset state to initial */
  reset: () => void;
}

/**
 * Hook options
 */
export interface UseStreamingChatOptions {
  /** Callback when streaming completes successfully */
  onComplete?: (answer: string, citations: Citation[], confidence: number | null) => void;

  /** Callback when an error occurs */
  onError?: (error: Error) => void;

  /** Callback when a token is received (for real-time updates) */
  onToken?: (token: string, accumulated: string) => void;

  /** Callback when state updates (for progress indication) */
  onStateUpdate?: (state: string) => void;

  /** API base URL (defaults to NEXT_PUBLIC_API_BASE) */
  apiBaseUrl?: string;
}

/**
 * Initial state
 */
const initialState: StreamingChatState = {
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

/**
 * useStreamingChat Hook
 *
 * Manages SSE streaming for chat responses with full state management
 *
 * @param options - Hook configuration options
 * @returns [state, controls] tuple
 */
export function useStreamingChat(
  options: UseStreamingChatOptions = {}
): [StreamingChatState, StreamingChatControls] {
  const { onComplete, onError, onToken, onStateUpdate, apiBaseUrl } = options;

  const [state, setState] = useState<StreamingChatState>(initialState);

  // AbortController for cancellation
  const abortControllerRef = useRef<AbortController | null>(null);

  // SSE Parser instance
  const parserRef = useRef<SSEParser>(new SSEParser());

  /**
   * Handle individual streaming event
   */
  const handleEvent = useCallback(
    (event: RagStreamingEvent) => {
      try {
        // Use event.type directly for switch to enable proper type narrowing
        switch (event.type) {
          case StreamingEventType.StateUpdate: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.StateUpdate>;
            const stateMsg = typedEvent.data.state;
            setState(prev => ({ ...prev, stateMessage: stateMsg }));
            onStateUpdate?.(stateMsg);
            break;
          }

          case StreamingEventType.Citations: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Citations>;
            // Check array length, not truthiness, since empty arrays are truthy
            const citationsData =
              typedEvent.data.citations.length > 0
                ? typedEvent.data.citations
                : typedEvent.data.snippets;
            setState(prev => ({ ...prev, citations: citationsData }));
            break;
          }

          case StreamingEventType.Token: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Token>;
            const token = typedEvent.data.token;
            setState(prev => {
              const newAnswer = prev.currentAnswer + token;
              onToken?.(token, newAnswer);
              return { ...prev, currentAnswer: newAnswer };
            });
            break;
          }

          case StreamingEventType.Complete: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Complete>;
            const { totalTokens, confidence, estimatedReadingTimeMinutes, snippets } =
              typedEvent.data;

            setState(prev => {
              const finalCitations = snippets && snippets.length > 0 ? snippets : prev.citations;
              onComplete?.(prev.currentAnswer, finalCitations, confidence || null);

              return {
                ...prev,
                totalTokens,
                confidence: confidence || null,
                estimatedReadingTimeMinutes: estimatedReadingTimeMinutes || null,
                citations: finalCitations,
                isStreaming: false,
                stateMessage: 'Complete',
              };
            });
            break;
          }

          case StreamingEventType.Error: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Error>;
            const error = new Error(typedEvent.data.message);
            error.name = typedEvent.data.code;
            setState(prev => ({
              ...prev,
              error,
              isStreaming: false,
              stateMessage: 'Error',
            }));
            onError?.(error);
            break;
          }

          case StreamingEventType.FollowUpQuestions: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.FollowUpQuestions>;
            const questions = typedEvent.data.questions;
            setState(prev => ({ ...prev, followUpQuestions: questions }));
            break;
          }

          case StreamingEventType.SetupStep: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.SetupStep>;
            // For setup guide streaming (future enhancement)
            // Currently just log, can be extended for setup UI
            console.log('[useStreamingChat] Setup step:', typedEvent.data.step);
            break;
          }
        }
      } catch (error) {
        console.error('[useStreamingChat] Event handling error:', error, event);
      }
    },
    [onComplete, onError, onToken, onStateUpdate]
  );

  /**
   * Start streaming chat response
   * Issue #2051: Supports document filtering via documentIds
   */
  const startStreaming = useCallback(
    async (gameId: string, query: string, chatId?: string, documentIds?: string[] | null) => {
      // Reset and mark streaming in a single update for consistency
      setState({ ...initialState, isStreaming: true, stateMessage: 'Connecting...' });

      // Allow state to propagate before potentially fast test streams complete
      await new Promise(resolve => setTimeout(resolve, 5));

      // Create new AbortController
      const controller = new AbortController();
      abortControllerRef.current = controller;

      // Reset parser
      parserRef.current.reset();

      const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';
      const endpoint = `${baseUrl}/api/v1/agents/qa/stream`;

      try {
        const response = await fetch(endpoint, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          credentials: 'include', // Send session cookies
          body: JSON.stringify({
            gameId,
            query,
            chatId: chatId || null,
            documentIds: documentIds ?? null, // Issue #2051: Document filtering
          }),
          signal: controller.signal,
        });

        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        if (!response.body) {
          throw new Error('No response body');
        }

        // Read stream
        const reader = response.body.getReader();
        const decoder = new TextDecoder();

        while (true) {
          const { done, value } = await reader.read();

          if (done) {
            break;
          }

          // Decode chunk
          const chunk = decoder.decode(value, { stream: true });

          // Parse events
          const events = parserRef.current.parse(chunk);

          // Handle each event
          events.forEach(handleEvent);
        }
      } catch (error) {
        if (error instanceof Error) {
          if (error.name === 'AbortError') {
            // User cancelled, not an error
            setState(prev => ({
              ...prev,
              isStreaming: false,
              stateMessage: 'Cancelled',
            }));
          } else {
            setState(prev => ({
              ...prev,
              error,
              isStreaming: false,
              stateMessage: 'Error',
            }));
            onError?.(error);
          }
        }
      } finally {
        abortControllerRef.current = null;
      }
    },
    [apiBaseUrl, handleEvent, onError]
  );

  /**
   * Stop streaming (cancel request)
   */
  const stopStreaming = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
    // Immediately reflect cancellation in state for responsive UI/tests
    setState(prev => ({
      ...prev,
      isStreaming: false,
      stateMessage: 'Cancelled',
    }));
  }, []);

  /**
   * Reset state to initial
   */
  const reset = useCallback(() => {
    setState(initialState);
    parserRef.current.reset();
  }, []);

  const controls: StreamingChatControls = {
    startStreaming,
    stopStreaming,
    reset,
  };

  return [state, controls];
}
