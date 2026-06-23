'use client';
/**
 * useSessionAgentChat — SSE streaming hook for game night AI agent
 * Game Night Flow feature
 *
 * Sends questions to the session agent and accumulates streamed token responses.
 * Enriches requests with current game context from the session store.
 *
 * Endpoint: POST /api/v1/game-sessions/{gameSessionId}/agent/chat
 *
 * R2/R3/R4 (#2500 Task 5-FE): opt-in `persistHistory` flag enables sessionStorage
 * persistence of chatThreadId (keyed on gameSessionId) and history hydration on mount.
 * Default OFF to keep RulesExplainer and other consumers unaffected.
 */
import { useCallback, useEffect, useRef, useState } from 'react';

import type { ChatCitation } from '@/components/chat/panel/ChatCitationCard';
import { api } from '@/lib/api';
import { getAgentChatUrl } from '@/lib/api/clients/sessionAgentClient';
import { CitationSchema } from '@/lib/api/schemas/streaming.schemas';
import { mapCitationToChatCitation } from '@/lib/session-live/map-citation-to-chat-citation';
import { useSessionStore } from '@/stores/session/store';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  citations?: ChatCitation[];
  /**
   * AC-CHAT-3: true only when this is a genuine RAG assistant response with zero citations
   * (i.e., the agent answered but found no grounding in the rulebook).
   * Never set on user messages or system status messages injected by SessionLiveView.
   */
  isNonGrounded?: boolean;
}

/** Options for useSessionAgentChat. */
export interface UseSessionAgentChatOptions {
  /**
   * When true, enables sessionStorage persistence of chatThreadId and history
   * hydration on mount via GET /chat-threads/{threadId}.
   * Default: false (off) — keeps legacy behaviour for all existing consumers.
   */
  persistHistory?: boolean;
}

/** sessionStorage key for the persisted chatThreadId, keyed on gameSessionId. */
function buildStorageKey(gameSessionId: string): string {
  return `meepleai:live-agent-thread:${gameSessionId}`;
}

/** Safe sessionStorage read (guard SSR / private-browsing throws). */
function readThreadIdFromStorage(gameSessionId: string): string | null {
  try {
    if (typeof window === 'undefined') return null;
    return sessionStorage.getItem(buildStorageKey(gameSessionId));
  } catch {
    return null;
  }
}

/** Safe sessionStorage write. */
function writeThreadIdToStorage(gameSessionId: string, threadId: string): void {
  try {
    if (typeof window === 'undefined') return;
    sessionStorage.setItem(buildStorageKey(gameSessionId), threadId);
  } catch {
    // Ignore (quota exceeded, private browsing, etc.)
  }
}

/**
 * Hook for streaming AI agent chat within an active game session.
 *
 * @param gameSessionId - The game session ID (used in URL path)
 * @param agentSessionId - The agent session ID returned by LaunchSessionAgent (sent in body)
 * @param options - Optional opt-in flags. Pass `{ persistHistory: true }` in SessionLiveView only.
 */
