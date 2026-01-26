/**
 * useStatefulStreaming Hook (Issue #2765)
 *
 * State machine-based streaming implementation with explicit state transitions,
 * pause/resume capability, and comprehensive lifecycle management.
 *
 * Features:
 * - Explicit state machine with guarded transitions
 * - Pause/resume streaming capability
 * - Internal buffering for paused streams
 * - Progress tracking and metrics
 * - Rich callbacks for each state transition
 * - Compatible with unified streaming interface
 *
 * State Machine:
 * idle → preparing → streaming → [paused ⇄ streaming] → completing → complete
 *   ↓        ↓           ↓            ↓                     ↓
 *   └────────┴───────────┴────────────┴─────────────────────┴──→ error
 */

import { useState, useCallback, useRef, useEffect, useMemo } from 'react';

/**
 * State machine states
 */
export type StreamingState =
  | 'idle'
  | 'preparing'
  | 'streaming'
  | 'paused'
  | 'completing'
  | 'complete'
  | 'error';

/**
 * State transition events
 */
export type StreamingEvent =
  | 'START'
  | 'READY'
  | 'RECEIVE'
  | 'PAUSE'
  | 'RESUME'
  | 'COMPLETE'
  | 'ERROR'
  | 'RESET';

/**
 * Progress metrics
 */
export interface StreamingProgress {
  /** Total bytes/characters received */
  bytesReceived: number;
  /** Number of data packets received */
  packetsReceived: number;
  /** Time elapsed since start (ms) */
  elapsedMs: number;
  /** Estimated bytes per second */
  bytesPerSecond: number;
}

/**
 * Stateful streaming state
 */
export interface StatefulStreamingState {
  /** Current state machine state */
  currentState: StreamingState;
  /** Previous state (for debugging/transitions) */
  previousState: StreamingState | null;
  /** Accumulated content */
  content: string;
  /** Buffered content (when paused) */
  buffer: string;
  /** Progress metrics */
  progress: StreamingProgress;
  /** Error message if in error state */
  error: string | null;
  /** Whether currently streaming (active receiving) */
  isActive: boolean;
  /** Whether stream can be paused */
  canPause: boolean;
  /** Whether stream can be resumed */
  canResume: boolean;
  /** Metadata from stream */
  metadata: Record<string, unknown>;
}

/**
 * Stateful streaming controls
 */
export interface StatefulStreamingControls {
  /** Start the streaming process */
  start: (streamFn: StreamFunction) => void;
  /** Pause streaming (buffers incoming data) */
  pause: () => void;
  /** Resume streaming (flushes buffer) */
  resume: () => void;
  /** Stop and reset to idle */
  stop: () => void;
  /** Reset state completely */
  reset: () => void;
  /** Manually trigger data receive (for testing) */
  receiveData: (data: string) => void;
  /** Manually trigger completion */
  complete: (metadata?: Record<string, unknown>) => void;
  /** Manually trigger error */
  triggerError: (error: string) => void;
}

/**
 * Stream function type - user provides this to handle actual data source
 */
export type StreamFunction = (handlers: StreamHandlers) => StreamCleanup;

/**
 * Handlers provided to the stream function
 */
export interface StreamHandlers {
  onData: (data: string) => void;
  onComplete: (metadata?: Record<string, unknown>) => void;
  onError: (error: string) => void;
}

/**
 * Cleanup function returned by stream function
 */
export type StreamCleanup = () => void;

/**
 * Hook options
 */
export interface StatefulStreamingOptions {
  /** Callback on state transitions */
  onStateChange?: (current: StreamingState, previous: StreamingState | null) => void;
  /** Callback when data is received */
  onData?: (data: string, accumulated: string) => void;
  /** Callback when streaming completes */
  onComplete?: (content: string, metadata: Record<string, unknown>) => void;
  /** Callback when error occurs */
  onError?: (error: string) => void;
  /** Callback when paused */
  onPause?: () => void;
  /** Callback when resumed */
  onResume?: (bufferedContent: string) => void;
  /** Initial metadata */
  initialMetadata?: Record<string, unknown>;
}

/**
 * Valid state transitions
 */
const VALID_TRANSITIONS: Record<StreamingState, StreamingEvent[]> = {
  idle: ['START'],
  preparing: ['READY', 'ERROR'],
  streaming: ['RECEIVE', 'PAUSE', 'COMPLETE', 'ERROR'],
  paused: ['RECEIVE', 'RESUME', 'ERROR', 'COMPLETE'],
  completing: ['COMPLETE', 'ERROR'],
  complete: ['RESET'],
  error: ['RESET'],
};

/**
 * State transition logic
 */
