/**
 * useStreamingChatWithReconnect Hook (Issue #2054)
 *
 * Enhanced SSE streaming with auto-reconnect capabilities:
 * - Exponential backoff reconnection (max 3 attempts)
 * - "Resuming" state indicator
 * - Duplicate message prevention via event IDs
 * - Network status integration
 * - Graceful degradation with retry button
 *
 * Extends useStreamingChat with resilience features.
 *
 * @example
 * ```tsx
 * const [state, controls] = useStreamingChatWithReconnect({
 *   onComplete: (answer) => addMessage(answer),
 *   onReconnecting: () => showResumeIndicator(),
 *   onReconnectFailed: () => showRetryButton(),
 * });
 *
 * // Start streaming
 * await controls.startStreaming(gameId, query);
 *
 * // Manual retry after max attempts
 * controls.retryStreaming();
 * ```
 */

import { useState, useCallback, useRef, useEffect } from 'react';

import { useNetworkStatus } from '@/hooks/useNetworkStatus';
import {
  StreamingEventType,
  parseEventData,
  type RagStreamingEvent,
  type Citation,
  type TypedStreamingEvent,
} from '@/lib/api/schemas/streaming.schemas';
import { SSEParser } from '@/lib/utils/sseParser';

// ============================================================================
// Constants
// ============================================================================

const MAX_RECONNECT_ATTEMPTS = 3;
const INITIAL_RECONNECT_DELAY_MS = 1000;
const MAX_RECONNECT_DELAY_MS = 30000;
const JITTER_FACTOR = 0.3;

// ============================================================================
// Types
// ============================================================================

export interface StreamingChatWithReconnectState {
  /** Whether streaming is currently active */
  isStreaming: boolean;

  /** Accumulated answer text (token-by-token) */
  currentAnswer: string;

  /** Citations/sources from vector search */
  citations: Citation[];

  /** Current state message (e.g., "Searching...", "Reconnecting...") */
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

  /** Whether currently reconnecting */
  isReconnecting: boolean;

  /** Current reconnect attempt number */
  reconnectAttempt: number;

  /** Whether max reconnect attempts reached */
  reconnectFailed: boolean;

  /** Last processed event ID (for deduplication) */
  lastEventId: string | null;
}

export interface StreamingChatWithReconnectControls {
  /** Start streaming chat response */
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

  /** Manually retry after reconnect failure */
  retryStreaming: () => Promise<void>;
}

export interface UseStreamingChatWithReconnectOptions {
  /** Callback when streaming completes successfully */
  onComplete?: (answer: string, citations: Citation[], confidence: number | null) => void;

  /** Callback when an error occurs */
  onError?: (error: Error) => void;

  /** Callback when a token is received */
  onToken?: (token: string, accumulated: string) => void;

  /** Callback when state updates */
  onStateUpdate?: (state: string) => void;

  /** Callback when reconnecting */
  onReconnecting?: (attempt: number) => void;

  /** Callback when max reconnect attempts reached */
  onReconnectFailed?: () => void;

  /** Callback when reconnection succeeds */
  onReconnectSuccess?: () => void;

  /** API base URL */
  apiBaseUrl?: string;

  /** Max reconnect attempts (default: 3) */
  maxReconnectAttempts?: number;
}

// ============================================================================
// Initial State
// ============================================================================

const initialState: StreamingChatWithReconnectState = {
  isStreaming: false,
  currentAnswer: '',
  citations: [],
  stateMessage: '',
  confidence: null,
  error: null,
  followUpQuestions: [],
  totalTokens: 0,
  estimatedReadingTimeMinutes: null,
  isReconnecting: false,
  reconnectAttempt: 0,
  reconnectFailed: false,
  lastEventId: null,
};

// ============================================================================
// Utility: Calculate Backoff with Jitter
// ============================================================================

