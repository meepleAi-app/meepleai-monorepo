/**
 * useAgentChatStream - SSE streaming for agent chat
 * Issue #4364: SSE streaming in ChatThreadView
 *
 * Refactored as a thin wrapper over useSseStreamFsm (#1704 Phase B).
 * Handles SSE connection to POST /api/v1/agents/{agentId}/chat
 * Parses RagStreamingEvent format (numeric StreamingEventType enum)
 */

import { useEffect, useMemo, useRef } from 'react';

import { toast } from 'sonner';

import { useSseStreamFsm, type SseStreamFsmConfig } from './useSseStreamFsm';

// StreamingEventType enum values from backend
const StreamingEventType = {
  StateUpdate: 0,
  Citations: 1,
  Outline: 2,
  ScriptChunk: 3,
  Complete: 4,
  Error: 5,
  Heartbeat: 6,
  Token: 7,
  FollowUpQuestions: 8,
  SetupStep: 9,
  // Debug pipeline events (Issue #4916)
  DebugAgentRouter: 10,
  DebugStrategySelected: 11,
  DebugRetrievalStart: 12,
  DebugRetrievalResults: 13,
  DebugPluginExecution: 14,
  DebugValidationLayer: 15,
  DebugPromptContext: 16,
  DebugCostUpdate: 17,
  DebugSearchDetails: 18,
  DebugCacheCheck: 19,
  DebugDocumentCheck: 20,
  ModelDowngrade: 21,
  DebugTypologyProfile: 22,
} as const;

/** Synthetic event type for the initial "connecting" state transition */
const SYNTHETIC_CONNECTING = -1;

/** Synthetic event type for the "connection lost, retrying" state transition */
const SYNTHETIC_DISCONNECTED = -2;

// Debug step captured from SSE stream (Issue #4916)
export interface DebugStep {
  /** Event type number */
  type: number;
  /** Human-readable step name */
  name: string;
  /** Raw payload from backend */
  payload: unknown;
  /** Timestamp from event */
  timestamp: string;
  /** Latency in ms if available in payload */
  latencyMs?: number;
}

/** Maps debug event types to display names */
const DEBUG_STEP_NAMES: Record<number, string> = {
  10: 'Agent Router',
  11: 'Strategy Selected',
  12: 'Retrieval Start',
  13: 'Retrieval Results',
  14: 'Plugin Execution',
  15: 'Validation Layer',
  16: 'Prompt Context',
  17: 'Cost Update',
  18: 'Search Details',
  19: 'Cache Check',
  20: 'Document Check',
  22: 'Typology Profile',
};

export type ConnectionStatus =
  | 'idle'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'
  | 'error';

export interface AgentChatStreamState {
  /** Current streaming status text */
  statusMessage: string | null;
  /** Accumulated assistant response */
  currentAnswer: string;
  /** Follow-up questions from the agent */
  followUpQuestions: string[];
  /** Whether currently streaming */
  isStreaming: boolean;
  /** Error message if any */
  error: string | null;
  /** Chat thread ID returned by backend */
  chatThreadId: string | null;
  /** Token count from complete event */
  totalTokens: number;
  /** Debug pipeline steps captured during streaming (Issue #4916) */
  debugSteps: DebugStep[];
  /** Model downgrade notice (fallback occurred) */
  modelDowngrade: {
    originalModel: string;
    fallbackModel: string;
    reason: string;
    isLocalFallback: boolean;
    upgradeMessage: string | null;
  } | null;
  /** Strategy tier from Complete event (Issue #5481: ResponseMetaBadge) */
  strategyTier: string | null;
  /** Execution ID for deep link to Debug Console (Issue #5486) */
  executionId: string | null;
  /** Connection status for UI indicator */
  connectionStatus: ConnectionStatus;
  /** Number of retry attempts */
  retryCount: number;
}

/** Game context for OpenRouter proxy requests */
export interface ProxyGameContext {
  gameName: string;
  agentTypology: string;
  ragContext?: string;
}

