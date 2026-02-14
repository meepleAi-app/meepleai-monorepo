/**
 * useAgentChat - React Query hook for agent chat with SSE streaming
 * Issue #4126: API Integration
 */

import { useState, useCallback } from 'react';

import { api } from '@/lib/api';

export interface UseAgentChatOptions {
  onToken?: (token: string) => void;
  onComplete?: () => void;
  onError?: (error: Error) => void;
}

export function useAgentChat(agentId: string, options: UseAgentChatOptions = {}) {
  const [messages, setMessages] = useState<Array<{ role: 'user' | 'agent'; content: string }>>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const sendMessage = useCallback(
    async (message: string) => {
      // Add user message
      setMessages(prev => [...prev, { role: 'user', content: message }]);
      setIsStreaming(true);
      setError(null);

      let agentResponse = '';

      try {
        for await (const event of api.agents.chat(agentId, message)) {
          if (event.type === 'Token' && event.data && typeof event.data === 'object' && 'text' in event.data) {
            const token = (event.data as { text: string }).text;
            agentResponse += token;
            options.onToken?.(token);
          } else if (event.type === 'Complete') {
            // Add complete agent message
            setMessages(prev => [...prev, { role: 'agent', content: agentResponse }]);
            options.onComplete?.();
          } else if (event.type === 'Error') {
            throw new Error('Agent chat error');
          }
        }
      } catch (err) {
        const error = err instanceof Error ? err : new Error('Chat failed');
        setError(error);
        options.onError?.(error);
      } finally {
        setIsStreaming(false);
      }
    },
    [agentId, options]
  );

  return {
    messages,
    isStreaming,
    error,
    sendMessage,
  };
}
