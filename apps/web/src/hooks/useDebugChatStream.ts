/**
 * useDebugChatStream - SSE streaming for admin debug chat
 *
 * Extends the useAgentChatStream pattern with debug event collection.
 * Normal events (types 0-9) update chat state, debug events (types >= 10)
 * are pushed to a separate debugEvents array for timeline rendering.
 */

import { useState, useCallback, useRef } from 'react';

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
  // Debug events (>= 10)
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
  // RAG Enhancement debug events
  DebugAdaptiveRouting: 23,
  DebugCragEvaluation: 24,
  DebugRagFusion: 25,
  DebugContextWindow: 26,
} as const;

/** Human-readable labels for debug event types */
const DEBUG_EVENT_LABELS: Record<number, string> = {
  [StreamingEventType.DebugAgentRouter]: 'Agent Router',
  [StreamingEventType.DebugStrategySelected]: 'Strategy Selected',
  [StreamingEventType.DebugRetrievalStart]: 'Retrieval Start',
  [StreamingEventType.DebugRetrievalResults]: 'Retrieval Results',
  [StreamingEventType.DebugPluginExecution]: 'Plugin Execution',
  [StreamingEventType.DebugValidationLayer]: 'Validation Layer',
  [StreamingEventType.DebugPromptContext]: 'Prompt Context',
  [StreamingEventType.DebugCostUpdate]: 'Cost Update',
  [StreamingEventType.DebugSearchDetails]: 'Search Details',
  [StreamingEventType.DebugCacheCheck]: 'Cache Check',
  [StreamingEventType.DebugDocumentCheck]: 'Document Check',
  [StreamingEventType.DebugAdaptiveRouting]: 'Adaptive Routing',
  [StreamingEventType.DebugCragEvaluation]: 'CRAG Evaluation',
  [StreamingEventType.DebugRagFusion]: 'RAG-Fusion',
  [StreamingEventType.DebugContextWindow]: 'Context Window',
};

export interface DebugEvent {
  /** Auto-increment ID for React keys */
  id: number;
  /** StreamingEventType numeric value */
  type: number;
  /** Human-readable label */
  typeName: string;
  /** Event-specific data payload */
  data: unknown;
  /** ISO timestamp from backend */
  timestamp: string;
  /** Milliseconds since stream started */
  elapsedMs: number;
}

export interface DebugChatStreamState {
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
  /** Collected debug events for timeline */
  debugEvents: DebugEvent[];
}

export interface DebugChatStreamCallbacks {
  onComplete?: (
    answer: string,
    metadata: {
      totalTokens: number;
      chatThreadId: string | null;
      debugEvents: DebugEvent[];
    }
  ) => void;
  onError?: (error: string) => void;
  onDebugEvent?: (event: DebugEvent) => void;
}

const INITIAL_STATE: DebugChatStreamState = {
  statusMessage: null,
  currentAnswer: '',
  followUpQuestions: [],
  isStreaming: false,
  error: null,
  chatThreadId: null,
  totalTokens: 0,
  debugEvents: [],
};

export interface DebugChatConfigOverride {
  denseWeight?: number;
  topK?: number;
  rerankingEnabled?: boolean;
  temperature?: number;
  maxTokens?: number;
  model?: string;
}