export interface AgentChatStreamCallbacks {
  onComplete?: (
    answer: string,
    metadata: {
      totalTokens: number;
      chatThreadId: string | null;
      followUpQuestions: string[];
    }
  ) => void;
  onError?: (error: string) => void;
}

/** Input for a single chat message stream */
export interface AgentChatInput {
  agentId: string;
  message: string;
  chatThreadId?: string;
  proxyGameContext?: ProxyGameContext;
  gameSessionId?: string;
}

/** Internal state shape (base hook owns retryCount + error separately) */
type AgentChatInternalState = Omit<AgentChatStreamState, 'retryCount' | 'error'>;

/** Parsed SSE event shape */
type AgentChatEvent = { type: number; data: unknown; timestamp?: string };

const INITIAL_STATE: AgentChatInternalState = {
  statusMessage: null,
  currentAnswer: '',
  followUpQuestions: [],
  isStreaming: false,
  chatThreadId: null,
  totalTokens: 0,
  debugSteps: [],
  modelDowngrade: null,
  strategyTier: null,
  executionId: null,
  connectionStatus: 'idle',
};

// ─── Transport ────────────────────────────────────────────────────────────────

/**
 * Transport: verbatim port of URL builder + fetch + SSE parse loop from original
 * useAgentChatStream.ts lines 239–360. Generator yields parsed events.
 * Yields a synthetic SYNTHETIC_CONNECTING event first to set isStreaming+connecting state.
 *
 * B-4 (CRITICAL): URL builder is verbatim from original lines 239–264.
 */
async function* agentChatTransport(
  input: AgentChatInput,
  signal: AbortSignal
): AsyncGenerator<AgentChatEvent> {
  // Yield synthetic connecting event so reducer can set isStreaming:true, connectionStatus:'connecting'
  // BEFORE the fetch blocks. This preserves the original "Connecting..." state that was set
  // synchronously in sendMessage before any fetch started.
  yield { type: SYNTHETIC_CONNECTING, data: null };

  if (signal.aborted) return;

  // B-4 — verbatim from original lines 239–264:
  const useProxy =
    !!input.proxyGameContext && process.env.NEXT_PUBLIC_USE_OPENROUTER_PROXY === 'true';

  let url: string;
  let body: Record<string, unknown>;

  if (useProxy) {
    url = '/api/chat-proxy';
    body = {
      message: input.message,
      agentId: input.agentId,
      threadId: input.chatThreadId,
      gameContext: input.proxyGameContext,
    };
  } else {
    const baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
    url = `${baseUrl}/api/v1/agents/${input.agentId}/chat`;
    body = { message: input.message };
    if (input.chatThreadId) {
      body.chatThreadId = input.chatThreadId;
    }
    if (input.gameSessionId) {
      body.gameSessionId = input.gameSessionId;
    }
  }

  let response: Response;
  try {
    response = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      credentials: useProxy ? 'same-origin' : 'include',
      signal,
    });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
  } catch (fetchErr) {
    if (signal.aborted) return;
    // Yield a synthetic "disconnected" event so the reducer can set connectionStatus:'disconnected'
    // BEFORE the error propagates to the base hook's retry scheduler.
    // This preserves the original state transition (original lines 496-503):
    //   setState({ connectionStatus: 'disconnected', statusMessage: 'Connessione persa, riprovo...' })
    yield { type: SYNTHETIC_DISCONNECTED, data: null };
    throw fetchErr; // re-throw so base hook errorMapper → retry scheduling
  }

  const reader = response.body?.getReader();
  if (!reader) {
    throw new Error('No response body');
  }

  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      if (signal.aborted) return;

      buffer += decoder.decode(value, { stream: true });

      // Process complete SSE messages (separated by \n\n)
      const parts = buffer.split('\n\n');
      buffer = parts.pop() ?? '';

      for (const part of parts) {
        if (!part.trim()) continue;

        // Parse "data: {json}" format
        const dataMatch = part.match(/data:\s*([\s\S]+)/);
        if (!dataMatch) continue;

        // Backend uses camelCase serialization (SseJsonOptions.cs)
        try {
          const event = JSON.parse(dataMatch[1]) as AgentChatEvent;
          yield event;
        } catch {
          // Skip malformed line — preserves the original lenient behavior (lines 308-310).
        }
      }
    }
  } finally {
    try {
      reader.releaseLock();
    } catch {
      /* lock may already be released on abort */
    }
  }
}

