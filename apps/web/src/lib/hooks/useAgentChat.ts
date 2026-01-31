/**
 * useAgentChat Hook (Issue #3187)
 *
 * Session-based SSE streaming for agent chat.
 * Wraps useStreamingChat with session-specific context and endpoint.
 *
 * Features:
 * - Session-scoped streaming to /agent/chat endpoint
 * - Progressive token accumulation
 * - Game state injection (handled by backend)
 * - Reconnection logic on disconnect
 * - Message history management
 *
 * Backend endpoint (Issue #3184):
 * - POST /api/v1/game-sessions/{sessionId}/agent/chat
 *
 * @example
 * ```typescript
 * const { messages, sendMessage, isStreaming, error } = useAgentChat({
 *   sessionId: 'abc123',
 *   onComplete: (answer) => console.log('Done:', answer),
 * });
 *
 * // Send message
 * await sendMessage('What should I do next?');
 * ```
 */

import { useState, useCallback, useRef } from 'react';

import {
  StreamingEventType,
  parseEventData,
  type RagStreamingEvent,
  type Citation,
  type TypedStreamingEvent,
} from '@/lib/api/schemas/streaming.schemas';
import { SSEParser } from '@/lib/utils/sseParser';
import { useAgentChatStore } from '@/store/agent/store';
import { AgentMessage } from '@/types/agent';

/**
 * Hook options
 */
export interface UseAgentChatOptions {
  /** Game session ID */
  sessionId: string;

  /** Callback when streaming completes */
  onComplete?: (answer: string, citations: Citation[]) => void;

  /** Callback when an error occurs */
  onError?: (error: Error) => void;

  /** Callback when a token is received */
  onToken?: (token: string) => void;

  /** Callback when state updates */
  onStateUpdate?: (state: string) => void;

  /** API base URL */
  apiBaseUrl?: string;
}

/**
 * Hook state
 */
export interface AgentChatState {
  /** Whether streaming is active */
  isStreaming: boolean;

  /** Error if streaming failed */
  error: Error | null;

  /** Current state message */
  stateMessage: string;

  /** Messages for this session */
  messages: AgentMessage[];
}

/**
 * Hook controls
 */
export interface AgentChatControls {
  /** Send a message and start streaming response */
  sendMessage: (query: string) => Promise<void>;

  /** Stop streaming */
  stopStreaming: () => void;

  /** Clear messages */
  clearMessages: () => void;
}

/**
 * useAgentChat Hook
 *
 * Manages session-based agent chat with SSE streaming
 */