function calculateBackoffDelay(attempt: number): number {
  const exponentialDelay = INITIAL_RECONNECT_DELAY_MS * Math.pow(2, attempt);
  const cappedDelay = Math.min(exponentialDelay, MAX_RECONNECT_DELAY_MS);
  // Apply jitter: random variation of ±JITTER_FACTOR
  const jitter = cappedDelay * JITTER_FACTOR * (Math.random() * 2 - 1);
  return Math.round(cappedDelay + jitter);
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useStreamingChatWithReconnect(
  options: UseStreamingChatWithReconnectOptions = {}
): [StreamingChatWithReconnectState, StreamingChatWithReconnectControls] {
  const {
    onComplete,
    onError,
    onToken,
    onStateUpdate,
    onReconnecting,
    onReconnectFailed,
    onReconnectSuccess,
    apiBaseUrl,
    maxReconnectAttempts = MAX_RECONNECT_ATTEMPTS,
  } = options;

  const [state, setState] = useState<StreamingChatWithReconnectState>(initialState);
  const {
    isOnline,
    startReconnecting,
    stopReconnecting,
    incrementReconnectAttempts,
    resetReconnectAttempts,
  } = useNetworkStatus();

  // Refs for connection management
  const abortControllerRef = useRef<AbortController | null>(null);
  const parserRef = useRef<SSEParser>(new SSEParser());
  const reconnectTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const lastRequestRef = useRef<{
    gameId: string;
    query: string;
    chatId?: string;
    documentIds?: string[] | null;
  } | null>(null);
  const processedEventIdsRef = useRef<Set<string>>(new Set());
  // Ref to store executeStreaming to break circular dependency
  const executeStreamingRef = useRef<
    (
      gameId: string,
      query: string,
      chatId?: string,
      documentIds?: string[] | null,
      isReconnect?: boolean
    ) => Promise<void>
  >(() => Promise.resolve());
  // Ref to store attemptReconnect to break circular dependency
  const attemptReconnectRef = useRef<() => Promise<void>>(() => Promise.resolve());

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
      }
      abortControllerRef.current?.abort();
    };
  }, []);

  /**
   * Handle individual streaming event with deduplication
   */
  const handleEvent = useCallback(
    (event: RagStreamingEvent) => {
      // Deduplication via event timestamp
      const eventKey = `${event.type}_${event.timestamp}`;
      if (processedEventIdsRef.current.has(eventKey)) {
        return; // Skip duplicate
      }
      processedEventIdsRef.current.add(eventKey);

      // Limit set size to prevent memory leak
      if (processedEventIdsRef.current.size > 1000) {
        const entries = Array.from(processedEventIdsRef.current);
        processedEventIdsRef.current = new Set(entries.slice(-500));
      }

      try {
        switch (event.type) {
          case StreamingEventType.StateUpdate: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.StateUpdate>;
            const stateMsg = typedEvent.data.state;
            setState(prev => ({ ...prev, stateMessage: stateMsg, lastEventId: eventKey }));
            onStateUpdate?.(stateMsg);
            break;
          }

          case StreamingEventType.Citations: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Citations>;
            const citationsData =
              typedEvent.data.citations.length > 0
                ? typedEvent.data.citations
                : typedEvent.data.snippets;
            setState(prev => ({ ...prev, citations: citationsData, lastEventId: eventKey }));
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
              return { ...prev, currentAnswer: newAnswer, lastEventId: eventKey };
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
                isReconnecting: false,
                reconnectAttempt: 0,
                reconnectFailed: false,
                stateMessage: 'Completo',
                lastEventId: eventKey,
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
              isReconnecting: false,
              stateMessage: 'Errore',
              lastEventId: eventKey,
            }));
            onError?.(error);
            break;
          }

          case StreamingEventType.FollowUpQuestions: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.FollowUpQuestions>;
            setState(prev => ({
              ...prev,
              followUpQuestions: typedEvent.data.questions,
              lastEventId: eventKey,
            }));
            break;
          }

          case StreamingEventType.SetupStep: {
            // Handle setup step events if needed
            break;
          }
        }
      } catch (error) {
        console.error('[useStreamingChatWithReconnect] Event handling error:', error, event);
      }
    },
    [onComplete, onError, onToken, onStateUpdate]
  );

  /**
   * Attempt to reconnect with exponential backoff
   */
  const attemptReconnect = useCallback(async () => {
    const currentAttempt = state.reconnectAttempt + 1;

    if (currentAttempt > maxReconnectAttempts || !lastRequestRef.current) {
      // Max attempts reached
      setState(prev => ({
        ...prev,
        isReconnecting: false,
        reconnectFailed: true,
        stateMessage: 'Riconnessione fallita. Premi Riprova.',
      }));
      stopReconnecting();
      onReconnectFailed?.();
      return;
    }

    // Update state for reconnecting
    setState(prev => ({
      ...prev,
      isReconnecting: true,
      reconnectAttempt: currentAttempt,
      stateMessage: `Riconnessione... (tentativo ${currentAttempt}/${maxReconnectAttempts})`,
    }));
    startReconnecting();
    incrementReconnectAttempts();
    onReconnecting?.(currentAttempt);

    // Calculate backoff delay
    const delay = calculateBackoffDelay(currentAttempt - 1);

    // Wait before attempting reconnect
    await new Promise(resolve => {
      reconnectTimeoutRef.current = setTimeout(resolve, delay);
    });

    // Check if still online and should reconnect
    if (!isOnline) {
      // Wait for online status
      return;
    }

    // Attempt to reconnect by re-calling startStreaming
    const { gameId, query, chatId, documentIds } = lastRequestRef.current;

    try {
      await executeStreamingRef.current(gameId, query, chatId, documentIds, true);
      // Success - reconnection worked
      resetReconnectAttempts();
      stopReconnecting();
      onReconnectSuccess?.();
    } catch {
      // Will trigger another reconnect attempt via error handling
    }
  }, [
    state.reconnectAttempt,
    maxReconnectAttempts,
    isOnline,
    startReconnecting,
    stopReconnecting,
    incrementReconnectAttempts,
    resetReconnectAttempts,
    onReconnecting,
    onReconnectFailed,
    onReconnectSuccess,
  ]);

  /**
   * Execute the actual streaming request
   */
  const executeStreaming = useCallback(
    async (
      gameId: string,
      query: string,
      chatId?: string,
      documentIds?: string[] | null,
      isReconnect = false
    ) => {
      // Create new AbortController
      const controller = new AbortController();
      abortControllerRef.current = controller;

      // Only reset parser and state if not a reconnect
      if (!isReconnect) {
        parserRef.current.reset();
        processedEventIdsRef.current.clear();
        setState({
          ...initialState,
          isStreaming: true,
          stateMessage: 'Connessione...',
        });
      } else {
        setState(prev => ({
          ...prev,
          isStreaming: true,
          isReconnecting: true,
          stateMessage: 'Ripresa...',
        }));
      }

      // Small delay for state propagation
      await new Promise(resolve => setTimeout(resolve, 5));

      const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';
      const endpoint = `${baseUrl}/api/v1/agents/qa/stream`;

      try {
        const response = await fetch(endpoint, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          credentials: 'include',
          body: JSON.stringify({
            gameId,
            query,
            chatId: chatId || null,
            documentIds: documentIds ?? null,
          }),
          signal: controller.signal,
        });

        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        if (!response.body) {
          throw new Error('No response body');
        }

        // Connection successful - update state
        if (isReconnect) {
          setState(prev => ({
            ...prev,
            isReconnecting: false,
            reconnectAttempt: 0,
            reconnectFailed: false,
          }));
        }

        // Read stream
        const reader = response.body.getReader();
        const decoder = new TextDecoder();

        while (true) {
          const { done, value } = await reader.read();

          if (done) {
            break;
          }

          const chunk = decoder.decode(value, { stream: true });
          const events = parserRef.current.parse(chunk);
          events.forEach(handleEvent);
        }
      } catch (error) {
        if (error instanceof Error) {
          if (error.name === 'AbortError') {
            // User cancelled
            setState(prev => ({
              ...prev,
              isStreaming: false,
              isReconnecting: false,
              stateMessage: 'Annullato',
            }));
          } else {
            // Connection error - attempt reconnect
            console.warn('[useStreamingChatWithReconnect] Connection error:', error.message);

            if (state.reconnectAttempt < maxReconnectAttempts) {
              // Attempt reconnect
              void attemptReconnectRef.current();
            } else {
              // Final failure
              setState(prev => ({
                ...prev,
                error,
                isStreaming: false,
                isReconnecting: false,
                reconnectFailed: true,
                stateMessage: 'Errore di connessione',
              }));
              onError?.(error);
            }
          }
        }
      } finally {
        abortControllerRef.current = null;
      }
    },
    [apiBaseUrl, handleEvent, onError, state.reconnectAttempt, maxReconnectAttempts]
  );

  // Update executeStreamingRef to break circular dependency
  useEffect(() => {
    executeStreamingRef.current = executeStreaming;
  }, [executeStreaming]);

  // Update attemptReconnectRef to break circular dependency
  useEffect(() => {
    attemptReconnectRef.current = attemptReconnect;
  }, [attemptReconnect]);

  /**
   * Start streaming chat response
   */
  const startStreaming = useCallback(
    async (gameId: string, query: string, chatId?: string, documentIds?: string[] | null) => {
      // Store request for potential reconnection
      lastRequestRef.current = { gameId, query, chatId, documentIds };

      await executeStreaming(gameId, query, chatId, documentIds, false);
    },
    [executeStreaming]
  );

  /**
   * Stop streaming
   */
  const stopStreaming = useCallback(() => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }

    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }

    setState(prev => ({
      ...prev,
      isStreaming: false,
      isReconnecting: false,
      stateMessage: 'Annullato',
    }));
  }, []);

  /**
   * Reset state
   */
  const reset = useCallback(() => {
    stopStreaming();
    setState(initialState);
    parserRef.current.reset();
    processedEventIdsRef.current.clear();
    lastRequestRef.current = null;
  }, [stopStreaming]);

  /**
   * Manual retry after max reconnect attempts
   */
  const retryStreaming = useCallback(async () => {
    if (!lastRequestRef.current) {
      return;
    }

    // Reset reconnect counter
    setState(prev => ({
      ...prev,
      reconnectAttempt: 0,
      reconnectFailed: false,
      error: null,
    }));

    const { gameId, query, chatId, documentIds } = lastRequestRef.current;
    await executeStreaming(gameId, query, chatId, documentIds, false);
  }, [executeStreaming]);

  const controls: StreamingChatWithReconnectControls = {
    startStreaming,
    stopStreaming,
    reset,
    retryStreaming,
  };

  return [state, controls];
}