export function useDebugChatStream(callbacks?: DebugChatStreamCallbacks) {
  const [state, setState] = useState<DebugChatStreamState>(INITIAL_STATE);
  const abortControllerRef = useRef<AbortController | null>(null);
  const callbacksRef = useRef(callbacks);
  const activeRequestIdRef = useRef<number>(0);
  const debugEventCounterRef = useRef<number>(0);
  callbacksRef.current = callbacks;

  const stopStreaming = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
    setState(prev => ({ ...prev, isStreaming: false, statusMessage: null }));
  }, []);

  const reset = useCallback(() => {
    stopStreaming();
    debugEventCounterRef.current = 0;
    setState(INITIAL_STATE);
  }, [stopStreaming]);

  const sendMessage = useCallback(
    (
      gameId: string,
      query: string,
      strategyOverride?: string,
      threadId?: string,
      configOverride?: DebugChatConfigOverride,
      documentIds?: string[],
      includePrompts?: boolean
    ) => {
      stopStreaming();

      const requestId = Date.now();
      activeRequestIdRef.current = requestId;
      debugEventCounterRef.current = 0;
      const streamStartTime = Date.now();

      setState({
        ...INITIAL_STATE,
        isStreaming: true,
        statusMessage: 'Connecting...',
      });

      const abortController = new AbortController();
      abortControllerRef.current = abortController;

      const baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
      const url = `${baseUrl}/api/v1/admin/agents/debug-chat/stream`;

      const body: Record<string, unknown> = { gameId, query };
      if (threadId) body.chatId = threadId;
      if (strategyOverride) body.strategyOverride = strategyOverride;
      if (configOverride) body.configOverride = configOverride;
      if (documentIds && documentIds.length > 0) body.documentIds = documentIds;
      if (includePrompts) body.includePrompts = true;

      fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
        credentials: 'include',
        signal: abortController.signal,
      })
        .then(async response => {
          if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
          }

          const reader = response.body?.getReader();
          if (!reader) throw new Error('No response body');

          const decoder = new TextDecoder();
          let buffer = '';

          while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            if (activeRequestIdRef.current !== requestId) break;

            buffer += decoder.decode(value, { stream: true });

            const parts = buffer.split('\n\n');
            buffer = parts.pop() || '';

            for (const part of parts) {
              if (!part.trim()) continue;

              const dataMatch = part.match(/data:\s*([\s\S]+)/);
              if (!dataMatch) continue;

              let event: { type: number; data: unknown; timestamp: string };
              try {
                event = JSON.parse(dataMatch[1]);
              } catch {
                continue;
              }

              // Debug events (type >= 10): push to debugEvents array
              if (event.type >= 10) {
                const debugEvent: DebugEvent = {
                  id: ++debugEventCounterRef.current,
                  type: event.type,
                  typeName: DEBUG_EVENT_LABELS[event.type] || `Debug(${event.type})`,
                  data: event.data,
                  timestamp: event.timestamp,
                  elapsedMs: Date.now() - streamStartTime,
                };

                setState(prev => ({
                  ...prev,
                  debugEvents: [...prev.debugEvents, debugEvent],
                }));

                callbacksRef.current?.onDebugEvent?.(debugEvent);
                continue;
              }

              // Normal events (type 0-9): same handling as useAgentChatStream
              switch (event.type) {
                case StreamingEventType.StateUpdate: {
                  const data = event.data as { message?: string; chatThreadId?: string };
                  setState(prev => ({
                    ...prev,
                    statusMessage: data?.message || null,
                    chatThreadId: data?.chatThreadId || prev.chatThreadId,
                  }));
                  break;
                }

                case StreamingEventType.Token: {
                  const data = event.data as { token?: string };
                  if (data?.token) {
                    setState(prev => ({
                      ...prev,
                      currentAnswer: prev.currentAnswer + data.token,
                      statusMessage: null,
                    }));
                  }
                  break;
                }

                case StreamingEventType.FollowUpQuestions: {
                  const data = event.data as { questions?: string[] };
                  setState(prev => ({
                    ...prev,
                    followUpQuestions: data?.questions || [],
                  }));
                  break;
                }

                case StreamingEventType.Complete: {
                  if (activeRequestIdRef.current !== requestId) break;

                  const data = event.data as {
                    totalTokens?: number;
                    chatThreadId?: string;
                  };
                  setState(prev => {
                    const finalState = {
                      ...prev,
                      totalTokens: data?.totalTokens || 0,
                      chatThreadId: data?.chatThreadId || prev.chatThreadId,
                      isStreaming: false,
                      statusMessage: null,
                    };

                    callbacksRef.current?.onComplete?.(finalState.currentAnswer, {
                      totalTokens: finalState.totalTokens,
                      chatThreadId: finalState.chatThreadId,
                      debugEvents: finalState.debugEvents,
                    });

                    return finalState;
                  });
                  break;
                }

                case StreamingEventType.Error: {
                  const data = event.data as { errorMessage?: string; errorCode?: string };
                  const errorMsg = data?.errorMessage || 'Unknown error';
                  setState(prev => ({
                    ...prev,
                    error: errorMsg,
                    isStreaming: false,
                    statusMessage: null,
                  }));
                  callbacksRef.current?.onError?.(errorMsg);
                  break;
                }

                case StreamingEventType.Heartbeat:
                  break;

                default:
                  break;
              }
            }
          }

          // If stream ended without Complete event
          if (activeRequestIdRef.current === requestId) {
            setState(prev => {
              if (prev.isStreaming) {
                return { ...prev, isStreaming: false, statusMessage: null };
              }
              return prev;
            });
          }
        })
        .catch(error => {
          if (error.name === 'AbortError') {
            setState(prev => ({ ...prev, isStreaming: false, statusMessage: null }));
            return;
          }
          const errorMsg = error instanceof Error ? error.message : 'Stream failed';
          setState(prev => ({
            ...prev,
            error: errorMsg,
            isStreaming: false,
            statusMessage: null,
          }));
          callbacksRef.current?.onError?.(errorMsg);
        });
    },
    [stopStreaming]
  );

  return { state, sendMessage, stopStreaming, reset };
}