export function useAgentChat(
  options: UseAgentChatOptions
): AgentChatState & AgentChatControls {
  const { sessionId, onComplete, onError, onToken, onStateUpdate, apiBaseUrl } = options;

  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [stateMessage, setStateMessage] = useState('');

  // Zustand store
  const { messages, addMessage, updateLastMessage, clearMessages: storeClearMessages } =
    useAgentChatStore(state => ({
      messages: state.messagesBySession[sessionId] ?? [],
      addMessage: state.addMessage,
      updateLastMessage: state.updateLastMessage,
      clearMessages: state.clearMessages,
    }));

  // AbortController for cancellation
  const abortControllerRef = useRef<AbortController | null>(null);

  // SSE Parser instance
  const parserRef = useRef<SSEParser>(new SSEParser());

  // Reconnection state
  const [reconnectAttempts, setReconnectAttempts] = useState(0);
  const maxReconnectAttempts = 3;

  /**
   * Handle streaming event
   */
  const handleEvent = useCallback(
    (event: RagStreamingEvent) => {
      try {
        switch (event.type) {
          case StreamingEventType.StateUpdate: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.StateUpdate>;
            const stateMsg = typedEvent.data.state;
            setStateMessage(stateMsg);
            onStateUpdate?.(stateMsg);
            break;
          }

          case StreamingEventType.Token: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Token>;
            const token = typedEvent.data.token;

            // Append token to last agent message
            updateLastMessage(sessionId, token);
            onToken?.(token);
            break;
          }

          case StreamingEventType.Citations: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Citations>;
            const citations =
              typedEvent.data.citations.length > 0
                ? typedEvent.data.citations
                : typedEvent.data.snippets;

            // Add citations to last message
            updateLastMessage(sessionId, '', citations);
            break;
          }

          case StreamingEventType.Complete: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Complete>;
            const { snippets } = typedEvent.data;

            setIsStreaming(false);
            setStateMessage('Complete');
            setReconnectAttempts(0);

            // Get last message for callback
            const lastMessage = messages[messages.length - 1];
            if (lastMessage) {
              onComplete?.(lastMessage.content, snippets || []);
            }
            break;
          }

          case StreamingEventType.Error: {
            const typedEvent = parseEventData(
              event
            ) as TypedStreamingEvent<StreamingEventType.Error>;
            const errorObj = new Error(typedEvent.data.message);
            errorObj.name = typedEvent.data.code;

            setError(errorObj);
            setIsStreaming(false);
            setStateMessage('Error');
            onError?.(errorObj);
            break;
          }
        }
      } catch (err) {
        console.error('[useAgentChat] Event handling error:', err, event);
      }
    },
    [sessionId, messages, updateLastMessage, onComplete, onError, onToken, onStateUpdate]
  );

  /**
   * Send message and start streaming
   */
  const sendMessage = useCallback(
    async (query: string) => {
      // Add user message
      addMessage(sessionId, {
        type: 'user',
        content: query,
        timestamp: new Date(),
      });

      // Add empty agent message (will be filled by tokens)
      addMessage(sessionId, {
        type: 'agent',
        content: '',
        timestamp: new Date(),
        citations: [],
      });

      setIsStreaming(true);
      setError(null);
      setStateMessage('Connecting...');

      // Create AbortController
      const controller = new AbortController();
      abortControllerRef.current = controller;

      // Reset parser
      parserRef.current.reset();

      const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';
      const endpoint = `${baseUrl}/api/v1/game-sessions/${sessionId}/agent/chat`;

      try {
        const response = await fetch(endpoint, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          credentials: 'include',
          body: JSON.stringify({ query }),
          signal: controller.signal,
        });

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        if (!response.body) {
          throw new Error('No response body');
        }

        // Read stream
        const reader = response.body.getReader();
        const decoder = new TextDecoder();

        while (true) {
          const { done, value } = await reader.read();

          if (done) break;

          const chunk = decoder.decode(value, { stream: true });
          const events = parserRef.current.parse(chunk);
          events.forEach(handleEvent);
        }
      } catch (err) {
        if (err instanceof Error) {
          if (err.name === 'AbortError') {
            setIsStreaming(false);
            setStateMessage('Cancelled');
          } else {
            setError(err);
            setIsStreaming(false);
            setStateMessage('Error');
            onError?.(err);

            // Attempt reconnection
            if (reconnectAttempts < maxReconnectAttempts) {
              const delay = Math.pow(2, reconnectAttempts) * 1000; // Exponential backoff
              setReconnectAttempts(prev => prev + 1);
              setStateMessage(`Reconnecting in ${delay / 1000}s...`);

              setTimeout(() => {
                void sendMessage(query);
              }, delay);
            }
          }
        }
      } finally {
        abortControllerRef.current = null;
      }
    },
    [
      sessionId,
      apiBaseUrl,
      addMessage,
      handleEvent,
      onError,
      reconnectAttempts,
      maxReconnectAttempts,
    ]
  );

  /**
   * Stop streaming
   */
  const stopStreaming = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
    setIsStreaming(false);
    setStateMessage('Cancelled');
    setReconnectAttempts(0);
  }, []);

  /**
   * Clear messages
   */
  const clearMessages = useCallback(() => {
    storeClearMessages(sessionId);
    setError(null);
    setStateMessage('');
  }, [sessionId, storeClearMessages]);

  return {
    isStreaming,
    error,
    stateMessage,
    messages,
    sendMessage,
    stopStreaming,
    clearMessages,
  };
}
