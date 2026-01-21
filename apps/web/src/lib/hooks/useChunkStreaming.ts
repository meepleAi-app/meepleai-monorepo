/**
 * useChunkStreaming Hook (Issue #2765)
 *
 * EventSource-based SSE streaming implementation for chunk-by-chunk content delivery.
 * Uses the browser's native EventSource API for automatic reconnection and simpler SSE handling.
 *
 * Features:
 * - Native EventSource API for SSE
 * - Automatic reconnection support (browser-managed)
 * - Chunk accumulation with proper ordering
 * - [DONE] signal handling for stream completion
 * - Clean unmount with connection cleanup
 * - Error handling with retry capability
 * - Compatible with unified streaming interface
 *
 * Note: EventSource only supports GET requests. For POST-based SSE, use useRealStreaming instead.
 */

import { useState, useCallback, useRef, useEffect } from 'react';

/**
 * Streaming state machine phases
 */
export type ChunkStreamingPhase =
  | 'idle'
  | 'connecting'
  | 'receiving'
  | 'complete'
  | 'error';

/**
 * Chunk data structure from SSE
 */
export interface StreamChunk {
  content: string;
  index?: number;
  isLast?: boolean;
}

/**
 * Chunk streaming state
 */
export interface ChunkStreamingState {
  /** Current phase of the streaming state machine */
  phase: ChunkStreamingPhase;
  /** Accumulated content from all chunks */
  content: string;
  /** Array of received chunks (for debugging/advanced use) */
  chunks: StreamChunk[];
  /** Total chunks received */
  chunkCount: number;
  /** Whether streaming is currently active */
  isStreaming: boolean;
  /** Whether connection is established */
  isConnected: boolean;
  /** Error message if streaming failed */
  error: string | null;
  /** Reconnection attempt count */
  reconnectAttempts: number;
}

/**
 * Chunk streaming controls
 */
export interface ChunkStreamingControls {
  /** Start streaming from the specified URL */
  connect: (url: string) => void;
  /** Stop streaming and close connection */
  disconnect: () => void;
  /** Reset state to initial */
  reset: () => void;
  /** Retry connection after error */
  retry: () => void;
}

/**
 * Chunk streaming configuration
 */
export interface ChunkStreamingOptions {
  /** Maximum reconnection attempts (default: 3) */
  maxReconnectAttempts?: number;
  /** Whether to auto-reconnect on connection loss (default: false) */
  autoReconnect?: boolean;
  /** Callback when a chunk is received */
  onChunk?: (chunk: StreamChunk, accumulated: string) => void;
  /** Callback when streaming completes */
  onComplete?: (content: string, chunkCount: number) => void;
  /** Callback when an error occurs */
  onError?: (error: string) => void;
  /** Callback when connection is established */
  onConnect?: () => void;
}

const INITIAL_STATE: ChunkStreamingState = {
  phase: 'idle',
  content: '',
  chunks: [],
  chunkCount: 0,
  isStreaming: false,
  isConnected: false,
  error: null,
  reconnectAttempts: 0,
};

/**
 * Parse chunk data from SSE message
 */
function parseChunkData(data: string): StreamChunk | null {
  // Handle [DONE] signal
  if (data === '[DONE]' || data.trim() === '[DONE]') {
    return { content: '', isLast: true };
  }

  // Try to parse as JSON
  try {
    const parsed = JSON.parse(data);

    // Handle various chunk formats
    if (typeof parsed === 'string') {
      return { content: parsed };
    }

    if (parsed.chunk !== undefined) {
      return {
        content: String(parsed.chunk),
        index: parsed.index,
        isLast: parsed.isLast ?? parsed.done ?? false,
      };
    }

    if (parsed.content !== undefined) {
      return {
        content: String(parsed.content),
        index: parsed.index,
        isLast: parsed.isLast ?? parsed.done ?? false,
      };
    }

    if (parsed.token !== undefined) {
      return {
        content: String(parsed.token),
        index: parsed.index,
        isLast: false,
      };
    }

    // If parsed but no recognized fields, stringify back
    return { content: JSON.stringify(parsed) };
  } catch {
    // Not JSON, treat as plain text
    return { content: data };
  }
}

/**
 * useChunkStreaming Hook
 *
 * EventSource-based SSE streaming for chunk delivery
 *
 * @param options - Hook configuration options
 * @returns [state, controls] tuple
 *
 * @example
 * ```tsx
 * const [state, controls] = useChunkStreaming({
 *   onChunk: (chunk, accumulated) => console.log('Chunk:', chunk.content),
 *   onComplete: (content) => console.log('Done:', content),
 * });
 *
 * // Connect to SSE endpoint
 * controls.connect('/api/v1/stream?query=hello');
 *
 * // Display accumulated content
 * <div>{state.content}</div>
 * ```
 */
