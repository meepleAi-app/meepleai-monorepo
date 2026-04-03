'use client';
/**
 * useSessionAgentChat — SSE streaming hook for game night AI agent
 * Game Night Flow feature
 *
 * Sends questions to the session agent and accumulates streamed token responses.
 * Enriches requests with current game context from the session store.
 *
 * Endpoint: POST /api/v1/game-sessions/{gameSessionId}/agent/chat
 */
import { useCallback, useRef, useState } from 'react';

import { getAgentChatUrl } from '@/lib/api/clients/sessionAgentClient';
import { useSessionStore } from '@/stores/session/store';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
}

/**
 * Hook for streaming AI agent chat within an active game session.
 *
 * @param gameSessionId - The game session ID (used in URL path)
 * @param agentSessionId - The agent session ID returned by LaunchSessionAgent (sent in body)
 */
export function useSessionAgentChat(gameSessionId: string, agentSessionId: string) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [streamingContent, setStreamingContent] = useState('');
  const threadIdRef = useRef<string | undefined>(undefined);
  const abortRef = useRef<AbortController | null>(null);

  // Granular selectors — avoid re-renders on unrelated store changes
  const gameId = useSessionStore(s => s.gameId);
  const gameTitle = useSessionStore(s => s.gameTitle);
  const participants = useSessionStore(s => s.participants);
  const currentTurn = useSessionStore(s => s.currentTurn);

  const ask = useCallback(
    async (question: string) => {
      if (!question.trim() || isLoading) return;

      setIsLoading(true);
      setError(null);
      setStreamingContent('');

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

        const reader = response.body!.getReader();
        const decoder = new TextDecoder();
        let accumulated = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          const chunk = decoder.decode(value, { stream: true });
          const lines = chunk.split('\n');

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              try {
                const payload = JSON.parse(line.slice(6)) as {
                  type: string;
                  content?: string;
                  threadId?: string;
                };
                if (payload.type === 'token' && payload.content) {
                  accumulated += payload.content;
                  setStreamingContent(accumulated);
                } else if (payload.type === 'complete' && payload.threadId) {
                  threadIdRef.current = payload.threadId;
                }
              } catch {
                // Ignore non-JSON lines (keep-alive, comments, etc.)
              }
            }
          }
        }

        const assistantMsg: ChatMessage = {
          id: crypto.randomUUID(),
          role: 'assistant',
          content: accumulated,
          timestamp: new Date().toISOString(),
        };
        setMessages(prev => [...prev, assistantMsg]);
        setStreamingContent('');
      } catch (err) {
        if ((err as Error).name !== 'AbortError') {
          setError("L'agente non è disponibile. Controlla la connessione.");
        }
      } finally {
        setIsLoading(false);
      }
    },

    [gameSessionId, agentSessionId, gameId, gameTitle, participants, currentTurn, isLoading]
  );

  const stop = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  return { messages, isLoading, error, streamingContent, ask, stop };
}
