/**
 * useAgentChatStream - SSE streaming for agent chat
 * Issue #4364: SSE streaming in ChatThreadView
 *
 * Handles SSE connection to POST /api/v1/agents/{agentId}/chat
 * Parses RagStreamingEvent format (numeric StreamingEventType enum)
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
} as const;

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
}

/** Game context for OpenRouter proxy requests */
export interface ProxyGameContext {
  gameName: string;
  agentTypology: string;
  ragContext?: string;
}

export interface AgentChatStreamCallbacks {
  onComplete?: (answer: string, metadata: {
    totalTokens: number;
    chatThreadId: string | null;
    followUpQuestions: string[];
  }) => void;
  onError?: (error: string) => void;
}

const INITIAL_STATE: AgentChatStreamState = {
  statusMessage: null,
  currentAnswer: '',
  followUpQuestions: [],
  isStreaming: false,
  error: null,
  chatThreadId: null,
  totalTokens: 0,
};

export function useAgentChatStream(callbacks?: AgentChatStreamCallbacks) {
  const [state, setState] = useState<AgentChatStreamState>(INITIAL_STATE);
  const abortControllerRef = useRef<AbortController | null>(null);
  const callbacksRef = useRef(callbacks);
  const activeRequestIdRef = useRef<number>(0);
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
    setState(INITIAL_STATE);
  }, [stopStreaming]);

  const sendMessage = useCallback(
    (agentId: string, message: string, chatThreadId?: string, proxyGameContext?: ProxyGameContext) => {
      stopStreaming();

      // Track request ID to ignore stale completions after agent switch
      const requestId = Date.now();
      activeRequestIdRef.current = requestId;

      setState({
        ...INITIAL_STATE,
        isStreaming: true,
        statusMessage: 'Connecting...',
      });

      const abortController = new AbortController();
      abortControllerRef.current = abortController;

      // Issue #4780: Use OpenRouter proxy when gameContext is provided and env var is set
      const useProxy = !!proxyGameContext && process.env.NEXT_PUBLIC_USE_OPENROUTER_PROXY === 'true';

      let url: string;
      let body: Record<string, unknown>;

      if (useProxy) {
        url = '/api/chat-proxy';
        body = {
          message,
          agentId,
          threadId: chatThreadId,
          gameContext: proxyGameContext,
        };
      } else {
        const baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
        url = `${baseUrl}/api/v1/agents/${agentId}/chat`;
        body = { message };
        if (chatThreadId) {
          body.chatThreadId = chatThreadId;
        }
      }

      fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
        credentials: useProxy ? 'same-origin' : 'include',
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

            // Ignore events from stale requests (agent was switched)
            if (activeRequestIdRef.current !== requestId) break;

            buffer += decoder.decode(value, { stream: true });

            // Process complete SSE messages (separated by \n\n)
            const parts = buffer.split('\n\n');
            buffer = parts.pop() || '';

            for (const part of parts) {
              if (!part.trim()) continue;

              // Parse "data: {json}" format
              const dataMatch = part.match(/data:\s*([\s\S]+)/);
              if (!dataMatch) continue;

              // Backend uses camelCase serialization (SseJsonOptions.cs)
              let event: { type: number; data: unknown; timestamp: string };
              try {
                event = JSON.parse(dataMatch[1]);
              } catch {
                continue;
              }

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
                  // Ignore stale completions from a previous request
                  if (activeRequestIdRef.current !== requestId) break;

                  const data = event.data as {
                    totalTokens?: number;
                    chatThreadId?: string;
                    completionTokens?: number;
                    promptTokens?: number;
                  };
                  setState(prev => {
                    const finalState = {
                      ...prev,
                      totalTokens: data?.totalTokens || 0,
                      chatThreadId: data?.chatThreadId || prev.chatThreadId,
                      isStreaming: false,
                      statusMessage: null,
                    };

                    callbacksRef.current?.onComplete?.(
                      finalState.currentAnswer,
                      {
                        totalTokens: finalState.totalTokens,
                        chatThreadId: finalState.chatThreadId,
                        followUpQuestions: prev.followUpQuestions,
                      }
                    );

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
