'use client';

import { useState, useCallback } from 'react';

import type { ChatMessage } from '@/components/game-night/SessionChatWidget';
import { api } from '@/lib/api';

interface UseSessionInlineChatReturn {
  messages: ChatMessage[];
  isStreaming: boolean;
  send: (text: string) => void;
}

export function useSessionInlineChat(
  gameId: string | null | undefined
): UseSessionInlineChatReturn {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [threadId, setThreadId] = useState<string | null>(null);

  const send = useCallback(
    async (text: string) => {
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
        if (!threadId) {
          thread = await api.chat.createThread({
            gameId: gameId ?? null,
            initialMessage: text,
          });
          setThreadId(thread.id);
        } else {
          thread = await api.chat.addMessage(threadId, { content: text, role: 'user' });
        }

        // Extract the last assistant message from the returned thread
        const assistantMsgs = thread.messages.filter(
          (m: { role: string }) => m.role === 'assistant'
        );
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
        // Silent fail — user can retry
      } finally {
        setIsStreaming(false);
      }
    },
    [gameId, threadId]
  );

  return { messages, isStreaming, send };
}