// ─── Reducer ──────────────────────────────────────────────────────────────────

/**
 * Event reducer: VERBATIM port of the switch from useAgentChatStream.ts lines 312–464.
 * All 23 StreamingEventType cases (0..22) + synthetic SYNTHETIC_CONNECTING (-1).
 *
 * Note: toast.info calls for ModelDowngrade are side effects inside a reducer — this matches
 * the original behavior where toast was called inside the setState updater (lines 425–429).
 * Moving them here preserves the original test assertions on toast.info.
 */
function agentChatEventReducer(
  state: AgentChatInternalState,
  event: AgentChatEvent
): AgentChatInternalState {
  switch (event.type) {
    case SYNTHETIC_CONNECTING: {
      // Synthetic event: set connecting state before fetch begins
      return {
        ...state,
        isStreaming: true,
        statusMessage: 'Connecting...',
        connectionStatus: 'connecting',
      };
    }

    case SYNTHETIC_DISCONNECTED: {
      // Synthetic event: connection failed, retry pending — matches original lines 496-503
      return {
        ...state,
        connectionStatus: 'disconnected',
        statusMessage: 'Connessione persa, riprovo...',
        // isStreaming stays true (retry is pending)
      };
    }

    case StreamingEventType.StateUpdate: {
      const data = event.data as { message?: string; chatThreadId?: string };
      return {
        ...state,
        statusMessage: data?.message ?? null,
        chatThreadId: data?.chatThreadId ?? state.chatThreadId,
        connectionStatus: 'connected',
      };
    }

    case StreamingEventType.Citations:
    case StreamingEventType.Outline:
    case StreamingEventType.ScriptChunk:
    case StreamingEventType.SetupStep: {
      // Parsed but no additional state change beyond connection status
      return state;
    }

    case StreamingEventType.Complete: {
      const data = event.data as {
        totalTokens?: number;
        chatThreadId?: string;
        completionTokens?: number;
        promptTokens?: number;
        strategyTier?: string;
        executionId?: string;
      };
      return {
        ...state,
        totalTokens: data?.totalTokens ?? 0,
        chatThreadId: data?.chatThreadId ?? state.chatThreadId,
        strategyTier: data?.strategyTier ?? state.strategyTier,
        executionId: data?.executionId ?? state.executionId,
        isStreaming: false,
        statusMessage: null,
        connectionStatus: 'idle',
        retryCount: 0,
      } as AgentChatInternalState;
    }

    case StreamingEventType.Error: {
      const data = event.data as { errorMessage?: string; errorCode?: string };
      let errorMsg: string;
      if (data?.errorCode === 'rate_limited') {
        errorMsg = 'Hai raggiunto il limite di messaggi. Riprova tra qualche minuto.';
      } else if (data?.errorCode === 'provider_unavailable') {
        errorMsg = 'Il servizio AI è temporaneamente non disponibile. Riprova tra poco.';
      } else {
        errorMsg = data?.errorMessage ?? 'Si è verificato un errore. Riprova.';
      }
      // Store error in internal state so the wrapper can fire onError callback
      // and merge into state.error
      return {
        ...state,
        // We store the error message in a special field; the wrapper will pick it up
        _sseError: errorMsg,
        isStreaming: false,
        statusMessage: null,
        connectionStatus: 'error',
      } as AgentChatInternalState;
    }

    case StreamingEventType.Heartbeat: {
      return state;
    }

    case StreamingEventType.Token: {
      const data = event.data as { token?: string };
      if (data?.token) {
        return {
          ...state,
          currentAnswer: state.currentAnswer + data.token,
          statusMessage: null,
          connectionStatus: 'connected',
        };
      }
      return state;
    }

    case StreamingEventType.FollowUpQuestions: {
      const data = event.data as { questions?: string[] };
      return {
        ...state,
        followUpQuestions: data?.questions ?? [],
      };
    }

    case StreamingEventType.ModelDowngrade: {
      const data = event.data as {
        originalModel?: string;
        fallbackModel?: string;
        reason?: string;
        isLocalFallback?: boolean;
        upgradeMessage?: string | null;
      };
      const toastMessage =
        data?.reason === 'rate_limited'
          ? 'Modello temporaneamente cambiato per limiti di utilizzo'
          : 'Modello alternativo in uso per garantire la risposta';
      toast.info(toastMessage, { duration: 5000 });
      return {
        ...state,
        modelDowngrade: {
          originalModel: data?.originalModel ?? 'unknown',
          fallbackModel: data?.fallbackModel ?? 'unknown',
          reason: data?.reason ?? 'unknown',
          isLocalFallback: data?.isLocalFallback ?? false,
          upgradeMessage: data?.upgradeMessage ?? null,
        },
      };
    }

    // Debug pipeline events (Issue #4916) — types 10–20, 22
    case StreamingEventType.DebugAgentRouter:
    case StreamingEventType.DebugStrategySelected:
    case StreamingEventType.DebugRetrievalStart:
    case StreamingEventType.DebugRetrievalResults:
    case StreamingEventType.DebugPluginExecution:
    case StreamingEventType.DebugValidationLayer:
    case StreamingEventType.DebugPromptContext:
    case StreamingEventType.DebugCostUpdate:
    case StreamingEventType.DebugSearchDetails:
    case StreamingEventType.DebugCacheCheck:
    case StreamingEventType.DebugDocumentCheck:
    case StreamingEventType.DebugTypologyProfile: {
      const payload = event.data as Record<string, unknown>;
      const step: DebugStep = {
        type: event.type,
        name: DEBUG_STEP_NAMES[event.type] ?? `Step ${event.type}`,
        payload,
        timestamp: event.timestamp ?? '',
        latencyMs: typeof payload?.latencyMs === 'number' ? payload.latencyMs : undefined,
      };
      return {
        ...state,
        debugSteps: [...state.debugSteps, step],
      };
    }

    default:
      return state;
  }
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

type AgentChatErrorShape = { kind: 'connection' | 'other'; message: string };

export function useAgentChatStream(callbacks?: AgentChatStreamCallbacks): {
  state: AgentChatStreamState;
  sendMessage: (
    agentId: string,
    message: string,
    chatThreadId?: string,
    proxyGameContext?: ProxyGameContext,
    gameSessionId?: string
  ) => void;
  stopStreaming: () => void;
  reset: () => void;
} {
  // M-4: track current partial answer for retry-guard decision in errorMapper.
  // Only retry (kind: 'connection') when currentAnswer === '' — avoids duplicating partial tokens.
  const currentAnswerRef = useRef<string>('');
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  // Track previous complete/error state for callback firing via useEffect
  const prevIsStreamingRef = useRef<boolean>(false);
  const prevErrorRef = useRef<string | null>(null);

  const config = useMemo<
    SseStreamFsmConfig<AgentChatInput, AgentChatEvent, AgentChatInternalState, AgentChatErrorShape>
  >(
    () => ({
      transport: agentChatTransport,
      initialState: INITIAL_STATE,
      eventReducer: (s, e) => {
        const next = agentChatEventReducer(s, e);
        // Update currentAnswerRef so errorMapper can check M-4 guard
        currentAnswerRef.current = next.currentAnswer;
        return next;
      },
      errorMapper: raw => {
        // M-4: only mark as 'connection' (retryable) if no partial answer received yet.
        // This preserves the original `!hasAnswer` guard (lines 491–521 of original).
        const isConnectionError =
          raw instanceof TypeError || (raw instanceof Error && /HTTP \d/.test(raw.message));
        const canRetry = isConnectionError && currentAnswerRef.current === '';

        let message = 'Si è verificato un errore. Riprova.';
        if (raw instanceof Error) {
          message = raw.message;
        }

        return { kind: canRetry ? 'connection' : 'other', message };
      },
      retryPolicy: {
        maxRetries: 2,
        backoffMs: [2000, 2000],
        retryableErrorKinds: ['connection'],
      },
    }),
    []
  );

  const { state, retryCount, error, ask, stop, reset: baseReset } = useSseStreamFsm(config);

  // ── Merged state ──────────────────────────────────────────────────────────

  // Check if an SSE Error event was received (stored in internal state via _sseError)
  const sseError = (state as AgentChatInternalState & { _sseError?: string })._sseError ?? null;

  // The public `error` field is: SSE Error event message (from reducer) OR thrown error message
  const publicError: string | null = sseError ?? error?.message ?? null;

  // Build merged state: spread internal state, add base-hook retryCount and merged error.
  // _sseError is an internal implementation field — exclude it from the public shape.
  const stateWithExtras = state as AgentChatInternalState & { _sseError?: string };
  const merged: AgentChatStreamState = {
    statusMessage: stateWithExtras.statusMessage,
    currentAnswer: stateWithExtras.currentAnswer,
    followUpQuestions: stateWithExtras.followUpQuestions,
    isStreaming: stateWithExtras.isStreaming,
    chatThreadId: stateWithExtras.chatThreadId,
    totalTokens: stateWithExtras.totalTokens,
    debugSteps: stateWithExtras.debugSteps,
    modelDowngrade: stateWithExtras.modelDowngrade,
    strategyTier: stateWithExtras.strategyTier,
    executionId: stateWithExtras.executionId,
    connectionStatus: stateWithExtras.connectionStatus,
    retryCount,
    error: publicError,
  };

  // ── onComplete callback ───────────────────────────────────────────────────
  // Fire when stream transitions from streaming → not streaming with an answer,
  // matching original lines 371-376 (fired inside Complete event setState updater).
  useEffect(() => {
    const wasStreaming = prevIsStreamingRef.current;
    prevIsStreamingRef.current = merged.isStreaming;

    if (wasStreaming && !merged.isStreaming && merged.currentAnswer && !merged.error) {
      callbacksRef.current?.onComplete?.(merged.currentAnswer, {
        totalTokens: merged.totalTokens,
        chatThreadId: merged.chatThreadId,
        followUpQuestions: merged.followUpQuestions,
      });
    }
  });

  // ── onError callback ──────────────────────────────────────────────────────
  // Fire when error becomes non-null (SSE Error event OR connection error after max retries).
  // Matches original lines 400 (SSE error) and 531 (connection error).
  useEffect(() => {
    const prevError = prevErrorRef.current;
    prevErrorRef.current = merged.error;

    if (merged.error !== null && prevError === null) {
      callbacksRef.current?.onError?.(merged.error);
    }
  });

  // ── Dev tools scenario switch (non-functional for tests) ──────────────────
  useEffect(() => {
    const onScenarioSwitch = (): void => {
      stop();
    };
    window.addEventListener('meepledev:scenario-switch-begin', onScenarioSwitch);
    return () => {
      window.removeEventListener('meepledev:scenario-switch-begin', onScenarioSwitch);
    };
  }, [stop]);

  // ── Public API ────────────────────────────────────────────────────────────

  const sendMessage = (
    agentId: string,
    message: string,
    chatThreadId?: string,
    proxyGameContext?: ProxyGameContext,
    gameSessionId?: string
  ): void => {
    currentAnswerRef.current = ''; // reset M-4 guard for new stream
    ask({ agentId, message, chatThreadId, proxyGameContext, gameSessionId });
  };

  const stopStreaming = (): void => {
    stop();
    // reset() instead of stop() so state resets to INITIAL (isStreaming: false, connectionStatus: 'idle')
    // matching original stopStreaming behavior (lines 168-182)
    baseReset();
  };

  const reset = (): void => {
    currentAnswerRef.current = '';
    baseReset();
  };

  return { state: merged, sendMessage, stopStreaming, reset };
}