export function useChunkStreaming(
  options: ChunkStreamingOptions = {}
): [ChunkStreamingState, ChunkStreamingControls] {
  const {
    maxReconnectAttempts = 3,
    autoReconnect = false,
    onChunk,
    onComplete,
    onError,
    onConnect,
  } = options;

  const [state, setState] = useState<ChunkStreamingState>(INITIAL_STATE);

  // Refs for stable callbacks and EventSource management
  const eventSourceRef = useRef<EventSource | null>(null);
  const lastUrlRef = useRef<string | null>(null);
  const reconnectTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const optionsRef = useRef(options);
  optionsRef.current = options;
  const connectRef = useRef<((url: string) => void) | null>(null);

  /**
   * Close the EventSource connection
   */
  const closeConnection = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  /**
   * Clear any pending reconnection timeout
   */
  const clearReconnectTimeout = useCallback(() => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }
  }, []);

  /**
   * Cleanup on unmount
   */
  useEffect(() => {
    return () => {
      clearReconnectTimeout();
      closeConnection();
    };
  }, [closeConnection, clearReconnectTimeout]);

  /**
   * Disconnect and reset streaming state
   */
  const disconnect = useCallback(() => {
    clearReconnectTimeout();
    closeConnection();
    setState(prev => ({
      ...prev,
      phase: 'idle',
      isStreaming: false,
      isConnected: false,
    }));
  }, [closeConnection, clearReconnectTimeout]);

  /**
   * Reset state to initial
   */
  const reset = useCallback(() => {
    clearReconnectTimeout();
    closeConnection();
    setState(INITIAL_STATE);
    lastUrlRef.current = null;
  }, [closeConnection, clearReconnectTimeout]);

  /**
   * Connect to SSE endpoint
   */
  const connect = useCallback((url: string): void => {
    // Close any existing connection
    closeConnection();

    // Store URL for retry
    lastUrlRef.current = url;

    // Set connecting state
    setState(prev => ({
      ...INITIAL_STATE,
      phase: 'connecting',
      isStreaming: true,
      reconnectAttempts: prev.reconnectAttempts,
    }));

    // Create EventSource
    const eventSource = new EventSource(url);
    eventSourceRef.current = eventSource;

    // Handle connection open
    eventSource.onopen = () => {
      setState(prev => ({
        ...prev,
        phase: 'receiving',
        isConnected: true,
        reconnectAttempts: 0,
      }));
      optionsRef.current.onConnect?.();
    };

    // Handle messages
    eventSource.onmessage = (event) => {
      const chunk = parseChunkData(event.data);

      if (!chunk) return;

      // Handle completion signal
      if (chunk.isLast && chunk.content === '') {
        closeConnection();
        setState(prev => {
          optionsRef.current.onComplete?.(prev.content, prev.chunkCount);
          return {
            ...prev,
            phase: 'complete',
            isStreaming: false,
            isConnected: false,
          };
        });
        return;
      }

      // Accumulate chunk
      setState(prev => {
        const newContent = prev.content + chunk.content;
        const newChunks = [...prev.chunks, chunk];

        optionsRef.current.onChunk?.(chunk, newContent);

        // Check if this is the last chunk
        if (chunk.isLast) {
          closeConnection();
          optionsRef.current.onComplete?.(newContent, newChunks.length);
          return {
            ...prev,
            content: newContent,
            chunks: newChunks,
            chunkCount: newChunks.length,
            phase: 'complete',
            isStreaming: false,
            isConnected: false,
          };
        }

        return {
          ...prev,
          content: newContent,
          chunks: newChunks,
          chunkCount: newChunks.length,
        };
      });
    };

    // Handle errors
    eventSource.onerror = () => {
      closeConnection();

      // Use setState callback to access current state and avoid stale closures
      setState(prev => {
        const currentAttempts = prev.reconnectAttempts;

        // Check if we should auto-reconnect
        if (autoReconnect && currentAttempts < maxReconnectAttempts && lastUrlRef.current) {
          // Exponential backoff: 1s, 2s, 4s...
          const delay = Math.pow(2, currentAttempts) * 1000;
          reconnectTimeoutRef.current = setTimeout(() => {
            if (lastUrlRef.current && connectRef.current) {
              connectRef.current(lastUrlRef.current);
            }
          }, delay);

          return {
            ...prev,
            phase: 'connecting',
            isConnected: false,
            reconnectAttempts: prev.reconnectAttempts + 1,
          };
        } else {
          const errorMessage = 'Connection failed';
          optionsRef.current.onError?.(errorMessage);
          return {
            ...prev,
            phase: 'error',
            isStreaming: false,
            isConnected: false,
            error: errorMessage,
          };
        }
      });
    };
  }, [closeConnection, autoReconnect, maxReconnectAttempts]);

  // Keep ref updated for recursive calls
  connectRef.current = connect;

  /**
   * Retry connection after error
   */
  const retry = useCallback((): void => {
    if (lastUrlRef.current && connectRef.current) {
      setState(prev => ({
        ...INITIAL_STATE,
        reconnectAttempts: prev.reconnectAttempts,
      }));
      connectRef.current(lastUrlRef.current);
    }
  }, []);

  return [
    state,
    {
      connect,
      disconnect,
      reset,
      retry,
    },
  ];
}