export function useSessionAgentChat(
  gameSessionId: string,
  agentSessionId: string,
  options?: UseSessionAgentChatOptions
) {
  const persistHistory = options?.persistHistory ?? false;

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const threadIdRef = useRef<string | undefined>(undefined);
  const citationsRef = useRef<ChatCitation[]>([]);
  const abortRef = useRef<AbortController | null>(null);
  // Ref-based guard prevents double-submit race regardless of stale closure timing
  const isLoadingRef = useRef(false);

  // Granular selectors — avoid re-renders on unrelated store changes
  const gameId = useSessionStore(s => s.gameId);
  const gameTitle = useSessionStore(s => s.gameTitle);
  const participants = useSessionStore(s => s.participants);
  const currentTurn = useSessionStore(s => s.currentTurn);

  // R3/R4: on mount, hydrate thread history when persistHistory is enabled.
  // Runs only once (empty deps list — gameSessionId and persistHistory are stable for the
  // lifetime of the component instance, so no re-run is needed even if they change).
  useEffect(() => {
    if (!persistHistory) return;

    const savedThreadId = readThreadIdFromStorage(gameSessionId);
    if (!savedThreadId) return;

    // Hydrate the ref so that the next ask() continues the same thread (multi-turn).
    threadIdRef.current = savedThreadId;

    let cancelled = false;

    (async () => {
      try {
        const thread = await api.chat.getThreadById(savedThreadId);
        if (cancelled || !thread) return;

        const historicMessages: ChatMessage[] = thread.messages.map((dto, idx) => {
          // Parse citationsJson if present (assistant messages only per spec).
          let citations: ChatCitation[] | undefined;
          if (dto.citationsJson) {
            try {
              const parsed = JSON.parse(dto.citationsJson) as unknown;
              const result = CitationSchema.array().safeParse(parsed);
              const mapped = (result.data ?? [])
                .map(mapCitationToChatCitation)
                .filter((x): x is ChatCitation => x !== null);
              if (mapped.length > 0) citations = mapped;
            } catch {
              // Ignore malformed JSON — graceful failure per R4
            }
          }

          const role = (dto.role === 'user' ? 'user' : 'assistant') as 'user' | 'assistant';
          // AC-CHAT-3: historic assistant message with no citations → isNonGrounded true.
          // User messages never have this flag.
          const isNonGrounded =
            role === 'assistant' && (citations === undefined || citations.length === 0)
              ? true
              : undefined;

          return {
            id: dto.backendMessageId ?? `history-${idx}`,
            role,
            content: dto.content,
            timestamp: dto.timestamp,
            citations,
            isNonGrounded,
          };
        });

        setMessages(historicMessages);
      } catch (err) {
        // Graceful failure (R4): log but do not crash or block chat
        console.warn('[useSessionAgentChat] Failed to hydrate history:', err);
      }
    })();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const ask = useCallback(
    async (question: string) => {
      if (!question.trim() || isLoadingRef.current) return;

      isLoadingRef.current = true;
      setIsLoading(true);
      setError(null);
      setStreamingContent('');
      citationsRef.current = [];

      const userMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'user',
        content: question,
        timestamp: new Date().toISOString(),
      };
      setMessages(prev => [...prev, userMsg]);

      const gameContext =
        gameId && gameTitle
          ? {
              gameId,
              gameTitle,
              players: participants.map(p => p.displayName),
              currentTurn,
              responseLanguage: 'it',
            }
          : undefined;

      abortRef.current = new AbortController();

      try {
        const response = await fetch(getAgentChatUrl(gameSessionId), {
          method: 'POST',
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
            Accept: 'text/event-stream',
          },
          body: JSON.stringify({
            agentSessionId,
            userQuestion: question,
            chatThreadId: threadIdRef.current,
            gameContext,
          }),
          signal: abortRef.current.signal,
        });

        if (!response.ok) {
          throw new Error(`Errore ${response.status}`);
        }

        if (!response.body) {
          throw new Error('Response body is null');
        }

        const reader = response.body.getReader();

        // Ensure reader lock is released when the request is aborted
        abortRef.current.signal.addEventListener('abort', () => {
          void reader.cancel();
        });

        const decoder = new TextDecoder();
        let accumulated = '';
        // Buffer incomplete SSE lines across chunk boundaries
        let lineBuffer = '';

        try {
          while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            lineBuffer += decoder.decode(value, { stream: true });
            const lines = lineBuffer.split('\n');
            // Keep the last (potentially incomplete) line in the buffer
            lineBuffer = lines.pop() ?? '';

            for (const line of lines) {
              if (line.startsWith('data: ')) {
                try {
                  const payload = JSON.parse(line.slice(6)) as {
                    type: string;
                    content?: string;
                    threadId?: string;
                    citations?: unknown[];
                  };
                  if (payload.type === 'token' && payload.content) {
                    accumulated += payload.content;
                    setStreamingContent(accumulated);
                  } else if (payload.type === 'complete') {
                    if (payload.threadId) {
                      threadIdRef.current = payload.threadId;
                      // R3: persist to sessionStorage so it survives reload
                      if (persistHistory) {
                        writeThreadIdToStorage(gameSessionId, payload.threadId);
                      }
                    }
                    if (payload.citations) {
                      citationsRef.current = (
                        CitationSchema.array().safeParse(payload.citations).data ?? []
                      )
                        .map(mapCitationToChatCitation)
                        .filter((x): x is ChatCitation => x !== null);
                    }
                  }
                } catch {
                  // Ignore non-JSON lines (keep-alive, comments, etc.)
                }
              }
            }
          }
        } finally {
          reader.releaseLock();
        }

        // AC-CHAT-3: if the RAG response produced zero citations, flag as non-grounded.
        const hasCitations = citationsRef.current.length > 0;
        const assistantMsg: ChatMessage = {
          id: crypto.randomUUID(),
          role: 'assistant',
          content: accumulated,
          timestamp: new Date().toISOString(),
          citations: hasCitations ? citationsRef.current : undefined,
          isNonGrounded: hasCitations ? undefined : true,
        };
        setMessages(prev => [...prev, assistantMsg]);
        setStreamingContent('');
      } catch (err) {
        if ((err as Error).name !== 'AbortError') {
          setError("L'agente non è disponibile. Controlla la connessione.");
        }
      } finally {
        isLoadingRef.current = false;
        setIsLoading(false);
      }
    },
    // Removed `isLoading` — guard now uses isLoadingRef to avoid stale closure race
    // persistHistory is captured by closure from outer scope (stable)
    [gameSessionId, agentSessionId, gameId, gameTitle, participants, currentTurn, persistHistory]
  );

  const stop = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  return { messages, isLoading, error, streamingContent, ask, stop };
}