function transition(current: StreamingState, event: StreamingEvent): StreamingState | null {
  if (!VALID_TRANSITIONS[current].includes(event)) {
    return null; // Invalid transition
  }

  switch (current) {
    case 'idle':
      if (event === 'START') return 'preparing';
      break;
    case 'preparing':
      if (event === 'READY') return 'streaming';
      if (event === 'ERROR') return 'error';
      break;
    case 'streaming':
      if (event === 'RECEIVE') return 'streaming';
      if (event === 'PAUSE') return 'paused';
      if (event === 'COMPLETE') return 'complete';
      if (event === 'ERROR') return 'error';
      break;
    case 'paused':
      if (event === 'RECEIVE') return 'paused';
      if (event === 'RESUME') return 'streaming';
      if (event === 'COMPLETE') return 'complete';
      if (event === 'ERROR') return 'error';
      break;
    case 'completing':
      if (event === 'COMPLETE') return 'complete';
      if (event === 'ERROR') return 'error';
      break;
    case 'complete':
    case 'error':
      if (event === 'RESET') return 'idle';
      break;
  }
  return null;
}

const INITIAL_PROGRESS: StreamingProgress = {
  bytesReceived: 0,
  packetsReceived: 0,
  elapsedMs: 0,
  bytesPerSecond: 0,
};

const INITIAL_STATE: StatefulStreamingState = {
  currentState: 'idle',
  previousState: null,
  content: '',
  buffer: '',
  progress: { ...INITIAL_PROGRESS },
  error: null,
  isActive: false,
  canPause: false,
  canResume: false,
  metadata: {},
};

/**
 * useStatefulStreaming Hook
 *
 * State machine-based streaming with pause/resume support
 *
 * @param options - Hook configuration options
 * @returns [state, controls] tuple
 *
 * @example
 * ```tsx
 * const [state, controls] = useStatefulStreaming({
 *   onData: (data, accumulated) => console.log('Data:', data),
 *   onComplete: (content) => console.log('Done:', content),
 * });
 *
 * // Start with a stream function
 * controls.start(({ onData, onComplete, onError }) => {
 *   const eventSource = new EventSource('/api/stream');
 *   eventSource.onmessage = (e) => onData(e.data);
 *   eventSource.onerror = () => onError('Connection failed');
 *   return () => eventSource.close(); // cleanup
 * });
 *
 * // Pause/Resume
 * controls.pause();
 * controls.resume();
 *
 * // Display state
 * <div>State: {state.currentState}</div>
 * <div>{state.content}</div>
 * ```
 */
