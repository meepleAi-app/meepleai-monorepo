'use client';

import { useState, useRef, useCallback } from 'react';

import type { ChatMessage } from '@/components/game-night/SessionChatWidget';
import { api } from '@/lib/api';

interface UseSessionInlineChatReturn {
  messages: ChatMessage[];
  isStreaming: boolean;
  error: string | null;
  send: (text: string) => Promise<void>;
}

export function useSessionInlineChat(
  gameId: string | null | undefined
): UseSessionInlineChatReturn {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const threadIdRef = useRef<string | null>(null);

  const send = useCallback(
    async (text: string) => {
      setError(null);
      const userMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'user',
        content: text,
        timestamp: new Date(),
      };
      setMessages(prev => [...prev, userMsg]);
      setIsStreaming(true);

      try {
        let thread;
        if (!threadIdRef.current) {
          thread = await api.chat.createThread({
            gameId: gameId ?? null,
            initialMessage: text,
          });
          threadIdRef.current = thread.id;
        } else {
          thread = await api.chat.addMessage(threadIdRef.current!, { content: text, role: 'user' });
        }

        // Extract the last assistant message from the returned thread
        const assistantMsgs = thread.messages.filter(m => m.role === 'assistant');
        const last = assistantMsgs[assistantMsgs.length - 1];
        if (last) {
          const assistantMsg: ChatMessage = {
            id: last.backendMessageId ?? crypto.randomUUID(),
            role: 'assistant',
            content: last.content,
            timestamp: new Date(last.timestamp),
          };
          setMessages(prev => [...prev, assistantMsg]);
        }
      } catch {
        setError("Errore nell'invio del messaggio.");
      } finally {
        setIsStreaming(false);
      }
    },
    [gameId]
  );

  return { messages, isStreaming, error, send };
}