export function useStatefulStreaming(
  options: StatefulStreamingOptions = {}
): [StatefulStreamingState, StatefulStreamingControls] {
  const {
    onStateChange: _onStateChange,
    onData: _onData,
    onComplete: _onComplete,
    onError: _onError,
    onPause: _onPause,
    onResume: _onResume,
    initialMetadata = {},
  } = options;

  const [state, setState] = useState<StatefulStreamingState>({
    ...INITIAL_STATE,
    metadata: initialMetadata,
  });

  // Refs for stable callbacks and cleanup
  const cleanupRef = useRef<StreamCleanup | null>(null);
  const startTimeRef = useRef<number | null>(null);
  const optionsRef = useRef(options);
  optionsRef.current = options;

  /**
   * Cleanup stream on unmount
   */
  useEffect(() => {
    return () => {
      if (cleanupRef.current) {
        cleanupRef.current();
        cleanupRef.current = null;
      }
    };
  }, []);

  /**
   * Dispatch a state machine event
   */
  const dispatch = useCallback((event: StreamingEvent, payload?: {
    data?: string;
    error?: string;
    metadata?: Record<string, unknown>;
  }) => {
    setState(prev => {
      const nextState = transition(prev.currentState, event);

      if (!nextState) {
        // Invalid transition, log and ignore
        console.warn(`Invalid transition: ${prev.currentState} + ${event}`);
        return prev;
      }

      // Calculate new state based on event
      let newState: StatefulStreamingState = {
        ...prev,
        previousState: prev.currentState,
        currentState: nextState,
      };

      // Update derived flags
      newState.isActive = nextState === 'streaming';
      newState.canPause = nextState === 'streaming';
      newState.canResume = nextState === 'paused';

      // Handle specific events
      switch (event) {
        case 'START':
          startTimeRef.current = Date.now();
          newState.progress = { ...INITIAL_PROGRESS };
          newState.content = '';
          newState.buffer = '';
          newState.error = null;
          break;

        case 'RECEIVE':
          if (payload?.data) {
            const dataBytes = payload.data.length;
            const elapsedMs = startTimeRef.current
              ? Date.now() - startTimeRef.current
              : 0;
            const bytesReceived = prev.progress.bytesReceived + dataBytes;
            const bytesPerSecond = elapsedMs > 0
              ? (bytesReceived / elapsedMs) * 1000
              : 0;

            newState.progress = {
              bytesReceived,
              packetsReceived: prev.progress.packetsReceived + 1,
              elapsedMs,
              bytesPerSecond,
            };

            if (prev.currentState === 'paused') {
              // Buffer data when paused
              newState.buffer = prev.buffer + payload.data;
            } else {
              // Append to content when streaming
              newState.content = prev.content + payload.data;
            }
          }
          break;

        case 'PAUSE':
          // No content changes, just state
          break;

        case 'RESUME':
          // Flush buffer to content
          newState.content = prev.content + prev.buffer;
          newState.buffer = '';
          break;

        case 'COMPLETE':
          // Flush any remaining buffer
          newState.content = prev.content + prev.buffer;
          newState.buffer = '';
          if (payload?.metadata) {
            newState.metadata = { ...prev.metadata, ...payload.metadata };
          }
          newState.progress.elapsedMs = startTimeRef.current
            ? Date.now() - startTimeRef.current
            : prev.progress.elapsedMs;
          break;

        case 'ERROR':
          newState.error = payload?.error || 'Unknown error';
          break;

        case 'RESET':
          newState = {
            ...INITIAL_STATE,
            metadata: initialMetadata,
          };
          startTimeRef.current = null;
          break;
      }

      // Trigger callbacks
      if (nextState !== prev.currentState) {
        optionsRef.current.onStateChange?.(nextState, prev.currentState);
      }

      return newState;
    });
  }, [initialMetadata]);

  /**
   * Start streaming
   */
  const start = useCallback((streamFn: StreamFunction) => {
    // Dispatch START event
    dispatch('START');

    // Create handlers for the stream function
    const handlers: StreamHandlers = {
      onData: (data: string) => {
        dispatch('RECEIVE', { data });
        // Callback handled in state update
        setState(prev => {
          if (prev.currentState !== 'paused') {
            optionsRef.current.onData?.(data, prev.content + data);
          }
          return prev;
        });
      },
      onComplete: (metadata?: Record<string, unknown>) => {
        dispatch('COMPLETE', { metadata });
        // Get final content for callback
        setState(prev => {
          optionsRef.current.onComplete?.(prev.content + prev.buffer, prev.metadata);
          return prev;
        });
      },
      onError: (error: string) => {
        dispatch('ERROR', { error });
        optionsRef.current.onError?.(error);
      },
    };

    // Mark as ready and call stream function
    dispatch('READY');

    try {
      cleanupRef.current = streamFn(handlers);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Stream initialization failed';
      dispatch('ERROR', { error: errorMessage });
      optionsRef.current.onError?.(errorMessage);
    }
  }, [dispatch]);

  /**
   * Pause streaming
   */
  const pause = useCallback(() => {
    dispatch('PAUSE');
    optionsRef.current.onPause?.();
  }, [dispatch]);

  /**
   * Resume streaming
   */
  const resume = useCallback(() => {
    setState(prev => {
      dispatch('RESUME');
      optionsRef.current.onResume?.(prev.buffer);
      return prev;
    });
  }, [dispatch]);

  /**
   * Stop streaming
   */
  const stop = useCallback(() => {
    if (cleanupRef.current) {
      cleanupRef.current();
      cleanupRef.current = null;
    }
    dispatch('RESET');
  }, [dispatch]);

  /**
   * Reset state
   */
  const reset = useCallback(() => {
    if (cleanupRef.current) {
      cleanupRef.current();
      cleanupRef.current = null;
    }
    dispatch('RESET');
  }, [dispatch]);

  /**
   * Manually receive data (for testing)
   */
  const receiveData = useCallback((data: string) => {
    dispatch('RECEIVE', { data });
    setState(prev => {
      if (prev.currentState !== 'paused') {
        optionsRef.current.onData?.(data, prev.content);
      }
      return prev;
    });
  }, [dispatch]);

  /**
   * Manually complete (for testing)
   */
  const complete = useCallback((metadata?: Record<string, unknown>) => {
    dispatch('COMPLETE', { metadata });
    setState(prev => {
      optionsRef.current.onComplete?.(prev.content + prev.buffer, { ...prev.metadata, ...metadata });
      return prev;
    });
  }, [dispatch]);

  /**
   * Manually trigger error (for testing)
   */
  const triggerError = useCallback((error: string) => {
    dispatch('ERROR', { error });
    optionsRef.current.onError?.(error);
  }, [dispatch]);

  const controls = useMemo<StatefulStreamingControls>(() => ({
    start,
    pause,
    resume,
    stop,
    reset,
    receiveData,
    complete,
    triggerError,
  }), [start, pause, resume, stop, reset, receiveData, complete, triggerError]);

  return [state, controls];
}
